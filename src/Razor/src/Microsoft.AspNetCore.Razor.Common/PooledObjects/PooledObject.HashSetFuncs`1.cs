// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal static partial class PooledObject
{
    private static class HashSetFuncs<T>
    {
        public static Func<ObjectPool<HashSet<T>>, HashSet<T>> Allocate = pool =>
        {
            var set = pool.Allocate();
            set.Clear();

            return set;
        };

        public static Action<ObjectPool<HashSet<T>>, HashSet<T>> Release = (pool, set) =>
        {
            if (set is null)
            {
                return;
            }

            var count = set.Count;
            set.Clear();

            if (count > Threshold)
            {
                set.TrimExcess();
            }

            pool.Free(set);
        };
    }
}
