// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

public sealed partial class CodeWriter
{
    private sealed class IndentCache
    {
        private const int MaxCachedIndentSize = 80;

        // Caches for combination of common tab sizes (2, 4) and the _useTabs bool
        private static readonly IndentCache?[] s_caches = new IndentCache?[4];

        private readonly ReadOnlyMemory<char>?[] _indents;
        private readonly bool _useTabs;
        private readonly int _tabSize;

        private IndentCache(bool useTabs, int tabSize)
        {
            _useTabs = useTabs;
            _tabSize = tabSize;

            _indents = new ReadOnlyMemory<char>?[MaxCachedIndentSize + 1];
        }

        public static ReadOnlyMemory<char> GetIndentString(int size, bool useTabs, int tabSize)
        {
            var cacheIndex = tabSize switch
            {
                2 => useTabs ? 0 : 1,
                4 => useTabs ? 2 : 3,
                _ => -1,
            };

            // Only cache common tab sizes and small indents
            if (cacheIndex == -1 || size > MaxCachedIndentSize)
            {
                return CreateIndent(size, useTabs, tabSize);
            }

            if (s_caches[cacheIndex] is not IndentCache indentCache)
            {
                indentCache = new IndentCache(useTabs, tabSize);
                s_caches[cacheIndex] = indentCache;
            }

            return indentCache.GetOrCacheIndent(size);
        }

        private ReadOnlyMemory<char> GetOrCacheIndent(int size)
        {
            if (_indents[size] is not ReadOnlyMemory<char> cachedIndent)
            {
                cachedIndent = CreateIndent(size, _useTabs, _tabSize);
                _indents[size] = cachedIndent;
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
