// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.AspNetCore.Razor.Utilities;

internal sealed partial class Checksum : IEquatable<Checksum?>
{
    private const int HashSize = 20;

    public static readonly Checksum Null = new(default);

    private readonly HashData _checksum;

    private Checksum(HashData hash)
        => _checksum = hash;

    public static Checksum From(ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
        {
            return Null;
        }

        if (source.Length < HashSize)
        {
            throw new ArgumentException($"{nameof(source)} must be equal to or larger than the hash size: {HashSize}", nameof(source));
        }

        if (!MemoryMarshal.TryRead(source, out HashData hash))
        {
            throw new InvalidOperationException("Could not read hash data");
        }

        return new Checksum(hash);
    }

    private void WriteTo(BinaryWriter writer)
        => _checksum.WriteTo(writer);

    public string ToBase64String()
    {
#if NETCOREAPP
        Span<byte> bytes = stackalloc byte[HashSize];
        MemoryMarshal.TryWrite(bytes, ref Unsafe.AsRef(in _checksum));

        return Convert.ToBase64String(bytes);
#else
        unsafe
        {
            var data = new byte[HashSize];
            fixed (byte* dataPtr = data)
            {
                *(HashData*)dataPtr = _checksum;
            }

            return Convert.ToBase64String(data, offset: 0, length: HashSize);
        }
#endif
    }

    public static Checksum FromBase64String(string value)
        => value is null
            ? Null
            : From(Convert.FromBase64String(value));

    public override bool Equals(object? obj)
        => Equals(obj as Checksum);

    public bool Equals(Checksum? other)
        => other is not null &&
           _checksum == other._checksum;

    public override int GetHashCode()
        => _checksum.GetHashCode();

    public override string ToString()
        => ToBase64String();

    public static bool operator ==(Checksum? left, Checksum? right)
        => EqualityComparer<Checksum?>.Default.Equals(left, right);

    public static bool operator !=(Checksum? left, Checksum? right)
        => !(left == right);
}
