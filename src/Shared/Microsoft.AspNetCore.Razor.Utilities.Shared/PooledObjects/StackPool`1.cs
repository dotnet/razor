// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
/// A pool of <see cref="Stack{T}"/> instances.
/// </summary>
/// 
/// <remarks>
/// Instances originating from this pool are intended to be short-lived and are suitable
/// for temporary work. Do not return them as the results of methods or store them in fields.
/// </remarks>
internal sealed partial class StackPool<T> : DefaultObjectPool<Stack<T>>
{
    public static readonly StackPool<T> Default = Create();

    private StackPool(IPooledObjectPolicy<Stack<T>> policy, int size)
        : base(policy, size)
    {
    }

    public static StackPool<T> Create(IPooledObjectPolicy<Stack<T>> policy, int size = DefaultPool.DefaultPoolSize)
        => new(policy, size);

    public static StackPool<T> Create(int size = DefaultPool.DefaultPoolSize)
        => new(Policy.Instance, size);

    public static PooledObject<Stack<T>> GetPooledObject()
        => Default.GetPooledObject();

    public static PooledObject<Stack<T>> GetPooledObject(out Stack<T> stack)
        => Default.GetPooledObject(out stack);
}
