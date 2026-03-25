// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        var mappedEdits = _documentMappingService.GetRazorDocumentEdits(csharpDocument, csharpEdits.SelectAsArray(static e => e.ToTextChange()));

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
