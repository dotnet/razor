// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal static partial class PooledObject
{
    private static class ListFuncs<T>
    {
        public static Func<ObjectPool<List<T>>, List<T>> Allocate = pool =>
        {
            var list = pool.Allocate();
            list.Clear();

            return list;
        };

        public static Action<ObjectPool<List<T>>, List<T>> Release = (pool, list) =>
        {
            if (list is null)
            {
                return;
            }

            var count = list.Count;
            list.Clear();

            if (count > Threshold)
            {
                list.TrimExcess();
            }

            pool.Free(list);
        };
    }
}
