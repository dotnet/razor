// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Utilities;

internal sealed partial class Checksum
{
    private sealed class ByteArrayPoolPolicy : IPooledObjectPolicy<byte[]>
    {
        public static readonly ByteArrayPoolPolicy Instance = new();

        private ByteArrayPoolPolicy()
        {
        }

        public byte[] Create() => new byte[4 * 1024];
        public bool Return(byte[] obj) => true;
    }
}
