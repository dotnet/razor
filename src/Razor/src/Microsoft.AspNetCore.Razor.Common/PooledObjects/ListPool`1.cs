// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

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
    public static readonly ObjectPool<List<T>> DefaultPool = ObjectPool.Default<List<T>>();

    public static PooledObject<List<T>> GetPooledObject()
        => DefaultPool.GetPooledObject();

    public static PooledObject<List<T>> GetPooledObject(out List<T> list)
        => DefaultPool.GetPooledObject(out list);
}
