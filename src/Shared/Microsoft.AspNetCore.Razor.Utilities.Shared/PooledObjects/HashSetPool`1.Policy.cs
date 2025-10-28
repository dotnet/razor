// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal partial class HashSetPool<T>
{
    private sealed class Policy(IEqualityComparer<T>? comparer) : IPooledObjectPolicy<HashSet<T>>
    {
        public static readonly Policy Instance = new();

        private Policy()
            : this(comparer: null)
        {
        }

        public HashSet<T> Create() => new(comparer);

        public bool Return(HashSet<T> set)
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
