// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Razor.Utilities;

internal static class StreamExtensions
{
    public static Task WriteStringAsync(this Stream stream, string text, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        Debug.Assert(stream.CanWrite);
        encoding ??= Encoding.UTF8;

        var encodedBytes = encoding.GetBytes(text);

        WriteSize(stream, encodedBytes.Length);
        return stream.WriteAsync(encodedBytes, 0, encodedBytes.Length, cancellationToken);
    }

    public static async Task<string> ReadStringAsync(this Stream stream, Encoding? encoding = null, CancellationToken cancellationToken = default)
    {
        Debug.Assert(stream.CanRead);
        encoding ??= Encoding.UTF8;

        var length = ReadSize(stream);
        var encodedBytes = new byte[length];
        await stream.ReadAsync(encodedBytes, 0, encodedBytes.Length, cancellationToken).ConfigureAwait(false);
        return encoding.GetString(encodedBytes);
    }

    private static void WriteSize(Stream stream, int length)
    {
        var bytes = GetSizeBytes(length);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static int ReadSize(Stream stream)
    {
        var bytes = new byte[4];
        stream.Read(bytes, 0, bytes.Length);

        return bytes.Sum(b => (int)b);
    }

    private static byte[] GetSizeBytes(int length)
        => BitConverter.GetBytes(length);
}
