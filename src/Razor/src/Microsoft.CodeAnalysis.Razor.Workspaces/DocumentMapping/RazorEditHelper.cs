// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.DocumentMapping;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.DocumentMapping;

internal static partial class RazorEditHelper
{
    /// <summary>
    /// Maps the given text edits for a razor file based on changes in csharp. It special
    /// cases usings directives to insure they are added correctly. All other edits
    /// are applied if they map to the razor document.
    /// </summary>
    /// <returns></returns>
    internal static async Task<ImmutableArray<TextEdit>> MapCSharpEditsAsync(
        ImmutableArray<TextEdit> textEdits,
        IDocumentSnapshot snapshot,
        IDocumentMappingService documentMappingService,
        ITelemetryReporter telemetryReporter,
        CancellationToken cancellationToken)
    {

        var codeDocument = await snapshot.GetGeneratedOutputAsync(cancellationToken).ConfigureAwait(false);
        using var textChangeBuilder = new TextChangeBuilder(documentMappingService);

        var originalSyntaxTree = await snapshot.GetCSharpSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        var csharpSourceText = await originalSyntaxTree.GetTextAsync(cancellationToken).ConfigureAwait(false);

        var textChanges = textEdits.SelectAsArray(e => csharpSourceText.GetTextChange(e));
        var newText = csharpSourceText.WithChanges(textChanges);
        var newSyntaxTree = originalSyntaxTree.WithChangedText(newText);

        textChangeBuilder.AddDirectlyMappedEdits(textEdits, codeDocument, cancellationToken);

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
    private static ImmutableArray<TextEdit> NormalizeEdits(ImmutableArray<TextEdit> changes, ITelemetryReporter telemetryReporter, CancellationToken cancellationToken)
    {
        // Ensure that the changes are sorted by start position otherwise
        // the normalization logic will not work.
        Debug.Assert(changes.SequenceEqual(changes.OrderBy(static c => c.Range, RangeComparer.Instance)));

        using var normalizedEdits = new PooledArrayBuilder<TextEdit>(changes.Length);
        var remaining = changes.AsSpan();

        var droppedEdits = 0;
        while (remaining is not [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (remaining is [var edit, var nextEdit, ..])
            {
                if (edit.Range == nextEdit.Range)
                {
                    normalizedEdits.Add(nextEdit);
                    remaining = remaining[1..];

                    if (edit.NewText != nextEdit.NewText)
                    {
                        droppedEdits++;
                    }
                }
                else if (edit.Range.Contains(nextEdit.Range))
                {
                    // Cases where there was a removal and addition on the same
                    // line err to taking the addition. This can happen in the
                    // case of a namespace rename
                    if (edit.Range.Start.Line == nextEdit.Range.Start.Line)
                    {
                        if (string.IsNullOrEmpty(edit.NewText) && !string.IsNullOrEmpty(nextEdit.NewText))
                        {
                            // Don't count this as a dropped edit, it is expected
                            // in the case of a rename
                            normalizedEdits.Add(new TextEdit()
                            {
                                Range = edit.Range,
                                NewText = nextEdit.NewText
                            });
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
                else if (nextEdit.Range.Contains(edit.Range))
                {
                    // Add the edit that is contained in the other edit
                    // and skip the next edit.
                    normalizedEdits.Add(nextEdit);
                    remaining = remaining[1..];
                    if (edit.NewText != nextEdit.NewText)
                    {
                        droppedEdits++;
                    }
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
            telemetryReporter.ReportFault(
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
