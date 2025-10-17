// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;

#if NET9_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace Microsoft.AspNetCore.Razor.Language;

internal static class ChecksumUtilities
{
    public static string BytesToString(ImmutableArray<byte> bytes)
    {
        if (bytes.IsDefault)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

#if NET9_0_OR_GREATER
        var bytesArray = ImmutableCollectionsMarshal.AsArray(bytes)!;

        return Convert.ToHexStringLower(bytesArray);
#else
        const int StackAllocThreshold = 256; // reasonable for stackalloc
        var charCount = bytes.Length * 2;

        // As this should be getting called with a Checksum array of length 32, this shouldn't allocate
        var buffer = charCount <= StackAllocThreshold
            ? stackalloc char[charCount]
            : new char[charCount];

        var bufferIndex = 0;
        foreach (var b in bytes)
        {
            // Write hex chars directly
            buffer[bufferIndex++] = GetHexChar(b >> 4);
            buffer[bufferIndex++] = GetHexChar(b & 0xF);
        }

        // Allocate the final string
        return buffer.ToString();

        static char GetHexChar(int value)
            => (char)(value < 10 ? '0' + value : 'a' + (value - 10));
#endif
    }
}
