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
        var normalized = Normalize(directoryFilePath);

        if (!normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized += '/';
        }

        return normalized;
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
        var destinationSpan = array.AsSpan(0, filePathSpan.Length);

        var normalizedSpan = Normalize(filePathSpan, destinationSpan);

        // If we didn't change anything, just return the original string.
        if (filePathSpan.Equals(normalizedSpan, StringComparison.Ordinal))
        {
            return filePath.AssumeNotNull();
        }

        // Otherwise, create a new string from our normalized char buffer.
        unsafe
        {
            fixed (char* buffer = normalizedSpan)
            {
                return new string(buffer, 0, normalizedSpan.Length);
            }
        }
    }

    /// <summary>
    ///  Normalizes the given <paramref name="source"/> file path and writes the result in <paramref name="destination"/>.
    /// </summary>
    /// <param name="source">The span to normalize.</param>
    /// <param name="destination">The span to write to.</param>
    /// <returns>
    ///  Returns the normalized span within <paramref name="destination"/>.
    /// </returns>
    private static ReadOnlySpan<char> Normalize(ReadOnlySpan<char> source, Span<char> destination)
    {
        if (source.IsEmpty)
        {
            if (destination.Length < 1)
            {
                throw new ArgumentException("Destination length must be at least 1 if the source is empty.", nameof(destination));
            }

            destination[0] = '/';

            return destination[..1];
        }

        if (destination.Length < source.Length)
        {
            throw new ArgumentException("Destination length must be greater or equal to the source length.", nameof(destination));
        }

        Span<char> normalizedSpan;

        // Note: We check for '%' characters before calling UrlDecoder.Decode to ensure that we *only*
        // decode when there are '%XX' entities. So, calling Normalize on a path and then calling Normalize
        // on the result will not call Decode twice.
        if (source.Contains("%".AsSpan(), StringComparison.Ordinal))
        {
            UrlDecoder.Decode(source, destination, out var charsWritten);
            normalizedSpan = destination[..charsWritten];
        }
        else
        {
            source.CopyTo(destination);
            normalizedSpan = destination[..source.Length];
        }

        // Replace slashes in our normalized span.
        normalizedSpan.Replace('\\', '/');

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            normalizedSpan is ['/', ..] &&
            normalizedSpan is not ['/', '/', ..])
        {
            // We've been provided a path that probably looks something like /C:/path/to.
            // So, we adjust resulting span to skip the leading '/'.
            return normalizedSpan[1..];
        }
        else
        {
            // Already a valid path like C:/path or //path
            return normalizedSpan;
        }
    }

    public static string GetDirectory(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new InvalidOperationException(filePath);
        }

        var normalizedPath = Normalize(filePath);
        var lastSeparatorIndex = normalizedPath.LastIndexOf('/');

        var directory = normalizedPath[..(lastSeparatorIndex + 1)];
        return directory;
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
        using var _2 = ArrayPool<char>.Shared.GetPooledArray(filePathSpan2.Length, out var array2);

        var normalizedSpan1 = Normalize(filePathSpan1, array1.AsSpan(0, filePathSpan1.Length));
        var normalizedSpan2 = Normalize(filePathSpan2, array2.AsSpan(0, filePathSpan2.Length));

        return normalizedSpan1.Equals(normalizedSpan2, FilePathComparison.Instance);
    }
}
