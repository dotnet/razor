// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
/// A pool of <see cref="Stack{T}"/> instances.
/// </summary>
/// 
/// <remarks>
/// Instances originating from this pool are intended to be short-lived and are suitable
/// for temporary work. Do not return them as the results of methods or store them in fields.
/// </remarks>
internal static class StackPool<T>
{
    public static readonly ObjectPool<Stack<T>> DefaultPool = ObjectPool.Default<Stack<T>>();

    public static PooledObject<Stack<T>> GetPooledObject() => DefaultPool.GetPooledObject();

    public static PooledObject<Stack<T>> GetPooledObject(out Stack<T> stack)
        => DefaultPool.GetPooledObject(out stack);
}
