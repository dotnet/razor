// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Utilities;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
/// A pool of <see cref="HashSet{T}"/> instances that compares items using reference equality.
/// </summary>
/// 
/// <remarks>
/// Instances originating from this pool are intended to be short-lived and are suitable
/// for temporary work. Do not return them as the results of methods or store them in fields.
/// </remarks>
internal static class ReferenceEqualityHashSetPool<T>
    where T : class
{
    public static readonly ObjectPool<HashSet<T>> DefaultPool =
        ObjectPool.Default(() => new HashSet<T>(ReferenceEqualityComparer<T>.Instance));

    public static PooledObject<HashSet<T>> GetPooledObject() => DefaultPool.GetPooledObject();

    public static PooledObject<HashSet<T>> GetPooledObject(out HashSet<T> set)
        => DefaultPool.GetPooledObject(out set);
}
