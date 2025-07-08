// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.Compiler.Language.Extensions;

internal static class SourceTextExtensions
{
    public static ImmutableArray<TextChange> GetTextChangesArray(this SourceText newText, SourceText oldText)
    {
        var list = newText.GetTextChanges(oldText);

        // Fast path for the common case. The base SourceText.GetTextChanges method returns an ImmutableArray
        if (list is ImmutableArray<TextChange> array)
        {
            return array;
        }

        return list.ToImmutableArray();
    }
}
