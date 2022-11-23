// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Utilities;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
/// A pool of <see cref="HashSet{T}"/> instances that compares items using reference equality.
/// </summary>
/// 
/// <remarks>
/// Instances originating from this pool are intended to be short-lived and are suitable
/// for temporary work. Do not return them as the results of methods or store them in fields.
/// </remarks>
internal static class ReferenceEqualityHashSetPool<T>
    where T : class
{
    private const int Threshold = 512;

    private static readonly Func<ObjectPool<HashSet<T>>, HashSet<T>> s_allocate = AllocateAndClear;
    private static readonly Action<ObjectPool<HashSet<T>>, HashSet<T>> s_release = ClearAndFree;

    public static ObjectPool<HashSet<T>> DefaultPool { get; } = ObjectPool.Default(() => new HashSet<T>(ReferenceEqualityComparer<T>.Instance));

    public static PooledObject<HashSet<T>> GetPooledObject()
        => new(DefaultPool, s_allocate, s_release);

    private static HashSet<T> AllocateAndClear(ObjectPool<HashSet<T>> pool)
    {
        var set = pool.Allocate();
        set.Clear();

        return set;
    }

    private static void ClearAndFree(ObjectPool<HashSet<T>> pool, HashSet<T> set)
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
    }
}
