// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Razor.Compiler.Language.Extensions;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.TextDifferencing;

internal static partial class SourceTextDiffer
{
    public static ImmutableArray<TextChange> GetMinimalTextChanges(SourceText oldText, SourceText newText, DiffKind kind = DiffKind.Line)
    {
        if (oldText.ContentEquals(newText))
        {
            return [];
        }
        else if (oldText.Length == 0 || newText.Length == 0)
        {
            return newText.GetTextChangesArray(oldText);
        }

        using TextDiffer differ = kind == DiffKind.Line
            ? new SourceTextDiffer.LineDiffer(oldText, newText)
            : new SourceTextDiffer.CharDiffer(oldText, newText);

        var changes = differ.GetMinimalTextChanges();

        Debug.Assert(oldText.WithChanges(changes).ContentEquals(newText), "Incorrect minimal changes");

        return changes;
    }
}
