// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.Diagnostics;

internal static class UnusedDirectiveCache
{
    private static readonly ConditionalWeakTable<RazorCodeDocument, int[]> s_cache = new();

    public static void Set(RazorCodeDocument codeDocument, int[] lines)
    {
        s_cache.Remove(codeDocument);
        s_cache.Add(codeDocument, lines);
    }

    public static bool TryGet(RazorCodeDocument codeDocument, out int[] lines)
    {
        return s_cache.TryGetValue(codeDocument, out lines!);
    }
}
