// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Mapping;

internal static partial class RazorEditHelper
{
    /// <summary>
    /// Maps the given text edits for a razor file based on changes in csharp. It special
    /// cases usings directives to insure they are added correctly. All other edits
    /// are applied if they map to the razor document.
    /// </summary>
    /// <returns></returns>
    internal static async Task<ImmutableArray<TextChange>> MapEditsAsync(
        ImmutableArray<TextChange> textEdits,
        IDocumentSnapshot snapshot,
        RazorCodeDocument codeDocument,
        IDocumentMappingService _documentMappingService,
        ITelemetryReporter? telemetryReporter,
        CancellationToken cancellationToken)
    {
        using var textChangeBuilder = new TextChangeBuilder(_documentMappingService);
        var originalSyntaxTree =  await snapshot.GetCSharpSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        var csharpSourceText = await originalSyntaxTree.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var newText = csharpSourceText.WithChanges(textEdits);
        var newSyntaxTree = originalSyntaxTree.WithChangedText(newText);

        textChangeBuilder.AddDirectlyMappedChanges(textEdits, codeDocument, cancellationToken);

        var oldUsings = await AddUsingsHelper.FindUsingDirectiveStringsAsync(
            originalSyntaxTree,
            cancellationToken).ConfigureAwait(false);

        var newUsings = await AddUsingsHelper.FindUsingDirectiveStringsAsync(
            newSyntaxTree,
            cancellationToken).ConfigureAwait(false);

        var addedUsings = Delta.Compute(oldUsings, newUsings);
        var removedUsings = Delta.Compute(newUsings, oldUsings);

        textChangeBuilder.AddUsingsChanges(codeDocument, addedUsings, removedUsings, cancellationToken);

        return NormalizeEdits(textChangeBuilder.DrainToOrderedImmutable(), telemetryReporter, cancellationToken);
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
    private static ImmutableArray<TextChange> NormalizeEdits(ImmutableArray<TextChange> changes, ITelemetryReporter? telemetryReporter, CancellationToken cancellationToken)
    {
        // Ensure that the changes are sorted by start position otherwise
        // the normalization logic will not work.
        Debug.Assert(changes.SequenceEqual(changes.OrderBy(static c => c.Span.Start)));

        using var normalizedEdits = new PooledArrayBuilder<TextChange>(changes.Length);
        var remaining = changes.AsSpan();

        var droppedEdits = 0;
        while (remaining is not [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (remaining is [var edit, var nextEdit, ..])
            {
                if (edit.Span == nextEdit.Span)
                {
                    normalizedEdits.Add(nextEdit);
                    remaining = remaining[1..];

                    if (edit.NewText != nextEdit.NewText)
                    {
                        droppedEdits++;
                    }
                }
                else if (edit.Span.Contains(nextEdit.Span))
                {
                    // Cases where there was a removal and addition on the same
                    // line err to taking the addition. This can happen in the
                    // case of a namespace rename
                    if (edit.Span.Start == nextEdit.Span.Start)
                    {
                        if (string.IsNullOrEmpty(edit.NewText) && !string.IsNullOrEmpty(nextEdit.NewText))
                        {
                            // Don't count this as a dropped edit, it is expected
                            // in the case of a rename
                            normalizedEdits.Add(new TextChange(edit.Span, nextEdit.NewText));
                            remaining = remaining[1..];
                        }
                        else
                        {
                            normalizedEdits.Add(edit);
                            remaining = remaining[1..];
                            droppedEdits++;
                        }
                    }
                    else
                    {
                        normalizedEdits.Add(edit);

                        remaining = remaining[1..];
                        droppedEdits++;
                    }
                }
                else if (nextEdit.Span.Contains(edit.Span))
                {
                    // Add the edit that is contained in the other edit
                    // and skip the next edit.
                    normalizedEdits.Add(nextEdit);
                    remaining = remaining[1..];
                    droppedEdits++;
                }
                else
                {
                    normalizedEdits.Add(edit);
                }
            }
            else
            {
                normalizedEdits.Add(remaining[0]);
            }

            remaining = remaining[1..];
        }

        if (droppedEdits > 0)
        {
            telemetryReporter?.ReportFault(
                new DroppedEditsException(),
                "Potentially dropped edits when trying to map",
                new Property("droppedEditCount", droppedEdits));
        }

        return normalizedEdits.ToImmutable();
    }

    private sealed class DroppedEditsException : Exception
    {
    }
}
