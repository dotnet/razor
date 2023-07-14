// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Utilities;

internal partial class Checksum
{
    private static readonly ObjectPool<IncrementalHash> s_incrementalHashPool = DefaultPool.Create(IncrementalHashPoolPolicy.Instance);
    private static readonly ObjectPool<byte[]> s_byteArrayPool = DefaultPool.Create(ByteArrayPoolPolicy.Instance, size: 512);

    public static Checksum Create(string value)
    {
        using var pooledHash = s_incrementalHashPool.GetPooledObject(out var hash);
        using var pooledBuffer = s_byteArrayPool.GetPooledObject(out var buffer);

        AppendStringData(hash, buffer, value);

        return From(hash.GetHashAndReset());
    }

    public static Checksum Create(IEnumerable<string> values)
    {
        using var pooledHash = s_incrementalHashPool.GetPooledObject(out var hash);
        using var pooledBuffer = s_byteArrayPool.GetPooledObject(out var buffer);

        foreach (var value in values)
        {
            AppendStringData(hash, buffer, value);
            AppendStringData(hash, buffer, "\0");
        }

        return From(hash.GetHashAndReset());
    }

    private static void AppendStringData(IncrementalHash hash, byte[] buffer, string value)
    {
        var stringBytes = MemoryMarshal.AsBytes(value.AsSpan());
        Debug.Assert(stringBytes.Length == value.Length * 2);

        var index = 0;
        while (index < stringBytes.Length)
        {
            var remaining = stringBytes.Length - index;
            var toCopy = Math.Min(remaining, buffer.Length);
            stringBytes.Slice(index, toCopy).CopyTo(buffer);
            hash.AppendData(buffer, 0, toCopy);

            index += toCopy;
        }
    }

    public static Checksum Create(Stream stream)
    {
        using var pooledHash = s_incrementalHashPool.GetPooledObject(out var hash);
        using var pooledBuffer = s_byteArrayPool.GetPooledObject(out var buffer);

        var bufferLength = buffer.Length;
        int bytesRead;
        do
        {
            bytesRead = stream.Read(buffer, 0, bufferLength);
            if (bytesRead > 0)
            {
                hash.AppendData(buffer, 0, bytesRead);
            }
        }
        while (bytesRead > 0);

        // Note: The result of hash.GetHashAndReset() can be truncated if the byte array it returns
        // is larger than HashSize. However, the hash should still be functionally correct.
        return From(hash.GetHashAndReset());
    }

    public static Checksum Create(Checksum checksum1, Checksum checksum2)
    {
        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream))
        {
            checksum1.WriteTo(writer);
            checksum2.WriteTo(writer);
        }

        stream.Position = 0;

        return Create(stream);
    }

    public static Checksum Create(Checksum checksum1, Checksum checksum2, Checksum checksum3)
    {
        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream))
        {
            checksum1.WriteTo(writer);
            checksum2.WriteTo(writer);
            checksum3.WriteTo(writer);
        }

        stream.Position = 0;

        return Create(stream);
    }

    public static Checksum Create(IEnumerable<Checksum> checksums)
    {
        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream))
        {
            foreach (var checksum in checksums)
            {
                checksum.WriteTo(writer);
            }
        }

        stream.Position = 0;

        return Create(stream);
    }

    public static Checksum Create(ImmutableArray<Checksum> checksums)
    {
        using var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream))
        {
            foreach (var checksum in checksums)
            {
                checksum.WriteTo(writer);
            }
        }

        stream.Position = 0;

        return Create(stream);
    }
}
