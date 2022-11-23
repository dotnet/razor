// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Text;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
/// A pool of <see cref="StringBuilder"/> instances.
/// </summary>
/// 
/// <remarks>
/// Instances originating from this pool are intended to be short-lived and are suitable
/// for temporary work. Do not return them as the results of methods or store them in fields.
/// </remarks>
internal static class StringBuilderPool
{
    internal const int Threshold = 512;

    private static readonly Func<ObjectPool<StringBuilder>, StringBuilder> s_allocate = AllocateAndClear;
    private static readonly Action<ObjectPool<StringBuilder>, StringBuilder> s_release = ClearAndFree;

    public static ObjectPool<StringBuilder> DefaultPool { get; } = ObjectPool.Default<StringBuilder>();

    public static PooledObject<StringBuilder> GetPooledObject()
        => new(DefaultPool, s_allocate, s_release);

    private static StringBuilder AllocateAndClear(ObjectPool<StringBuilder> pool)
    {
        var builder = pool.Allocate();
        builder.Clear();

        return builder;
    }

    private static void ClearAndFree(ObjectPool<StringBuilder> pool, StringBuilder builder)
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
