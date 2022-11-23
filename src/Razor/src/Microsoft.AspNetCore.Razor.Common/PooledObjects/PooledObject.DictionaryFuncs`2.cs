// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal static partial class PooledObject
{
    private static class DictionaryFuncs<TKey, TValue>
        where TKey : notnull
    {
        public static Func<ObjectPool<Dictionary<TKey, TValue>>, Dictionary<TKey, TValue>> Allocate = pool =>
        {
            var map = pool.Allocate();
            map.Clear();

            return map;
        };

        public static Action<ObjectPool<Dictionary<TKey, TValue>>, Dictionary<TKey, TValue>> Release = (pool, map) =>
        {
            if (map is null)
            {
                return;
            }

            // If this map grew too large, don't put it back in the the pool.
            if (map.Count > Threshold)
            {
                return;
            }

            map.Clear();
            pool.Free(map);
        };
    }
}
