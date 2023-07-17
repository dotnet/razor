// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Utilities;

internal sealed partial class Checksum
{
    internal readonly ref struct Builder
    {
        private enum TypeKind : byte
        {
            Null,
            Array,
            String,
            Checksum,

            Bool,
            Int32,
            Int64,
        }

        // Per-thread array to use as a buffer for appending primitive values to the hash.
        [ThreadStatic]
        private static readonly byte[] g_buffer = new byte[8];

        private readonly IncrementalHash _hash;

        public Builder()
        {
            _hash = s_incrementalHashPool.Get();
        }

        public Checksum FreeAndGetChecksum()
        {
            var result = From(_hash.GetHashAndReset());
            s_incrementalHashPool.Return(_hash);
            return result;
        }

        private static void AppendTypeKind(IncrementalHash hash, TypeKind kind)
        {
            g_buffer[0] = (byte)kind;
            hash.AppendData(g_buffer, offset: 0, count: 1);
        }

        private static void AppendBoolValue(IncrementalHash hash, bool value)
        {
            g_buffer[0] = (byte)(value ? 1 : 0);
            hash.AppendData(g_buffer, offset: 0, count: sizeof(bool));
        }

        private static void AppendInt32Value(IncrementalHash hash, int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(g_buffer.AsSpan(0, sizeof(int)), value);
            hash.AppendData(g_buffer, offset: 0, count: sizeof(int));
        }

        private static void AppendInt64Value(IncrementalHash hash, long value)
        {
            BinaryPrimitives.WriteInt64LittleEndian(g_buffer.AsSpan(0, sizeof(long)), value);
            hash.AppendData(g_buffer, offset: 0, count: sizeof(long));
        }

        private static void AppendStringValue(IncrementalHash hash, string value)
        {
            var stringBytes = MemoryMarshal.AsBytes(value.AsSpan());
            Debug.Assert(stringBytes.Length == value.Length * 2);

            using var pooledBuffer = s_byteArrayPool.GetPooledObject(out var buffer);

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

        private void AppendHashDataValue(IncrementalHash hash, HashData value)
        {
            AppendInt64Value(hash, value.Data1);
            AppendInt64Value(hash, value.Data2);
            AppendInt32Value(hash, value.Data3);
        }

        public void AppendData<T>(T[]? array)
        {
            if (array is null)
            {
                AppendTypeKind(_hash, TypeKind.Null);
                return;
            }

            AppendTypeKind(_hash, TypeKind.Array);
            AppendInt32Value(_hash, array.Length);

            if (typeof(T) == typeof(bool))
            {
                foreach (var item in (bool[])(Array)array)
                {
                    AppendData(item);
                }
            }
            else if (typeof(T) == typeof(int))
            {
                foreach (var item in (int[])(Array)array)
                {
                    AppendData(item);
                }
            }
            else if (typeof(T) == typeof(long))
            {
                foreach (var item in (long[])(Array)array)
                {
                    AppendData(item);
                }
            }
            else if (typeof(T) == typeof(string))
            {
                foreach (var item in (string?[])(Array)array)
                {
                    AppendData(item);
                }
            }
            else if (typeof(T) == typeof(Checksum))
            {
                foreach (var item in (Checksum[])(Array)array)
                {
                    AppendData(item);
                }
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public void AppendData(bool value)
        {
            AppendTypeKind(_hash, TypeKind.Bool);
            AppendBoolValue(_hash, value);
        }

        public void AppendData(int value)
        {
            AppendTypeKind(_hash, TypeKind.Int32);
            AppendInt32Value(_hash, value);
        }

        public void AppendData(long value)
        {
            AppendTypeKind(_hash, TypeKind.Int64);
            AppendInt64Value(_hash, value);
        }

        public void AppendData(string? value)
        {
            if (value is null)
            {
                AppendTypeKind(_hash, TypeKind.Null);
                return;
            }

            AppendTypeKind(_hash, TypeKind.String);
            AppendStringValue(_hash, value);
        }

        public void AppendData(Checksum value)
        {
            AppendTypeKind(_hash, TypeKind.Checksum);
            AppendHashDataValue(_hash, value._checksum);
        }
    }
}
