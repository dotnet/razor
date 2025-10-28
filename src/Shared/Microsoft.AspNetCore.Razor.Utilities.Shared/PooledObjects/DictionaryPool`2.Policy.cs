// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal partial class DictionaryPool<TKey, TValue>
    where TKey : notnull
{
    private sealed class Policy(IEqualityComparer<TKey>? comparer) : PooledObjectPolicy
    {
        public static readonly Policy Instance = new();

        private Policy()
            : this(comparer: null)
        {
        }

        public override Dictionary<TKey, TValue> Create() => new(comparer);

        public override bool Return(Dictionary<TKey, TValue> map)
        {
            var count = map.Count;

            map.Clear();

            // If the map grew too large, don't return it to the pool.
            return count <= DefaultPool.MaximumObjectSize;
        }
    }
}
