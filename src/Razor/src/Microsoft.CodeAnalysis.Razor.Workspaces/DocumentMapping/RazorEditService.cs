// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Telemetry;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.DocumentMapping;

internal partial class RazorEditService(
    IDocumentMappingService documentMappingService,
    ITelemetryReporter telemetryReporter) : IRazorEditService
{
    private readonly IDocumentMappingService _documentMappingService = documentMappingService;
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;

    /// <summary>
    /// Maps the given text edits for a razor file based on changes in csharp. It special
    /// cases usings directives to insure they are added correctly. All other edits
    /// are applied if they map to the razor document.
    /// </summary>
    /// <remarks>
    /// Note that the changes coming in are in the generated C# file. This method will map them appropriately.
    /// </remarks>
    public async Task<ImmutableArray<RazorTextChange>> MapCSharpEditsAsync(
        ImmutableArray<RazorTextChange> textChanges,
        IDocumentSnapshot snapshot,
        bool includeCSharpLanguageFeatureEdits,
        CancellationToken cancellationToken)
    {
        var codeDocument = await snapshot.GetGeneratedOutputAsync(cancellationToken).ConfigureAwait(false);

        var originalCSharpSyntaxTree = await snapshot.GetCSharpSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        var originalCSharpSourceText = await originalCSharpSyntaxTree.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var originalCSharpSyntaxRoot = await originalCSharpSyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);

        var newCSharpSourceText = originalCSharpSourceText.WithChanges(textChanges.Select(c => c.ToTextChange()));
        var newCSharpSyntaxTree = originalCSharpSyntaxTree.WithChangedText(newCSharpSourceText);
        var newCSharpSyntaxRoot = await newCSharpSyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);

        using var edits = new PooledArrayBuilder<RazorTextChange>();
        AddDirectlyMappedEdits(ref edits.AsRef(), textChanges, codeDocument, cancellationToken);
        if (includeCSharpLanguageFeatureEdits)
        {
            AddCSharpLanguageFeatureChanges(ref edits.AsRef(), codeDocument, originalCSharpSyntaxRoot, originalCSharpSourceText, newCSharpSyntaxRoot, newCSharpSourceText, cancellationToken);
        }

        return NormalizeEdits(edits.ToImmutableOrderedBy(static e => e.Span.Start), cancellationToken);
    }

    /// <summary>
    /// Go through edits and make sure a few things are true:
    ///
    /// <list type="number">
    /// <item>
    ///  No edit is added twice. This can happen if a rename happens.
    /// </item>
    /// <item>
    ///  No edit overlaps with another edit. If they do throw to capture logs but choose the first
    ///  edit to at least not completely fail. It's possible this will need to be tweaked later.
    /// </item>
    /// </list>
    /// </summary>
    private ImmutableArray<RazorTextChange> NormalizeEdits(ImmutableArray<RazorTextChange> changes, CancellationToken cancellationToken)
    {
        // Ensure that the changes are sorted by start position otherwise
        // the normalization logic will not work.
        Debug.Assert(changes.SequenceEqual(changes.OrderBy(static c => c.Span.Start)));

        using var normalizedChanges = new PooledArrayBuilder<RazorTextChange>(changes.Length);
        var remaining = changes.AsSpan();

        var droppedEdits = 0;
        while (remaining is not [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (remaining is [var edit, var nextEdit, ..])
            {
                var editSpan = edit.Span.ToTextSpan();
                var nextEditSpan = nextEdit.Span.ToTextSpan();

                if (editSpan == nextEditSpan)
                {
                    normalizedChanges.Add(nextEdit);
                    remaining = remaining[1..];

                    if (edit.NewText != nextEdit.NewText)
                    {
                        droppedEdits++;
                    }
                }
                else if (editSpan.Contains(nextEditSpan))
                {
                    // Cases where there was a removal and addition on the same
                    // line err to taking the addition. This can happen in the
                    // case of a namespace rename
                    if (editSpan.Start == nextEditSpan.Start)
                    {
                        if (string.IsNullOrEmpty(edit.NewText) && !string.IsNullOrEmpty(nextEdit.NewText))
                        {
                            // Don't count this as a dropped edit, it is expected
                            // in the case of a rename
                            normalizedChanges.Add(new RazorTextChange()
                            {
                                Span = edit.Span,
                                NewText = nextEdit.NewText
                            });
                            remaining = remaining[1..];
                        }
                        else
                        {
                            normalizedChanges.Add(edit);
                            remaining = remaining[1..];
                            droppedEdits++;
                        }
                    }
                    else
                    {
                        normalizedChanges.Add(edit);

                        remaining = remaining[1..];
                        droppedEdits++;
                    }
                }
                else if (nextEditSpan.Contains(editSpan))
                {
                    // Add the edit that is contained in the other edit
                    // and skip the next edit.
                    normalizedChanges.Add(nextEdit);
                    remaining = remaining[1..];
                    if (edit.NewText != nextEdit.NewText)
                    {
                        droppedEdits++;
                    }
                }
                else
                {
                    normalizedChanges.Add(edit);
                }
            }
            else
            {
                normalizedChanges.Add(remaining[0]);
            }

            remaining = remaining[1..];
        }

        if (droppedEdits > 0)
        {
            _telemetryReporter.ReportFault(
                new DroppedEditsException(),
                "Potentially dropped edits when trying to map",
                new Property("droppedEditCount", droppedEdits));
        }

        return normalizedChanges.ToImmutable();
    }

    /// <summary>
    /// Maps changes in a C# generated document back to the Razor document, preserving the existing
    /// special handling for partially mapped multi-line edits and edits that need to insert content
    /// before a transition on a directive line.
    /// </summary>
    private ImmutableArray<TextChange> GetRazorDocumentEdits(RazorCSharpDocument csharpDocument, ImmutableArray<TextChange> csharpChanges)
    {
        using var result = new PooledArrayBuilder<TextChange>();
        var csharpSourceText = csharpDocument.Text;
        var lastNewLineAddedToLine = 0;

        foreach (var change in csharpChanges)
        {
            var span = change.Span;
            // Deliberately doing a naive check to avoid telemetry for truly bad data
            if (span.Start <= 0 || span.Start >= csharpSourceText.Length || span.End <= 0 || span.End >= csharpSourceText.Length)
            {
                continue;
            }

            var (startLine, startChar) = csharpSourceText.GetLinePosition(span.Start);
            var (endLine, _) = csharpSourceText.GetLinePosition(span.End);

            var mappedStart = _documentMappingService.TryMapToRazorDocumentPosition(csharpDocument, span.Start, out _, out var hostStartIndex);
            var mappedEnd = _documentMappingService.TryMapToRazorDocumentPosition(csharpDocument, span.End, out _, out var hostEndIndex);

            // Ideal case, both start and end can be mapped so just return the edit
            if (mappedStart && mappedEnd)
            {
                // If the previous edit was on the same line, and added a newline, then we need to add a space
                // between this edit and the previous one, because the normalization will have swallowed it. See
                // below for a more info.
                var newText = (lastNewLineAddedToLine == startLine ? " " : "") + change.NewText;
                result.Add(new TextChange(TextSpan.FromBounds(hostStartIndex, hostEndIndex), newText));
                continue;
            }

            // For the first line of a code block the C# formatter will often return an edit that starts
            // before our mapping, but ends within. In those cases, when the edit spans multiple lines
            // we just take the last line and try to use that.
            if (!mappedStart && mappedEnd && startLine != endLine)
            {
                // Construct a theoretical edit that is just for the last line of the edit that the C# formatter
                // gave us, and see if we can map that.
                // The +1 here skips the newline character that is found, but also protects from Substring throwing
                // if there are no newlines (which should be impossible anyway)
                var lastNewLine = change.NewText.AssumeNotNull().LastIndexOfAny(['\n', '\r']) + 1;

                // Strictly speaking we could be dropping more lines than we need to, because our mapping point could be anywhere within the edit
                // but we know that the C# formatter will only be returning blank lines up until the first bit of content that needs to be indented
                // so we can ignore all but the last line. This assert ensures that is true, just in case something changes in Roslyn
                Debug.Assert(lastNewLine == 0 || change.NewText[..(lastNewLine - 1)].All(static c => c == '\r' || c == '\n'), "We are throwing away part of an edit that has more than just empty lines!");

                var startSync = csharpSourceText.TryGetAbsoluteIndex((endLine, 0), out var startIndex);
                if (startSync is false)
                {
                    break;
                }

                mappedStart = _documentMappingService.TryMapToRazorDocumentPosition(csharpDocument, startIndex, out _, out hostStartIndex);

                if (mappedStart && mappedEnd)
                {
                    result.Add(new TextChange(TextSpan.FromBounds(hostStartIndex, hostEndIndex), change.NewText[lastNewLine..]));
                    continue;
                }
            }

            // The opposite case of the above: for the last line of a code block, the C# formatter might
            // return an edit that starts within our mapping, but ends after. In those cases, when the edit
            // spans multiple lines we just take the first line and try to use that.
            if (mappedStart && !mappedEnd && startLine != endLine)
            {
                // Construct a theoretical edit that is just for the first line of the edit that the C# formatter
                // gave us, and see if we can map that.
                if (!csharpSourceText.TryGetAbsoluteIndex(startLine, csharpSourceText.Lines[startLine].Span.Length, out var endIndex))
                {
                    break;
                }

                if (_documentMappingService.TryMapToRazorDocumentPosition(csharpDocument, endIndex, out _, out hostEndIndex))
                {
                    // If there's a newline in the new text, only take the part before it
                    var firstNewLine = change.NewText.AssumeNotNull().IndexOfAny(['\n', '\r']);
                    var newText = firstNewLine >= 0 ? change.NewText[..firstNewLine] : change.NewText;
                    result.Add(new TextChange(TextSpan.FromBounds(hostStartIndex, hostEndIndex), newText));
                    continue;
                }
            }

            // If we couldn't map either the start or the end then we still might want to do something tricky.
            // When we have a block like this:
            //
            // @functions {
            //    class Goo
            //    {
            //    }
            // }
            //
            // The source mapping starts at char 13 on the "@functions" line (after the open brace). Unfortunately
            // and code that is needed on that line, say an attribute that the code action wants to insert, will
            // start at char 8 because of the desired indentation of that new code. This means it starts outside of the
            // mapping, so is thrown away, which results in data loss.
            //
            // To fix this we check and if the mapping would have been successful at the end of the line (char 13 above)
            // then we insert a newline, and enough indentation to get us back out to where the new code wanted to start (char 8)
            // and then we're good - we've left the @functions bit alone which razor needs, but we're still able to insert
            // new code above where the C# code is in the generated document.
            //
            // One last hurdle is that sometimes these edits come in as separate edits. So for example replacing "class Goo" above
            // with "public class Goo" would come in as one edit for "public", one for "class" and one for "Goo", all on the same line.
            // When we map the edit for "public" we will push everything down a line, so we don't want to do it for other edits
            // on that line.
            if (!mappedStart && !mappedEnd && startLine == endLine)
            {
                // If the new text doesn't have any content we don't care - throwing away invisible whitespace is fine
                if (string.IsNullOrWhiteSpace(change.NewText))
                {
                    continue;
                }

                var line = csharpSourceText.Lines[startLine];

                // If the line isn't blank, then this isn't a functions directive
                if (line.GetFirstNonWhitespaceOffset() is not null)
                {
                    continue;
                }

                // Only do anything if the end of the line in question is a valid mapping point (ie, a transition)
                if (_documentMappingService.TryMapToRazorDocumentPosition(csharpDocument, line.Span.End, out _, out hostEndIndex))
                {
                    if (startLine == lastNewLineAddedToLine)
                    {
                        // If we already added a newline to this line, then we don't want to add another one, but
                        // we do need to add a space between this edit and the previous one, because the normalization
                        // will have swallowed it.
                        result.Add(new TextChange(new TextSpan(hostEndIndex, 0), " " + change.NewText));
                    }
                    else
                    {
                        // Otherwise, add a newline and the real content, and remember where we added it
                        lastNewLineAddedToLine = startLine;
                        result.Add(new TextChange(new TextSpan(hostEndIndex, 0), " " + Environment.NewLine + new string(' ', startChar) + change.NewText));
                    }

                    continue;
                }
            }
        }

        return result.ToImmutableAndClear();
    }

    /// <summary>
    /// For all edits that are not mapped to using directives, map them directly to the Razor document.
    /// Edits that don't map are skipped, and using directive changes are handled separately
    /// by <see cref="AddUsingsChanges"/>.
    /// </summary>
    private void AddDirectlyMappedEdits(
        ref PooledArrayBuilder<RazorTextChange> edits,
        ImmutableArray<RazorTextChange> csharpEdits,
        RazorCodeDocument codeDocument,
        CancellationToken cancellationToken)
    {
        var root = codeDocument.GetRequiredSyntaxRoot();
        var csharpDocument = codeDocument.GetRequiredCSharpDocument();
        var mappedEdits = GetRazorDocumentEdits(csharpDocument, csharpEdits.SelectAsArray(static e => e.ToTextChange()));

        foreach (var mappedEdit in mappedEdits)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var mappedSpan = mappedEdit.Span;
            var node = root.FindNode(mappedSpan, getInnermostNodeForTie: true);
            if (node is null)
            {
                continue;
            }

            if (RazorSyntaxFacts.IsInUsingDirective(node))
            {
                continue;
            }

            edits.Add(new RazorTextChange()
            {
                Span = mappedSpan.ToRazorTextSpan(),
                NewText = mappedEdit.NewText
            });

            if (node is BaseMarkupStartTagSyntax startTagSyntax &&
                startTagSyntax.GetEndTag() is { } endTag)
            {
                // We are changing a start tag, and so we have a matching end tag. We have to translate the edit over there too
                // as we only map the start tag, but if they got out of sync that would be bad.
                edits.Add(new RazorTextChange()
                {
                    Span = new RazorTextSpan()
                    {
                        Start = mappedSpan.Start + (endTag.Name.SpanStart - startTagSyntax.Name.SpanStart),
                        Length = mappedSpan.Length
                    },
                    NewText = mappedEdit.NewText
                });
            }
        }
    }

    private sealed class DroppedEditsException : Exception
    {
    }
}
