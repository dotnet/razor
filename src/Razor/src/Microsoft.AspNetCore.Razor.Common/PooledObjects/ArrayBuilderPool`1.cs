// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
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
internal static class ArrayBuilderPool<T>
{
    private const int Threshold = 512;

    private static readonly Func<ObjectPool<ImmutableArray<T>.Builder>, ImmutableArray<T>.Builder> s_allocate = AllocateAndClear;
    private static readonly Action<ObjectPool<ImmutableArray<T>.Builder>, ImmutableArray<T>.Builder> s_release = ClearAndFree;

    public static ObjectPool<ImmutableArray<T>.Builder> DefaultPool { get; } = ObjectPool.Default(ImmutableArray.CreateBuilder<T>);

    public static PooledObject<ImmutableArray<T>.Builder> GetPooledObject()
        => new(DefaultPool, s_allocate, s_release);

    private static ImmutableArray<T>.Builder AllocateAndClear(ObjectPool<ImmutableArray<T>.Builder> pool)
    {
        var builder = pool.Allocate();
        builder.Clear();

        return builder;
    }

    private static void ClearAndFree(ObjectPool<ImmutableArray<T>.Builder> pool, ImmutableArray<T>.Builder builder)
    {
        if (builder is null)
        {
            return;
        }

        builder.Clear();

        if (builder.Capacity > Threshold)
        {
            builder.Capacity = Threshold;
        }

        pool.Free(builder);
    }
}
