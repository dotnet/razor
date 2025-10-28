// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
internal sealed partial class ListPool<T> : CustomObjectPool<List<T>>
{
    public static readonly ListPool<T> Default = Create();

    private ListPool(PooledObjectPolicy policy, int size)
        : base(policy, size)
    {
    }

    public static ListPool<T> Create(PooledObjectPolicy policy, int size = DefaultPool.DefaultPoolSize)
        => new(policy, size);

    public static ListPool<T> Create(int size = DefaultPool.DefaultPoolSize)
        => new(Policy.Default, size);

    public static PooledObject<List<T>> GetPooledObject()
        => Default.GetPooledObject();

    public static PooledObject<List<T>> GetPooledObject(out List<T> list)
        => Default.GetPooledObject(out list);
}
