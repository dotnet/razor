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

    /// <summary>
    ///  Returns a value indicating whether a specified character occurs within a string instance.
    /// </summary>
    /// <param name="text">
    ///  The string instance.
    /// </param>
    /// <param name="value">
    ///  The character to seek.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if the value parameter occurs within the string; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  This method exists on .NET Core, but doesn't on .NET Framework or .NET Standard 2.0.
    /// </remarks>
    public static bool Contains(this string text, char value)
    {
#if NET
        return text.Contains(value);
#else
        return text.IndexOf(value) >= 0;
#endif
    }

    /// <summary>
    ///  Returns a value indicating whether a specified character occurs within a string instance,
    ///  using the specified comparison rules.
    /// </summary>
    /// <param name="text">
    ///  The string instance.
    /// </param>
    /// <param name="value">
    ///  The character to seek.
    /// </param>
    /// <param name="comparisonType">
    ///  One of the enumeration values that specifies the rules to use in the comparison.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if the value parameter occurs within the string; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  This method exists on .NET Core, but doesn't on .NET Framework or .NET Standard 2.0.
    /// </remarks>
    public static bool Contains(this string text, char value, StringComparison comparisonType)
    {
#if NET
        return text.Contains(value, comparisonType);
#else
        return text.IndexOf(value, comparisonType) != 0;
#endif
    }

    /// <summary>
    ///  Reports the zero-based index of the first occurrence of the specified Unicode character in a string instance.
    ///  A parameter specifies the type of search to use for the specified character.
    /// </summary>
    /// <param name="text">
    ///  The string instance.
    /// </param>
    /// <param name="value">
    ///  The character to compare to the character at the start of this string.
    /// </param>
    /// <param name="comparisonType">
    ///  An enumeration value that specifies the rules for the search.
    /// </param>
    /// <returns>
    ///  The zero-based index of <paramref name="value"/> if that character is found, or -1 if it is not.
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   Index numbering starts from zero.
    ///  </para>
    ///  <para>
    ///   The <paramref name="comparisonType"/> parameter is a <see cref="StringComparison"/> enumeration member
    ///   that specifies whether the search for the <paramref name="value"/> argument uses the current or invariant culture,
    ///   is case-sensitive or case-insensitive, or uses word or ordinal comparison rules.
    ///  </para>
    ///  <para>
    ///   This method exists on .NET Core, but doesn't on .NET Framework or .NET Standard 2.0.
    ///  </para>
    /// </remarks>
    public static int IndexOf(this string text, char value, StringComparison comparisonType)
    {
#if NET
        return text.IndexOf(value, comparisonType);
#else
        // [ch] produces a ReadOnlySpan<char> using a ref to ch.
        return text.AsSpan().IndexOf([value], comparisonType);
#endif
    }

    /// <summary>
    ///  Determines whether a string instance starts with the specified character.
    /// </summary>
    /// <param name="text">
    ///  The string instance.
    /// </param>
    /// <param name="value">
    ///  The character to compare to the character at the start of this string.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="value"/> matches the start of the string;
    ///  otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   This method performs an ordinal (case-sensitive and culture-insensitive) comparison.
    ///  </para>
    ///  <para>
    ///   This method exists on .NET Core, but doesn't on .NET Framework or .NET Standard 2.0.
    ///  </para>
    /// </remarks>
    public static bool StartsWith(this string text, char value)
    {
#if NET
        return text.StartsWith(value);
#else
        return text.Length > 0 && text[0] == value;
#endif
    }

    /// <summary>
    ///  Determines whether the end of a string instance matches the specified character.
    /// </summary>
    /// <param name="text">
    ///  The string instance.
    /// </param>
    /// <param name="value">
    ///  The character to compare to the character at the end of this string.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="value"/> matches the end of this string;
    ///  otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   This method performs an ordinal (case-sensitive and culture-insensitive) comparison.
    ///  </para>
    ///  <para>
    ///   This method exists on .NET Core, but doesn't on .NET Framework or .NET Standard 2.0.
    ///  </para>
    /// </remarks>
    public static bool EndsWith(this string text, char value)
    {
#if NET
        return text.EndsWith(value);
#else
        return text.Length > 0 && text[^1] == value;
#endif
    }
}
