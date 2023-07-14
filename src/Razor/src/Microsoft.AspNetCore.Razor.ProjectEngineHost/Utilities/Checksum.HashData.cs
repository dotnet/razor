// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.AspNetCore.Razor.Utilities;

internal sealed partial class Checksum
{
    [StructLayout(LayoutKind.Explicit, Size = HashSize)]
    private readonly struct HashData(long data1, long data2, int data3) : IEquatable<HashData>
    {
        [FieldOffset(0)]
        private readonly long _data1 = data1;

        [FieldOffset(8)]
        private readonly long _data2 = data2;

        [FieldOffset(16)]
        private readonly int _data3 = data3;

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(_data1);
            writer.Write(_data2);
            writer.Write(_data3);
        }

        public override bool Equals(object? obj)
            => obj is HashData data &&
               Equals(data);

        public bool Equals(HashData other)
            => _data1 == other._data1 &&
               _data2 == other._data2 &&
               _data3 == other._data3;

        public override int GetHashCode()
        {
            // The checksum is already a hash. Just provide a 4-byte value as a well-distributed hash code.
            return (int)_data1;
        }

        public static bool operator ==(HashData left, HashData right)
            => left.Equals(right);

        public static bool operator !=(HashData left, HashData right)
            => !(left == right);
    }
}
