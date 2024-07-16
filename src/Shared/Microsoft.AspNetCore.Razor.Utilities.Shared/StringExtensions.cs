// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;

#if !NET
using ThrowHelper = Microsoft.AspNetCore.Razor.Utilities.ThrowHelper;
#endif

namespace System;

internal static class StringExtensions
{
    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? s)
        => string.IsNullOrEmpty(s);

    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? s)
        => string.IsNullOrWhiteSpace(s);

    public static ReadOnlySpan<char> AsSpan(this string? s, Index startIndex)
    {
#if NET
        return MemoryExtensions.AsSpan(s, startIndex);
#else
        if (s is null)
        {
            if (!startIndex.Equals(Index.Start))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(startIndex));
            }

            return default;
        }

        return s.AsSpan(startIndex.GetOffset(s.Length));
#endif
    }

    public static ReadOnlySpan<char> AsSpan(this string? s, Range range)
    {
#if NET
        return MemoryExtensions.AsSpan(s, range);
#else
        if (s is null)
        {
            if (!range.Start.Equals(Index.Start) || !range.End.Equals(Index.Start))
            {
                ThrowHelper.ThrowArgumentNull(nameof(s));
            }

            return default;
        }

        var (start, length) = range.GetOffsetAndLength(s.Length);
        return s.AsSpan(start, length);
#endif
    }

    public static ReadOnlySpan<char> AsSpanOrDefault(this string? s)
        => s is not null ? s.AsSpan() : default;

    public static ReadOnlySpan<char> AsSpanOrDefault(this string? s, int start)
        => s is not null ? s.AsSpan(start) : default;

    public static ReadOnlySpan<char> AsSpanOrDefault(this string? s, int start, int length)
        => s is not null ? s.AsSpan(start, length) : default;

    public static ReadOnlySpan<char> AsSpanOrDefault(this string? s, Index startIndex)
    {
        if (s is null)
        {
            return default;
        }

#if NET
        return MemoryExtensions.AsSpan(s, startIndex);
#else
        return s.AsSpan(startIndex.GetOffset(s.Length));
#endif
    }

    public static ReadOnlySpan<char> AsSpanOrDefault(this string? s, Range range)
    {
        if (s is null)
        {
            return default;
        }

#if NET
        return MemoryExtensions.AsSpan(s, range);
#else
        var (start, length) = range.GetOffsetAndLength(s.Length);
        return s.AsSpan(start, length);
#endif
    }

    public static ReadOnlyMemory<char> AsMemory(this string? s, Index startIndex)
    {
#if NET
        return MemoryExtensions.AsMemory(s, startIndex);
#else
        if (s is null)
        {
            if (!startIndex.Equals(Index.Start))
            {
                ThrowHelper.ThrowArgumentOutOfRange(nameof(startIndex));
            }

            return default;
        }

        return s.AsMemory(startIndex.GetOffset(s.Length));
#endif
    }

    public static ReadOnlyMemory<char> AsMemory(this string? s, Range range)
    {
#if NET
        return MemoryExtensions.AsMemory(s, range);
#else
        if (s is null)
        {
            if (!range.Start.Equals(Index.Start) || !range.End.Equals(Index.Start))
            {
                ThrowHelper.ThrowArgumentNull(nameof(s));
            }

            return default;
        }

        var (start, length) = range.GetOffsetAndLength(s.Length);
        return s.AsMemory(start, length);
#endif
    }

    public static ReadOnlyMemory<char> AsMemoryOrDefault(this string? s)
        => s is not null ? s.AsMemory() : default;

    public static ReadOnlyMemory<char> AsMemoryOrDefault(this string? s, int start)
        => s is not null ? s.AsMemory(start) : default;

    public static ReadOnlyMemory<char> AsMemoryOrDefault(this string? s, int start, int length)
        => s is not null ? s.AsMemory(start, length) : default;

    public static ReadOnlyMemory<char> AsMemoryOrDefault(this string? s, Index startIndex)
    {
        if (s is null)
        {
            return default;
        }

#if NET
        return MemoryExtensions.AsMemory(s, startIndex);
#else
        return s.AsMemory(startIndex.GetOffset(s.Length));
#endif
    }

    public static ReadOnlyMemory<char> AsMemoryOrDefault(this string? s, Range range)
    {
        if (s is null)
        {
            return default;
        }

#if NET
        return MemoryExtensions.AsMemory(s, range);
#else
        var (start, length) = range.GetOffsetAndLength(s.Length);
        return s.AsMemory(start, length);
#endif
    }

    // This method doesn't exist on .NET Framework, but it does on .NET Core.
    public static bool Contains(this string s, char ch)
        => s.IndexOf(ch) >= 0;

    // This method doesn't exist on .NET Framework, but it does on .NET Core.
    public static bool EndsWith(this string s, char ch)
        => s.Length > 0 && s[^1] == ch;
}
