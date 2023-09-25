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
        var destination = array.AsSpan(0, filePathSpan.Length);

        Normalize(filePathSpan, destination, out var offset, out var charsWritten);
        destination = destination.Slice(offset, charsWritten);

        // If we didn't change anything, just return the original string.
        if (filePathSpan.Equals(destination, StringComparison.Ordinal))
        {
            return filePath.AssumeNotNull();
        }

        // Otherwise, create a new string from our normalized char buffer.
        unsafe
        {
            fixed (char* buffer = destination)
            {
                return new string(buffer, 0, destination.Length);
            }
        }
    }

    /// <summary>
    ///  Normalizes the given <paramref name="source"/> file path and writes the result in <paramref name="destination"/>.
    /// </summary>
    /// <param name="source">The span to normalize.</param>
    /// <param name="destination">The span to write to.</param>
    /// <param name="offset">The offset in <paramref name="destination"/> that was written to.</param>
    /// <param name="charsWritten">The number of characters written to <paramref name="destination"/>.</param>
    private static void Normalize(ReadOnlySpan<char> source, Span<char> destination, out int offset, out int charsWritten)
    {
        offset = 0;
        charsWritten = 0;

        if (source.IsEmpty)
        {
            if (destination.Length < 1)
            {
                throw new ArgumentException("Destination length must be at least 1 if the source is empty.", nameof(destination));
            }

            destination[0] = '/';
            charsWritten = 1;
            return;
        }

        if (destination.Length < source.Length)
        {
            throw new ArgumentException("Destination length must be greater or equal to the source length.", nameof(destination));
        }

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

        // Ensure that we only replace slashes in the range that was written to.
        destination[..charsWritten].Replace('\\', '/');

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            destination is ['/', ..] &&
            destination is not ['/', '/', ..])
        {
            // We've been provided a path that probably looks something like /C:/path/to.
            // So, we adjust offset and charsWritten to inform callers to ignore the first '/'.
            offset++;
            charsWritten--;
        }
        else
        {
            // Already a valid path like C:/path or //path
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

    public static bool FilePathsEquivalent(string filePath1, string filePath2)
    {
        var normalizedFilePath1 = Normalize(filePath1);
        var normalizedFilePath2 = Normalize(filePath2);

        return FilePathComparer.Instance.Equals(normalizedFilePath1, normalizedFilePath2);
    }
}
