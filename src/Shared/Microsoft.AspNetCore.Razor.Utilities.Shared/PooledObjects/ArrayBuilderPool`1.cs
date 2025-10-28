// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
/// A pool of <see cref="ImmutableArray{T}.Builder"/> instances.
/// </summary>
/// 
/// <remarks>
/// Instances originating from this pool are intended to be short-lived and are suitable
/// for temporary work. Do not return them as the results of methods or store them in fields.
/// </remarks>
internal sealed partial class ArrayBuilderPool<T> : CustomObjectPool<ImmutableArray<T>.Builder>
{
    public static readonly ArrayBuilderPool<T> Default = Create();

    private ArrayBuilderPool(PooledObjectPolicy policy, int size)
        : base(policy, size)
    {
    }

    public static ArrayBuilderPool<T> Create(
        PooledObjectPolicy policy, int size = DefaultPool.DefaultPoolSize)
        => new(policy, size);

    public static ArrayBuilderPool<T> Create(int size = DefaultPool.DefaultPoolSize)
        => new(Policy.Default, size);

    public static PooledObject<ImmutableArray<T>.Builder> GetPooledObject()
        => Default.GetPooledObject();

    public static PooledObject<ImmutableArray<T>.Builder> GetPooledObject(out ImmutableArray<T>.Builder builder)
        => Default.GetPooledObject(out builder);
}
