﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal sealed class RazorDocumentMappingService : IRazorDocumentMappingService
{
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions;
    private readonly DocumentContextFactory _documentContextFactory;
    private readonly ILogger _logger;

    public RazorDocumentMappingService(
        LanguageServerFeatureOptions languageServerFeatureOptions,
        DocumentContextFactory documentContextFactory,
        ILoggerFactory loggerFactory)
    {
        if (languageServerFeatureOptions is null)
        {
            throw new ArgumentNullException(nameof(languageServerFeatureOptions));
        }

        if (documentContextFactory is null)
        {
            throw new ArgumentNullException(nameof(documentContextFactory));
        }

        if (loggerFactory is null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _languageServerFeatureOptions = languageServerFeatureOptions;
        _documentContextFactory = documentContextFactory;
        _logger = loggerFactory.CreateLogger<RazorDocumentMappingService>();
    }

    public TextEdit[] GetHostDocumentEdits(IRazorGeneratedDocument generatedDocument, TextEdit[] generatedDocumentEdits)
    {
        var hostDocumentEdits = new List<TextEdit>();
        var generatedDocumentSourceText = GetGeneratedSourceText(generatedDocument);
        var lastNewLineAddedToLine = 0;

        foreach (var edit in generatedDocumentEdits)
        {
            var range = edit.Range;
            if (!IsRangeWithinDocument(range, generatedDocumentSourceText))
            {
                continue;
            }

            var startSync = range.Start.TryGetAbsoluteIndex(generatedDocumentSourceText, _logger, out var startIndex);
            var endSync = range.End.TryGetAbsoluteIndex(generatedDocumentSourceText, _logger, out var endIndex);
            if (startSync is false || endSync is false)
            {
                break;
            }

            var mappedStart = this.TryMapToHostDocumentPosition(generatedDocument, startIndex, out var hostDocumentStart, out _);
            var mappedEnd = this.TryMapToHostDocumentPosition(generatedDocument, endIndex, out var hostDocumentEnd, out _);

            // Ideal case, both start and end can be mapped so just return the edit
            if (mappedStart && mappedEnd)
            {
                // If the previous edit was on the same line, and added a newline, then we need to add a space
                // between this edit and the previous one, because the normalization will have swallowed it. See
                // below for a more info.
                var newText = (lastNewLineAddedToLine == range.Start.Line ? " " : "") + edit.NewText;
                hostDocumentEdits.Add(new TextEdit()
                {
                    NewText = newText,
                    Range = new Range { Start = hostDocumentStart!, End = hostDocumentEnd! },
                });
                continue;
            }

            // For the first line of a code block the C# formatter will often return an edit that starts
            // before our mapping, but ends within. In those cases, when the edit spans multiple lines
            // we just take the last line and try to use that.
            //
            // eg in the C# document you might see:
            //
            //      protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
            //      {
            // #nullable restore
            // #line 1 "/path/to/Document.component"
            //
            //          var x = DateTime.Now;
            //
            // To indent the 'var x' line the formatter will return an edit that starts the line before,
            // with a NewText of '\n            '. The start of that edit is outside our mapping, but we
            // still want to know how to format the 'var x' line, so we have to break up the edit.
            if (!mappedStart && mappedEnd && range.SpansMultipleLines())
            {
                // Construct a theoretical edit that is just for the last line of the edit that the C# formatter
                // gave us, and see if we can map that.
                // The +1 here skips the newline character that is found, but also protects from Substring throwing
                // if there are no newlines (which should be impossible anyway)
                var lastNewLine = edit.NewText.LastIndexOfAny(new char[] { '\n', '\r' }) + 1;

                // Strictly speaking we could be dropping more lines than we need to, because our mapping point could be anywhere within the edit
                // but we know that the C# formatter will only be returning blank lines up until the first bit of content that needs to be indented
                // so we can ignore all but the last line. This assert ensures that is true, just in case something changes in Roslyn
                Debug.Assert(lastNewLine == 0 || edit.NewText[..(lastNewLine - 1)].All(c => c == '\r' || c == '\n'), "We are throwing away part of an edit that has more than just empty lines!");

                var proposedRange = new Range { Start = new Position(range.End.Line, 0), End = new Position(range.End.Line, range.End.Character) };
                startSync = proposedRange.Start.TryGetAbsoluteIndex(generatedDocumentSourceText, _logger, out startIndex);
                endSync = proposedRange.End.TryGetAbsoluteIndex(generatedDocumentSourceText, _logger, out endIndex);
                if (startSync is false || endSync is false)
                {
                    break;
                }

                mappedStart = this.TryMapToHostDocumentPosition(generatedDocument, startIndex, out hostDocumentStart, out _);
                mappedEnd = this.TryMapToHostDocumentPosition(generatedDocument, endIndex, out hostDocumentEnd, out _);

                if (mappedStart && mappedEnd)
                {
                    hostDocumentEdits.Add(new TextEdit()
                    {
                        NewText = edit.NewText[lastNewLine..],
                        Range = new Range { Start = hostDocumentStart!, End = hostDocumentEnd! },
                    });
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
            if (!mappedStart && !mappedEnd && !range.SpansMultipleLines())
            {
                // If the new text doesn't have any content we don't care - throwing away invisible whitespace is fine
                if (string.IsNullOrWhiteSpace(edit.NewText))
                {
                    continue;
                }

                var line = generatedDocumentSourceText.Lines[range.Start.Line];

                // If the line isn't blank, then this isn't a functions directive
                if (line.GetFirstNonWhitespaceOffset() is not null)
                {
                    continue;
                }

                // Only do anything if the end of the line in question is a valid mapping point (ie, a transition)
                var endOfLine = line.Span.End;
                if (this.TryMapToHostDocumentPosition(generatedDocument, endOfLine, out var hostDocumentIndex, out _))
                {
                    if (range.Start.Line == lastNewLineAddedToLine)
                    {
                        // If we already added a newline to this line, then we don't want to add another one, but
                        // we do need to add a space between this edit and the previous one, because the normalization
                        // will have swallowed it.
                        hostDocumentEdits.Add(new TextEdit()
                        {
                            NewText = " " + edit.NewText,
                            Range = new Range { Start = hostDocumentIndex, End = hostDocumentIndex }
                        });
                    }
                    else
                    {
                        // Otherwise, add a newline and the real content, and remember where we added it
                        lastNewLineAddedToLine = range.Start.Line;
                        hostDocumentEdits.Add(new TextEdit()
                        {
                            NewText = Environment.NewLine + new string(' ', range.Start.Character) + edit.NewText,
                            Range = new Range { Start = hostDocumentIndex, End = hostDocumentIndex }
                        });
                    }

                    continue;
                }
            }
        }

        return hostDocumentEdits.ToArray();
    }

    public bool TryMapToHostDocumentRange(IRazorGeneratedDocument generatedDocument, Range generatedDocumentRange, MappingBehavior mappingBehavior, [NotNullWhen(true)] out Range? hostDocumentRange)
    {
        if (generatedDocument is null)
        {
            throw new ArgumentNullException(nameof(generatedDocument));
        }

        if (generatedDocumentRange is null)
        {
            throw new ArgumentNullException(nameof(generatedDocumentRange));
        }

        if (mappingBehavior == MappingBehavior.Strict)
        {
            return TryMapToHostDocumentRangeStrict(generatedDocument, generatedDocumentRange, out hostDocumentRange);
        }
        else if (mappingBehavior == MappingBehavior.Inclusive)
        {
            return TryMapToHostDocumentRangeInclusive(generatedDocument, generatedDocumentRange, out hostDocumentRange);
        }
        else if (mappingBehavior == MappingBehavior.Inferred)
        {
            return TryMapToHostDocumentRangeInferred(generatedDocument, generatedDocumentRange, out hostDocumentRange);
        }
        else
        {
            throw new InvalidOperationException(SR.Unknown_mapping_behavior);
        }
    }

    public bool TryMapToGeneratedDocumentRange(IRazorGeneratedDocument generatedDocument, Range hostDocumentRange, [NotNullWhen(true)] out Range? generatedDocumentRange)
    {
        if (generatedDocument is null)
        {
            throw new ArgumentNullException(nameof(generatedDocument));
        }

        if (hostDocumentRange is null)
        {
            throw new ArgumentNullException(nameof(hostDocumentRange));
        }

        if (generatedDocument.CodeDocument is not { } codeDocument)
        {
            throw new InvalidOperationException("Cannot use document mapping service on a generated document that has a null CodeDocument.");
        }

        generatedDocumentRange = default;

        if ((hostDocumentRange.End.Line < hostDocumentRange.Start.Line) ||
            (hostDocumentRange.End.Line == hostDocumentRange.Start.Line &&
             hostDocumentRange.End.Character < hostDocumentRange.Start.Character))
        {
            _logger.LogWarning("RazorDocumentMappingService:TryMapToGeneratedDocumentRange original range end < start '{originalRange}'", hostDocumentRange);
            Debug.Fail($"RazorDocumentMappingService:TryMapToGeneratedDocumentRange original range end < start '{hostDocumentRange}'");
            return false;
        }

        var sourceText = codeDocument.GetSourceText();
        var range = hostDocumentRange;
        if (!IsRangeWithinDocument(range, sourceText))
        {
            return false;
        }

        if (!range.Start.TryGetAbsoluteIndex(sourceText, _logger, out var startIndex) ||
            !TryMapToGeneratedDocumentPosition(generatedDocument, startIndex, out var generatedRangeStart, out var _))
        {
            return false;
        }

        if (!range.End.TryGetAbsoluteIndex(sourceText, _logger, out var endIndex) ||
            !TryMapToGeneratedDocumentPosition(generatedDocument, endIndex, out var generatedRangeEnd, out var _))
        {
            return false;
        }

        // Ensures a valid range is returned.
        // As we're doing two separate TryMapToGeneratedDocumentPosition calls,
        // it's possible the generatedRangeStart and generatedRangeEnd positions are in completely
        // different places in the document, including the possibility that the
        // generatedRangeEnd position occurs before the generatedRangeStart position.
        // We explicitly disallow such ranges where the end < start.
        if ((generatedRangeEnd.Line < generatedRangeStart.Line) ||
            (generatedRangeEnd.Line == generatedRangeStart.Line &&
             generatedRangeEnd.Character < generatedRangeStart.Character))
        {
            return false;
        }

        generatedDocumentRange = new Range
        {
            Start = generatedRangeStart,
            End = generatedRangeEnd,
        };

        return true;
    }

    public bool TryMapToHostDocumentPosition(IRazorGeneratedDocument generatedDocument, int generatedDocumentIndex, [NotNullWhen(true)] out Position? hostDocumentPosition, out int hostDocumentIndex)
    {
        if (generatedDocument is null)
        {
            throw new ArgumentNullException(nameof(generatedDocument));
        }

        if (generatedDocument.CodeDocument is not { } codeDocument)
        {
            throw new InvalidOperationException("Cannot use document mapping service on a generated document that has a null CodeDocument.");
        }

        foreach (var mapping in generatedDocument.SourceMappings)
        {
            var generatedSpan = mapping.GeneratedSpan;
            var generatedAbsoluteIndex = generatedSpan.AbsoluteIndex;
            if (generatedAbsoluteIndex <= generatedDocumentIndex)
            {
                // Treat the mapping as owning the edge at its end (hence <= originalSpan.Length),
                // otherwise we wouldn't handle the cursor being right after the final C# char
                var distanceIntoGeneratedSpan = generatedDocumentIndex - generatedAbsoluteIndex;
                if (distanceIntoGeneratedSpan <= generatedSpan.Length)
                {
                    // Found the generated span that contains the generated absolute index

                    hostDocumentIndex = mapping.OriginalSpan.AbsoluteIndex + distanceIntoGeneratedSpan;
                    var originalLocation = codeDocument.Source.Lines.GetLocation(hostDocumentIndex);
                    hostDocumentPosition = new Position(originalLocation.LineIndex, originalLocation.CharacterIndex);
                    return true;
                }
            }
        }

        hostDocumentPosition = default;
        hostDocumentIndex = default;
        return false;
    }

    public bool TryMapToGeneratedDocumentOrNextCSharpPosition(IRazorGeneratedDocument generatedDocument, int hostDocumentIndex, [NotNullWhen(true)] out Position? generatedPosition, out int generatedIndex)
        => TryMapToGeneratedDocumentPositionInternal(generatedDocument, hostDocumentIndex, nextCSharpPositionOnFailure: true, out generatedPosition, out generatedIndex);

    public bool TryMapToGeneratedDocumentPosition(IRazorGeneratedDocument generatedDocument, int hostDocumentIndex, [NotNullWhen(true)] out Position? generatedPosition, out int generatedIndex)
        => TryMapToGeneratedDocumentPositionInternal(generatedDocument, hostDocumentIndex, nextCSharpPositionOnFailure: false, out generatedPosition, out generatedIndex);

    private static bool TryMapToGeneratedDocumentPositionInternal(IRazorGeneratedDocument generatedDocument, int hostDocumentIndex, bool nextCSharpPositionOnFailure, [NotNullWhen(true)] out Position? generatedPosition, out int generatedIndex)
    {
        if (generatedDocument is null)
        {
            throw new ArgumentNullException(nameof(generatedDocument));
        }

        if (generatedDocument.CodeDocument is not { } codeDocument)
        {
            throw new InvalidOperationException("Cannot use document mapping service on a generated document that has a null CodeDocument.");
        }

        foreach (var mapping in generatedDocument.SourceMappings)
        {
            var originalSpan = mapping.OriginalSpan;
            var originalAbsoluteIndex = originalSpan.AbsoluteIndex;
            if (originalAbsoluteIndex <= hostDocumentIndex)
            {
                // Treat the mapping as owning the edge at its end (hence <= originalSpan.Length),
                // otherwise we wouldn't handle the cursor being right after the final C# char
                var distanceIntoOriginalSpan = hostDocumentIndex - originalAbsoluteIndex;
                if (distanceIntoOriginalSpan <= originalSpan.Length)
                {
                    generatedIndex = mapping.GeneratedSpan.AbsoluteIndex + distanceIntoOriginalSpan;
                    generatedPosition = GetGeneratedPosition(generatedDocument, generatedIndex);
                    return true;
                }
            }
            else if (nextCSharpPositionOnFailure)
            {
                // The "next" C# location is only valid if it is on the same line in the source document
                // as the requested position.
                codeDocument.GetSourceText().GetLineAndOffset(hostDocumentIndex, out var hostDocumentLine, out _);

                if (mapping.OriginalSpan.LineIndex == hostDocumentLine)
                {
                    generatedIndex = mapping.GeneratedSpan.AbsoluteIndex;
                    generatedPosition = GetGeneratedPosition(generatedDocument, generatedIndex);
                    return true;
                }

                break;
            }
        }

        generatedPosition = default;
        generatedIndex = default;
        return false;

        static Position GetGeneratedPosition(IRazorGeneratedDocument generatedDocument, int generatedIndex)
        {
            var generatedSource = GetGeneratedSourceText(generatedDocument);
            var generatedLinePosition = generatedSource.Lines.GetLinePosition(generatedIndex);
            var generatedPosition = new Position(generatedLinePosition.Line, generatedLinePosition.Character);
            return generatedPosition;
        }
    }

    public RazorLanguageKind GetLanguageKind(RazorCodeDocument codeDocument, int hostDocumentIndex, bool rightAssociative)
    {
        if (codeDocument is null)
        {
            throw new ArgumentNullException(nameof(codeDocument));
        }

        var classifiedSpans = GetClassifiedSpans(codeDocument);
        var tagHelperSpans = GetTagHelperSpans(codeDocument);
        var documentLength = codeDocument.Source.Length;
        var languageKind = GetLanguageKindCore(classifiedSpans, tagHelperSpans, hostDocumentIndex, documentLength, rightAssociative);

        return languageKind;
    }

    public async Task<WorkspaceEdit> RemapWorkspaceEditAsync(WorkspaceEdit workspaceEdit, CancellationToken cancellationToken)
    {
        if (workspaceEdit.TryGetDocumentChanges(out var documentChanges))
        {
            // The LSP spec says, we should prefer `DocumentChanges` property over `Changes` if available.
            var remappedEdits = await RemapVersionedDocumentEditsAsync(documentChanges, cancellationToken).ConfigureAwait(false);
            return new WorkspaceEdit()
            {
                DocumentChanges = remappedEdits
            };
        }
        else if (workspaceEdit.Changes != null)
        {
            var remappedEdits = await RemapDocumentEditsAsync(workspaceEdit.Changes, cancellationToken).ConfigureAwait(false);
            return new WorkspaceEdit()
            {
                Changes = remappedEdits
            };
        }

        return workspaceEdit;
    }

    public async Task<(Uri MappedDocumentUri, Range MappedRange)> MapToHostDocumentUriAndRangeAsync(Uri generatedDocumentUri, Range generatedDocumentRange, CancellationToken cancellationToken)
    {
        var razorDocumentUri = _languageServerFeatureOptions.GetRazorDocumentUri(generatedDocumentUri);

        // For Html we just map the Uri, the range will be the same
        if (_languageServerFeatureOptions.IsVirtualHtmlFile(generatedDocumentUri))
        {
            return (razorDocumentUri, generatedDocumentRange);
        }

        // We only map from C# files
        if (!_languageServerFeatureOptions.IsVirtualCSharpFile(generatedDocumentUri))
        {
            return (generatedDocumentUri, generatedDocumentRange);
        }

        var documentContext = await _documentContextFactory.TryCreateAsync(razorDocumentUri, cancellationToken).ConfigureAwait(false);
        if (documentContext is null)
        {
            return (generatedDocumentUri, generatedDocumentRange);
        }

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var generatedDocument = GetGeneratedDocumentFromGeneratedDocumentUri(generatedDocumentUri, codeDocument);

        // We already checked that the uri was for a generated document, above
        Assumes.NotNull(generatedDocument);

        if (TryMapToHostDocumentRange(generatedDocument, generatedDocumentRange, MappingBehavior.Strict, out var mappedRange))
        {
            return (razorDocumentUri, mappedRange);
        }

        return (generatedDocumentUri, generatedDocumentRange);
    }

    // Internal for testing
    internal static RazorLanguageKind GetLanguageKindCore(
        ImmutableArray<ClassifiedSpanInternal> classifiedSpans,
        ImmutableArray<TagHelperSpanInternal> tagHelperSpans,
        int hostDocumentIndex,
        int hostDocumentLength,
        bool rightAssociative)
    {
        var length = classifiedSpans.Length;
        for (var i = 0; i < length; i++)
        {
            var classifiedSpan = classifiedSpans[i];
            var span = classifiedSpan.Span;

            if (span.AbsoluteIndex <= hostDocumentIndex)
            {
                var end = span.AbsoluteIndex + span.Length;
                if (end >= hostDocumentIndex)
                {
                    if (end == hostDocumentIndex)
                    {
                        // We're at an edge.

                        if (span.Length > 0 &&
                            classifiedSpan.AcceptedCharacters == AcceptedCharactersInternal.None)
                        {
                            // Non-marker spans do not own the edges after it
                            continue;
                        }

                        // If we're right associative, then we don't want to use the classification that we're at the end
                        // of, if we're also at the start of the next one
                        if (rightAssociative)
                        {
                            if (i < classifiedSpans.Length - 1 && classifiedSpans[i + 1].Span.AbsoluteIndex == hostDocumentIndex)
                            {
                                // If we're at the start of the next span, then use that span
                                return GetLanguageFromClassifiedSpan(classifiedSpans[i + 1]);
                            }

                            // Otherwise, we did not find a match using right associativity, so check for tag helpers
                            break;
                        }
                    }

                    return GetLanguageFromClassifiedSpan(classifiedSpan);
                }
            }
        }

        foreach (var tagHelperSpan in tagHelperSpans)
        {
            var span = tagHelperSpan.Span;

            if (span.AbsoluteIndex <= hostDocumentIndex)
            {
                var end = span.AbsoluteIndex + span.Length;
                if (end >= hostDocumentIndex)
                {
                    if (end == hostDocumentIndex)
                    {
                        // We're at an edge. TagHelper spans never own their edge and aren't represented by marker spans
                        continue;
                    }

                    // Found intersection
                    return RazorLanguageKind.Html;
                }
            }
        }

        // Use the language of the last classified span if we're at the end
        // of the document.
        if (classifiedSpans.Length != 0 && hostDocumentIndex == hostDocumentLength)
        {
            var lastClassifiedSpan = classifiedSpans.Last();
            return GetLanguageFromClassifiedSpan(lastClassifiedSpan);
        }

        // Default to Razor
        return RazorLanguageKind.Razor;

        static RazorLanguageKind GetLanguageFromClassifiedSpan(ClassifiedSpanInternal classifiedSpan)
        {
            // Overlaps with request
            return classifiedSpan.SpanKind switch
            {
                SpanKindInternal.Markup => RazorLanguageKind.Html,
                SpanKindInternal.Code => RazorLanguageKind.CSharp,

                // Content type was non-C# or Html or we couldn't find a classified span overlapping the request position.
                // All other classified span kinds default back to Razor
                _ => RazorLanguageKind.Razor,
            };
        }
    }

    private bool TryMapToHostDocumentRangeStrict(IRazorGeneratedDocument generatedDocument, Range generatedDocumentRange, [NotNullWhen(returnValue: true)] out Range? hostDocumentRange)
    {
        hostDocumentRange = default;

        var csharpSourceText = GetGeneratedSourceText(generatedDocument);
        var range = generatedDocumentRange;
        if (!IsRangeWithinDocument(range, csharpSourceText))
        {
            return false;
        }

        if (!range.Start.TryGetAbsoluteIndex(csharpSourceText, _logger, out var startIndex) ||
            !TryMapToHostDocumentPosition(generatedDocument, startIndex, out var hostDocumentStart, out _))
        {
            return false;
        }

        if (!range.End.TryGetAbsoluteIndex(csharpSourceText, _logger, out var endIndex) ||
            !TryMapToHostDocumentPosition(generatedDocument, endIndex, out var hostDocumentEnd, out _))
        {
            return false;
        }

        hostDocumentRange = new Range
        {
            Start = hostDocumentStart,
            End = hostDocumentEnd
        };

        return true;
    }

    private bool TryMapToHostDocumentRangeInclusive(IRazorGeneratedDocument generatedDocument, Range generatedDocumentRange, [NotNullWhen(returnValue: true)] out Range? hostDocumentRange)
    {
        if (generatedDocument.CodeDocument is not { } codeDocument)
        {
            throw new InvalidOperationException("Cannot use document mapping service on a generated document that has a null CodeDocument.");
        }

        hostDocumentRange = default;

        var csharpSourceText = GetGeneratedSourceText(generatedDocument);

        if (!IsRangeWithinDocument(generatedDocumentRange, csharpSourceText))
        {
            return false;
        }

        var generatedRangeAsSpan = generatedDocumentRange.AsTextSpan(csharpSourceText);
        var range = generatedDocumentRange;
        var startIndex = generatedRangeAsSpan.Start;
        var startMappedDirectly = TryMapToHostDocumentPosition(generatedDocument, startIndex, out var hostDocumentStart, out _);

        var endIndex = generatedRangeAsSpan.End;
        var endMappedDirectly = TryMapToHostDocumentPosition(generatedDocument, endIndex, out var hostDocumentEnd, out _);

        if (startMappedDirectly && endMappedDirectly)
        {
            // We strictly mapped the start/end of the generated range.
            hostDocumentRange = new Range
            {
                Start = hostDocumentStart!,
                End = hostDocumentEnd!
            };
            return true;
        }

        List<SourceMapping> candidateMappings;
        if (startMappedDirectly)
        {
            // Start of generated range intersects with a mapping
            candidateMappings = generatedDocument.SourceMappings.Where(mapping => IntersectsWith(startIndex, mapping.GeneratedSpan)).ToList();
        }
        else if (endMappedDirectly)
        {
            // End of generated range intersects with a mapping
            candidateMappings = generatedDocument.SourceMappings.Where(mapping => IntersectsWith(endIndex, mapping.GeneratedSpan)).ToList();
        }
        else
        {
            // Our range does not intersect with any mapping; we should see if it overlaps generated locations
            candidateMappings = generatedDocument.SourceMappings.Where(mapping => Overlaps(generatedRangeAsSpan, mapping.GeneratedSpan)).ToList();
        }

        if (candidateMappings.Count == 1)
        {
            // We're intersecting or overlapping a single mapping, lets choose that.

            var mapping = candidateMappings[0];
            hostDocumentRange = ConvertMapping(codeDocument.Source, mapping);
            return true;
        }
        else
        {
            // More then 1 or exactly 0 intersecting/overlapping mappings
            return false;
        }

        bool Overlaps(TextSpan generatedRangeAsSpan, SourceSpan span)
        {
            var overlapStart = Math.Max(generatedRangeAsSpan.Start, span.AbsoluteIndex);
            var overlapEnd = Math.Min(generatedRangeAsSpan.End, span.AbsoluteIndex + span.Length);

            return overlapStart < overlapEnd;
        }

        bool IntersectsWith(int position, SourceSpan span)
        {
            return unchecked((uint)(position - span.AbsoluteIndex) <= (uint)span.Length);
        }

        static Range ConvertMapping(RazorSourceDocument sourceDocument, SourceMapping mapping)
        {
            var startLocation = sourceDocument.Lines.GetLocation(mapping.OriginalSpan.AbsoluteIndex);
            var endLocation = sourceDocument.Lines.GetLocation(mapping.OriginalSpan.AbsoluteIndex + mapping.OriginalSpan.Length);
            var convertedRange = new Range
            {
                Start = new Position(startLocation.LineIndex, startLocation.CharacterIndex),
                End = new Position(endLocation.LineIndex, endLocation.CharacterIndex)
            };
            return convertedRange;
        }
    }

    private bool TryMapToHostDocumentRangeInferred(IRazorGeneratedDocument generatedDocument, Range generatedDocumentRange, [NotNullWhen(returnValue: true)] out Range? hostDocumentRange)
    {
        if (generatedDocument.CodeDocument is not { } codeDocument)
        {
            throw new InvalidOperationException("Cannot use document mapping service on a generated document that has a null CodeDocument.");
        }

        // Inferred mapping behavior is a superset of inclusive mapping behavior so if the range is "inclusive" lets use that mapping.
        if (TryMapToHostDocumentRangeInclusive(generatedDocument, generatedDocumentRange, out hostDocumentRange))
        {
            return true;
        }

        // Doesn't map so lets try and infer some mappings

        hostDocumentRange = default;
        var csharpSourceText = GetGeneratedSourceText(generatedDocument);
        var generatedRangeAsSpan = generatedDocumentRange.AsTextSpan(csharpSourceText);
        SourceMapping? mappingBeforeGeneratedRange = null;
        SourceMapping? mappingAfterGeneratedRange = null;

        for (var i = generatedDocument.SourceMappings.Count - 1; i >= 0; i--)
        {
            var sourceMapping = generatedDocument.SourceMappings[i];
            var sourceMappingEnd = sourceMapping.GeneratedSpan.AbsoluteIndex + sourceMapping.GeneratedSpan.Length;
            if (generatedRangeAsSpan.Start >= sourceMappingEnd)
            {
                // This is the source mapping that's before us!
                mappingBeforeGeneratedRange = sourceMapping;

                if (i + 1 < generatedDocument.SourceMappings.Count)
                {
                    // We're not at the end of the document there's another source mapping after us
                    mappingAfterGeneratedRange = generatedDocument.SourceMappings[i + 1];
                }

                break;
            }
        }

        if (mappingBeforeGeneratedRange == null)
        {
            // Could not find a mapping before
            return false;
        }

        var sourceDocument = codeDocument.Source;
        var originalSpanBeforeGeneratedRange = mappingBeforeGeneratedRange.OriginalSpan;
        var originalEndBeforeGeneratedRange = originalSpanBeforeGeneratedRange.AbsoluteIndex + originalSpanBeforeGeneratedRange.Length;
        var originalEndPositionBeforeGeneratedRange = sourceDocument.Lines.GetLocation(originalEndBeforeGeneratedRange);
        var inferredStartPosition = new Position(originalEndPositionBeforeGeneratedRange.LineIndex, originalEndPositionBeforeGeneratedRange.CharacterIndex);

        if (mappingAfterGeneratedRange != null)
        {
            // There's a mapping after the "generated range" lets use its start position as our inferred end position.

            var originalSpanAfterGeneratedRange = mappingAfterGeneratedRange.OriginalSpan;
            var originalStartPositionAfterGeneratedRange = sourceDocument.Lines.GetLocation(originalSpanAfterGeneratedRange.AbsoluteIndex);
            var inferredEndPosition = new Position(originalStartPositionAfterGeneratedRange.LineIndex, originalStartPositionAfterGeneratedRange.CharacterIndex);

            hostDocumentRange = new Range()
            {
                Start = inferredStartPosition,
                End = inferredEndPosition,
            };
            return true;
        }

        // There was no projection after the "generated range". Therefore, lets fallback to the end-document location.

        Debug.Assert(sourceDocument.Length > 0, "Source document length should be greater than 0 here because there's a mapping before us");

        var endOfDocumentLocation = sourceDocument.Lines.GetLocation(sourceDocument.Length);
        var endOfDocumentPosition = new Position(endOfDocumentLocation.LineIndex, endOfDocumentLocation.CharacterIndex);

        hostDocumentRange = new Range()
        {
            Start = inferredStartPosition,
            End = endOfDocumentPosition,
        };
        return true;
    }

    private static bool s_haveAsserted = false;

    private bool IsRangeWithinDocument(Range range, SourceText sourceText)
    {
        // This might happen when the document that ranges were created against was not the same as the document we're consulting.
        var result = IsPositionWithinDocument(range.Start, sourceText) && IsPositionWithinDocument(range.End, sourceText);

        if (!s_haveAsserted && !result)
        {
            s_haveAsserted = true;
            var sourceTextLinesCount = sourceText.Lines.Count;
            _logger.LogWarning("Attempted to map a range {range} outside of the Source (line count {sourceTextLinesCount}.) This could happen if the Roslyn and Razor LSP servers are not in sync.", range, sourceTextLinesCount);
        }

        return result;

        static bool IsPositionWithinDocument(Position position, SourceText sourceText)
        {
            return position.Line < sourceText.Lines.Count;
        }
    }

    private async Task<TextDocumentEdit[]> RemapVersionedDocumentEditsAsync(TextDocumentEdit[] documentEdits, CancellationToken cancellationToken)
    {
        var remappedDocumentEdits = new List<TextDocumentEdit>();
        foreach (var entry in documentEdits)
        {
            var generatedDocumentUri = entry.TextDocument.Uri;

            // Check if the edit is actually for a generated document, because if not we don't need to do anything
            if (!_languageServerFeatureOptions.IsVirtualDocumentUri(generatedDocumentUri))
            {
                // This location doesn't point to a background razor file. No need to remap.
                remappedDocumentEdits.Add(entry);
                continue;
            }

            var razorDocumentUri = _languageServerFeatureOptions.GetRazorDocumentUri(generatedDocumentUri);
            var documentContext = await _documentContextFactory.TryCreateForOpenDocumentAsync(razorDocumentUri, entry.TextDocument.GetProjectContext(), cancellationToken).ConfigureAwait(false);
            if (documentContext is null)
            {
                continue;
            }

            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

            var remappedEdits = RemapTextEditsCore(generatedDocumentUri, codeDocument, entry.Edits);
            if (remappedEdits is null || remappedEdits.Length == 0)
            {
                // Nothing to do.
                continue;
            }

            remappedDocumentEdits.Add(new TextDocumentEdit()
            {
                TextDocument = new OptionalVersionedTextDocumentIdentifier()
                {
                    Uri = razorDocumentUri,
                    Version = documentContext.Version
                },
                Edits = remappedEdits
            });
        }

        return remappedDocumentEdits.ToArray();
    }

    private async Task<Dictionary<string, TextEdit[]>> RemapDocumentEditsAsync(Dictionary<string, TextEdit[]> changes, CancellationToken cancellationToken)
    {
        var remappedChanges = new Dictionary<string, TextEdit[]>();
        foreach (var entry in changes)
        {
            var uri = new Uri(entry.Key);
            var edits = entry.Value;

            // Check if the edit is actually for a generated document, because if not we don't need to do anything
            if (!_languageServerFeatureOptions.IsVirtualDocumentUri(uri))
            {
                remappedChanges[entry.Key] = entry.Value;
                continue;
            }

            var documentContext = await _documentContextFactory.TryCreateAsync(uri, cancellationToken).ConfigureAwait(false);
            if (documentContext is null)
            {
                continue;
            }

            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
            var remappedEdits = RemapTextEditsCore(uri, codeDocument, edits);
            if (remappedEdits is null || remappedEdits.Length == 0)
            {
                // Nothing to do.
                continue;
            }

            var razorDocumentUri = _languageServerFeatureOptions.GetRazorDocumentUri(uri);
            remappedChanges[razorDocumentUri.AbsoluteUri] = remappedEdits;
        }

        return remappedChanges;
    }

    private TextEdit[] RemapTextEditsCore(Uri generatedDocumentUri, RazorCodeDocument codeDocument, TextEdit[] edits)
    {
        var generatedDocument = GetGeneratedDocumentFromGeneratedDocumentUri(generatedDocumentUri, codeDocument);
        if (generatedDocument is null)
        {
            return edits;
        }

        var remappedEdits = new List<TextEdit>();
        for (var i = 0; i < edits.Length; i++)
        {
            var generatedRange = edits[i].Range;
            if (!TryMapToHostDocumentRange(generatedDocument, generatedRange, MappingBehavior.Strict, out var originalRange))
            {
                // Can't map range. Discard this edit.
                continue;
            }

            var edit = new TextEdit()
            {
                Range = originalRange,
                NewText = edits[i].NewText
            };

            remappedEdits.Add(edit);
        }

        return remappedEdits.ToArray();
    }

    private IRazorGeneratedDocument? GetGeneratedDocumentFromGeneratedDocumentUri(Uri generatedDocumentUri, RazorCodeDocument codeDocument)
    {
        if (_languageServerFeatureOptions.IsVirtualCSharpFile(generatedDocumentUri))
        {
            return codeDocument.GetCSharpDocument();
        }
        else if (_languageServerFeatureOptions.IsVirtualHtmlFile(generatedDocumentUri))
        {
            return codeDocument.GetHtmlDocument();
        }
        else
        {
            return null;
        }
    }

    private static SourceText GetGeneratedSourceText(IRazorGeneratedDocument generatedDocument)
    {
        if (generatedDocument.CodeDocument is not { } codeDocument)
        {
            throw new InvalidOperationException("Cannot use document mapping service on a generated document that has a null CodeDocument.");
        }

        return codeDocument.GetGeneratedSourceText(generatedDocument);
    }

    private static ImmutableArray<ClassifiedSpanInternal> GetClassifiedSpans(RazorCodeDocument document)
    {
        // Since this service is called so often, we get a good performance improvement by caching these values
        // for this code document. If the document changes, as the user types, then the document instance will be
        // different, so we don't need to worry about invalidating the cache.
        if (!document.Items.TryGetValue(typeof(ClassifiedSpanInternal), out ImmutableArray<ClassifiedSpanInternal> classifiedSpans))
        {
            var syntaxTree = document.GetSyntaxTree();
            classifiedSpans = ClassifiedSpanVisitor.VisitRoot(syntaxTree);

            document.Items[typeof(ClassifiedSpanInternal)] = classifiedSpans;
        }

        return classifiedSpans;
    }

    private static ImmutableArray<TagHelperSpanInternal> GetTagHelperSpans(RazorCodeDocument document)
    {
        // Since this service is called so often, we get a good performance improvement by caching these values
        // for this code document. If the document changes, as the user types, then the document instance will be
        // different, so we don't need to worry about invalidating the cache.
        if (!document.Items.TryGetValue(typeof(TagHelperSpanInternal), out ImmutableArray<TagHelperSpanInternal> tagHelperSpans))
        {
            var syntaxTree = document.GetSyntaxTree();
            tagHelperSpans = TagHelperSpanVisitor.VisitRoot(syntaxTree);

            document.Items[typeof(TagHelperSpanInternal)] = tagHelperSpans;
        }

        return tagHelperSpans;
    }
}
