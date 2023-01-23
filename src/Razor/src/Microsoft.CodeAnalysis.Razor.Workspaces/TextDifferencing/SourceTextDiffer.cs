// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.TextDifferencing;

internal abstract partial class SourceTextDiffer : TextDiffer
{
    protected readonly SourceText OldText;
    protected readonly SourceText NewText;

    protected SourceTextDiffer(SourceText oldText, SourceText newText)
    {
        OldText = oldText ?? throw new ArgumentNullException(nameof(oldText));
        NewText = newText ?? throw new ArgumentNullException(nameof(newText));
    }

    protected abstract int GetEditPosition(DiffEdit edit);
    protected abstract int AppendEdit(DiffEdit edit, StringBuilder builder);

    private IReadOnlyList<TextChange> ConsolidateEdits(IReadOnlyList<DiffEdit> edits)
    {
        // Scan through the list of edits and collapse them into a minimal set of TextChanges.
        // This method assumes that there are no overlapping changes and the changes are sorted.

        var minimalChanges = new List<TextChange>();

        var start = 0;
        var end = 0;

        using var _ = StringBuilderPool.GetPooledObject(out var builder);

        foreach (var edit in edits)
        {
            var startPosition = GetEditPosition(edit);
            if (startPosition != end)
            {
                // Previous edit's end doesn't match the new edit's start.
                // Output the text change we were tracking.
                if (start != end || builder.Length > 0)
                {
                    minimalChanges.Add(new TextChange(TextSpan.FromBounds(start, end), builder.ToString()));
                    builder.Clear();
                }

                start = startPosition;
            }

            end = AppendEdit(edit, builder);
        }

        if (start != end || builder.Length > 0)
        {
            minimalChanges.Add(new TextChange(TextSpan.FromBounds(start, end), builder.ToString()));
        }

        return minimalChanges;
    }

    public static IReadOnlyList<TextChange> GetMinimalTextChanges(SourceText oldText, SourceText newText, bool lineDiffOnly = true)
    {
        if (oldText is null)
        {
            throw new ArgumentNullException(nameof(oldText));
        }

        if (newText is null)
        {
            throw new ArgumentNullException(nameof(newText));
        }

        if (oldText.ContentEquals(newText))
        {
            return Array.Empty<TextChange>();
        }
        else if (oldText.Length == 0 || newText.Length == 0)
        {
            return newText.GetTextChanges(oldText);
        }

        SourceTextDiffer differ = lineDiffOnly
            ? new LineDiffer(oldText, newText)
            : new CharDiffer(oldText, newText);

        var edits = differ.ComputeDiff();

        var changes = differ.ConsolidateEdits(edits);

        Debug.Assert(oldText.WithChanges(changes).ContentEquals(newText), "Incorrect minimal changes");

        return changes;
    }
}
