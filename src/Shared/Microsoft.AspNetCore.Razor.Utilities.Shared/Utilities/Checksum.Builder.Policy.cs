// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.Extensions.ObjectPool;
#if NET
using System.Diagnostics;
#endif
using System.Security.Cryptography;

namespace Microsoft.AspNetCore.Razor.Utilities;

internal sealed partial record Checksum
{
    internal readonly ref partial struct Builder
    {

#if NET
        private sealed class Policy : IPooledObjectPolicy<IncrementalHash>
        {
            public static readonly Policy Instance = new();

            private Policy()
            {
            }

            public IncrementalHash Create()
                => IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            public bool Return(IncrementalHash hash)
            {
                Debug.Assert(hash.AlgorithmName == HashAlgorithmName.SHA256);

                return true;
            }
        }
#else
        private sealed class Policy : IPooledObjectPolicy<SHA256>
        {
            public static readonly Policy Instance = new();

            private Policy()
            {
            }

            public SHA256 Create()
                => SHA256.Create();

            public bool Return(SHA256 hash)
            {
                return true;
            }
        }
#endif
    }
}
