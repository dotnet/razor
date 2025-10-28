// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
/// A pool of <see cref="Stopwatch"/> instances.
/// </summary>
/// 
/// <remarks>
/// Instances originating from this pool are intended to be short-lived and are suitable
/// for temporary work. Do not return them as the results of methods or store them in fields.
/// </remarks>
internal sealed partial class StopwatchPool : DefaultObjectPool<Stopwatch>
{
    public static readonly StopwatchPool Default = Create();

    private StopwatchPool(IPooledObjectPolicy<Stopwatch> policy, int size)
        : base(policy, size)
    {
    }

    public static StopwatchPool Create(IPooledObjectPolicy<Stopwatch> policy, int size = DefaultPool.DefaultPoolSize)
        => new(policy, size);

    public static StopwatchPool Create(int size = DefaultPool.DefaultPoolSize)
        => new(Policy.Instance, size);

    public static PooledObject<Stopwatch> GetPooledObject()
        => Default.GetPooledObject();

    public static PooledObject<Stopwatch> GetPooledObject(out Stopwatch watch)
        => Default.GetPooledObject(out watch);
}
