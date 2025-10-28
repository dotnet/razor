// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal sealed partial class QueuePool<T> : CustomObjectPool<Queue<T>>
{
    public static readonly QueuePool<T> Default = Create();

    private QueuePool(PooledObjectPolicy policy, int size)
        : base(policy, size)
    {
    }

    public static QueuePool<T> Create(PooledObjectPolicy policy, int size = DefaultPool.DefaultPoolSize)
        => new(policy, size);

    public static QueuePool<T> Create(int size = DefaultPool.DefaultPoolSize)
        => new(Policy.Default, size);

    public static PooledObject<Queue<T>> GetPooledObject()
        => Default.GetPooledObject();

    public static PooledObject<Queue<T>> GetPooledObject(out Queue<T> queue)
        => Default.GetPooledObject(out queue);
}
