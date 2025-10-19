// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

public sealed partial class CodeWriter
{
    private static class IndentCache
    {
        private const int MaxCachedIndentSize = 80;

        // Caches for combination of useTabs and common tab sizes (2, 4)
        private static ReadOnlyMemory<char>?[]? s_tabsTwo;
        private static ReadOnlyMemory<char>?[]? s_tabsFour;
        private static ReadOnlyMemory<char>?[]? s_spacesTwo;
        private static ReadOnlyMemory<char>?[]? s_spacesFour;

        public static ReadOnlyMemory<char> GetIndentString(int size, bool useTabs, int tabSize)
        {
            // Don't initialize anything if we're not going to use it
            if (size > MaxCachedIndentSize)
            {
                return CreateIndent(size, useTabs, tabSize);
            }

            return (useTabs, tabSize) switch
            {
                (true, 2) => GetOrCacheIndent(ref s_tabsTwo, size, useTabs, tabSize),
                (true, 4) => GetOrCacheIndent(ref s_tabsFour, size, useTabs, tabSize),
                (false, 2) => GetOrCacheIndent(ref s_spacesTwo, size, useTabs, tabSize),
                (false, 4) => GetOrCacheIndent(ref s_spacesFour, size, useTabs, tabSize),
                _ => CreateIndent(size, useTabs, tabSize)
            };
        }

        private static ReadOnlyMemory<char> GetOrCacheIndent(ref ReadOnlyMemory<char>?[]? indents, int size, bool useTabs, int tabSize)
        {
            indents ??= new ReadOnlyMemory<char>?[MaxCachedIndentSize + 1];

            if (indents[size] is not ReadOnlyMemory<char> cachedIndent)
            {
                cachedIndent = CreateIndent(size, useTabs, tabSize);
                indents[size] = cachedIndent;
            }

            return cachedIndent;
        }

        private static ReadOnlyMemory<char> CreateIndent(int size, bool useTabs, int tabSize)
        {
            if (useTabs)
            {
                var tabCount = size / tabSize;
                var spaceCount = size % tabSize;

                return string.Create(tabCount + spaceCount, (tabCount, spaceCount), static (destination, state) =>
                {
                    var (tabCount, spaceCount) = state;
                    var index = 0;

                    for (var i = 0; i < tabCount; i++)
                    {
                        destination[index++] = '\t';
                    }

                    for (var i = 0; i < spaceCount; i++)
                    {
                        destination[index++] = ' ';
                    }
                }).AsMemory();
            }

            return new string(' ', size).AsMemory();
        }
    }
}
