// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal abstract class CustomObjectPool<T>(
    CustomObjectPool<T>.PooledObjectPolicy policy, int size) : DefaultObjectPool<T>(policy, size)
    where T : class
{
    public abstract class PooledObjectPolicy : IPooledObjectPolicy<T>
    {
        public abstract T Create();

        public abstract bool Return(T obj);
    }
}
