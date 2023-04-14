// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal static partial class StringHashSetPool
{
    private class Policy : IPooledObjectPolicy<HashSet<string>>
    {
        private readonly IEqualityComparer<string>? _comparer;

        public Policy(IEqualityComparer<string>? comparer = null)
        {
            _comparer = comparer;
        }

        public HashSet<string> Create() => new(_comparer);

        public bool Return(HashSet<string> set)
        {
            var count = set.Count;

            set.Clear();

            if (count > DefaultPool.MaximumObjectSize)
            {
                set.TrimExcess();
            }

            return true;
        }
    }
}
