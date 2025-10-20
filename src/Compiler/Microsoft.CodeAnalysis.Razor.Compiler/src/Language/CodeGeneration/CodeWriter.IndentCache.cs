// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

public sealed partial class CodeWriter
{
    private static class IndentCache
    {
        private static readonly ReadOnlyMemory<char> s_tabs = new string('\t', 64).AsMemory();
        private static readonly ReadOnlyMemory<char> s_spaces = new string(' ', 128).AsMemory();

        public static ReadOnlyMemory<char> GetIndentString(int size, bool useTabs, int tabSize)
        {
            if (!useTabs)
            {
                return SliceOrCreate(size, s_spaces);
            }

            var tabCount = size / tabSize;
            var spaceCount = size % tabSize;

            if (spaceCount == 0)
            {
                return SliceOrCreate(tabCount, s_tabs);
            }

            return string.Create(length: tabCount + spaceCount, (tabCount, spaceCount), static (destination, state) =>
            {
                var (tabCount, spaceCount) = state;

                s_tabs.Span[..tabCount].CopyTo(destination);
                s_spaces.Span[..spaceCount].CopyTo(destination[tabCount..]);
            }).AsMemory();
        }

        private static ReadOnlyMemory<char> SliceOrCreate(int length, ReadOnlyMemory<char> chars)
        {
            return (length <= chars.Length)
                ? chars[..length]
                : new string(chars.Span[0], length).AsMemory();
        }
    }
}
