// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
/// A pool of <see cref="Stopwatch"/> instances.
/// </summary>
/// 
/// <remarks>
/// Instances originating from this pool are intended to be short-lived and are suitable
/// for temporary work. Do not return them as the results of methods or store them in fields.
/// </remarks>
internal static class StopwatchPool
{
    public static readonly ObjectPool<Stopwatch> DefaultPool = ObjectPool.Default<Stopwatch>();

    public static PooledObject<Stopwatch> GetPooledObject()
        => DefaultPool.GetPooledObject();

    public static PooledObject<Stopwatch> GetPooledObject(out Stopwatch watch)
        => DefaultPool.GetPooledObject(out watch);
}
