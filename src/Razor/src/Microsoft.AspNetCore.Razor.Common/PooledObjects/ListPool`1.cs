// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
/// A pool of <see cref="List{T}"/> instances.
/// </summary>
/// 
/// <remarks>
/// Instances originating from this pool are intended to be short-lived and are suitable
/// for temporary work. Do not return them as the results of methods or store them in fields.
/// </remarks>
internal static class ListPool<T>
{
    internal const int Threshold = 512;

    private static readonly Func<ObjectPool<List<T>>, List<T>> s_allocate = AllocateAndClear;
    private static readonly Action<ObjectPool<List<T>>, List<T>> s_release = ClearAndFree;

    public static ObjectPool<List<T>> DefaultPool { get; } = ObjectPool.Default<List<T>>();

    public static PooledObject<List<T>> GetPooledObject()
        => new(DefaultPool, s_allocate, s_release);

    private static List<T> AllocateAndClear(ObjectPool<List<T>> pool)
    {
        var list = pool.Allocate();
        list.Clear();

        return list;
    }

    private static void ClearAndFree(ObjectPool<List<T>> pool, List<T> list)
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
    }
}
