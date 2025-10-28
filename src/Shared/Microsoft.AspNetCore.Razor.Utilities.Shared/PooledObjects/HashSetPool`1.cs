// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
/// A pool of <see cref="HashSet{T}"/> instances that compares items using default equality.
/// </summary>
/// 
/// <remarks>
/// Instances originating from this pool are intended to be short-lived and are suitable
/// for temporary work. Do not return them as the results of methods or store them in fields.
/// </remarks>
internal sealed partial class HashSetPool<T> : CustomObjectPool<HashSet<T>>
{
    public static readonly HashSetPool<T> Default = Create();

    private HashSetPool(PooledObjectPolicy policy, int size)
        : base(policy, size)
    {
    }

    public static HashSetPool<T> Create(IEqualityComparer<T> comparer, int size = DefaultPool.DefaultPoolSize)
        => new(new Policy(comparer), size);

    public static HashSetPool<T> Create(PooledObjectPolicy policy, int size = DefaultPool.DefaultPoolSize)
        => new(policy, size);

    public static HashSetPool<T> Create(int size = DefaultPool.DefaultPoolSize)
        => new(Policy.Default, size);

    public static PooledObject<HashSet<T>> GetPooledObject()
        => Default.GetPooledObject();

    public static PooledObject<HashSet<T>> GetPooledObject(out HashSet<T> set)
        => Default.GetPooledObject(out set);
}
