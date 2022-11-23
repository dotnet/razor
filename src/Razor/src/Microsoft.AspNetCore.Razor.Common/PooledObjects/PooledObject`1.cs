// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

// Copied from https://github/dotnet/roslyn

using System;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal struct PooledObject<T> : IDisposable
    where T : class
{
    private readonly ObjectPool<T> _pool;
    private readonly Action<ObjectPool<T>, T> _releaser;
    private T? _object;

    // Because of how this API is intended to be used, we don't want the consumption code to have
    // to deal with Object being a nullable reference type. Intead, the guarantee is that this is
    // non-null until this is disposed.
    public T Object => _object!;

    public PooledObject(ObjectPool<T> pool, Func<ObjectPool<T>, T> allocator, Action<ObjectPool<T>, T> releaser)
        : this()
    {
        _pool = pool;
        _object = allocator(pool);
        _releaser = releaser;
    }

    public void Dispose()
    {
        if (_object is { } obj)
        {
            _releaser(_pool, obj);
            _object = null;
        }
    }
}
