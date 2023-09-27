// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.AspNetCore.Razor.Utilities;

internal static class FilePathNormalizer
{
    public static string NormalizeDirectory(string? directoryFilePath)
    {
        var directoryFilePathSpan = directoryFilePath.AsSpanOrDefault();
        if (directoryFilePathSpan.IsEmpty)
        {
            return "/";
        }

        // Ensure that the array is at least 1 character larger, so that we can add
        // a trailing space after normalization if necessary.
        var arrayLength = directoryFilePathSpan.Length + 1;
        using var _ = ArrayPool<char>.Shared.GetPooledArray(arrayLength, out var array);
        var arraySpan = array.AsSpan(0, arrayLength);

        var (start, length) = NormalizeCore(directoryFilePathSpan, arraySpan);
        ReadOnlySpan<char> normalizedSpan = arraySpan.Slice(start, length);

        // Add a trailing slash if the normalized span doesn't end in one.
        if (normalizedSpan[^1] != '/')
        {
            arraySpan[start + length] = '/';
            normalizedSpan = arraySpan.Slice(start, length + 1);
        }

        if (directoryFilePathSpan.Equals(normalizedSpan, StringComparison.Ordinal))
        {
            return directoryFilePath.AssumeNotNull();
        }

        return CreateString(normalizedSpan);
    }

    public static string Normalize(string? filePath)
    {
        var filePathSpan = filePath.AsSpanOrDefault();
        if (filePathSpan.IsEmpty)
        {
            return "/";
        }

        // Rent a buffer for Normalize to write to.
        using var _ = ArrayPool<char>.Shared.GetPooledArray(filePathSpan.Length, out var array);
        var arraySpan = array.AsSpan(0, filePathSpan.Length);

        var (start, length) = NormalizeCore(filePathSpan, arraySpan);
        var normalizedSpan = arraySpan.Slice(start, length);

        // If we didn't change anything, just return the original string.
        if (filePathSpan.Equals(normalizedSpan, StringComparison.Ordinal))
        {
            return filePath.AssumeNotNull();
        }

        // Otherwise, create a new string from our normalized char buffer.
        return CreateString(normalizedSpan);
    }

    /// <summary>
    ///  Returns the directory portion of the given file path in normalized form.
    /// </summary>
    public static string GetNormalizedDirectoryName(string? filePath)
    {
        var filePathSpan = filePath.AsSpanOrDefault();
        if (filePathSpan.IsEmpty)
        {
            return "/";
        }

        using var _1 = ArrayPool<char>.Shared.GetPooledArray(filePathSpan.Length, out var array);
        var arraySpan = array.AsSpan(0, filePathSpan.Length);

        var (start, length) = NormalizeCore(filePathSpan, arraySpan);
        var normalizedSpan = arraySpan.Slice(start, length);

        var lastSlashIndex = normalizedSpan.LastIndexOf('/');

        var directoryNameSpan = lastSlashIndex >= 0
            ? normalizedSpan[..(lastSlashIndex + 1)] // Include trailiing slash
            : normalizedSpan;

        if (filePathSpan.Equals(directoryNameSpan, StringComparison.Ordinal))
        {
            return filePath.AssumeNotNull();
        }

        return CreateString(directoryNameSpan);
    }

    public static bool FilePathsEquivalent(string? filePath1, string? filePath2)
    {
        var filePathSpan1 = filePath1.AsSpanOrDefault();
        var filePathSpan2 = filePath2.AsSpanOrDefault();

        if (filePathSpan1.IsEmpty)
        {
            return filePathSpan2.IsEmpty;
        }
        else if (filePathSpan2.IsEmpty)
        {
            return false;
        }

        using var _1 = ArrayPool<char>.Shared.GetPooledArray(filePathSpan1.Length, out var array1);
        var arraySpan1 = array1.AsSpan(0, filePathSpan1.Length);
        var (start1, length1) = NormalizeCore(filePathSpan1, arraySpan1);
        ReadOnlySpan<char> normalizedSpan1 = arraySpan1.Slice(start1, length1);

        using var _2 = ArrayPool<char>.Shared.GetPooledArray(filePathSpan2.Length, out var array2);
        var arraySpan2 = array2.AsSpan(0, filePathSpan2.Length);
        var (start2, length2) = NormalizeCore(filePathSpan2, arraySpan2);
        ReadOnlySpan<char> normalizedSpan2 = arraySpan2.Slice(start2, length2);

        return normalizedSpan1.Equals(normalizedSpan2, FilePathComparison.Instance);
    }

    /// <summary>
    ///  Normalizes the given <paramref name="source"/> file path and writes the result in <paramref name="destination"/>.
    /// </summary>
    /// <param name="source">The span to normalize.</param>
    /// <param name="destination">The span to write to.</param>
    /// <returns>
    ///  Returns a tuple containing the start index and length of the normalized path within <paramref name="destination"/>.
    /// </returns>
    private static (int start, int length) NormalizeCore(ReadOnlySpan<char> source, Span<char> destination)
    {
        if (source.IsEmpty)
        {
            if (destination.Length < 1)
            {
                throw new ArgumentException("Destination length must be at least 1 if the source is empty.", nameof(destination));
            }

            destination[0] = '/';

            return (start: 0, length: 1);
        }

        if (destination.Length < source.Length)
        {
            throw new ArgumentException("Destination length must be greater or equal to the source length.", nameof(destination));
        }

        int charsWritten;

        // Note: We check for '%' characters before calling UrlDecoder.Decode to ensure that we *only*
        // decode when there are '%XX' entities. So, calling Normalize on a path and then calling Normalize
        // on the result will not call Decode twice.
        if (source.Contains("%".AsSpan(), StringComparison.Ordinal))
        {
            UrlDecoder.Decode(source, destination, out charsWritten);
        }
        else
        {
            source.CopyTo(destination);
            charsWritten = source.Length;
        }

        // Replace slashes in our normalized span.
        destination[..charsWritten].Replace('\\', '/');

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            destination is ['/', ..] &&
            destination is not ['/', '/', ..])
        {
            // We've been provided a path that probably looks something like /C:/path/to.
            // So, we adjust result to skip the leading '/'.
            return (start: 1, length: charsWritten - 1);
        }
        else
        {
            // Already a valid path like C:/path or //path
            return (start: 0, length: charsWritten);
        }
    }

    private static unsafe string CreateString(ReadOnlySpan<char> source)
    {
        if (source.IsEmpty)
        {
            return string.Empty;
        }

        fixed (char* ptr = source)
        {
            return new string(ptr, 0, source.Length);
        }
    }
}
