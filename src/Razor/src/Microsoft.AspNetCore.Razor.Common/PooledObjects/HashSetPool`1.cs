// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

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
internal static class HashSetPool<T>
{
    public static readonly ObjectPool<HashSet<T>> DefaultPool = ObjectPool.Default<HashSet<T>>();

    public static PooledObject<HashSet<T>> GetPooledObject()
        => DefaultPool.GetPooledObject();

    public static PooledObject<HashSet<T>> GetPooledObject(out HashSet<T> set)
        => DefaultPool.GetPooledObject(out set);
}
