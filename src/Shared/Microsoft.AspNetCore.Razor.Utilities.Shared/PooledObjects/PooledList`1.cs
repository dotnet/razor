// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
///  Wraps a pooled <see cref="List{T}"/> but doesn't allocate it until
///  it's needed. Note: Dispose this to ensure that the pooled set is returned
///  to the pool.
/// </summary>
internal ref struct PooledList<T>
{
    private readonly ObjectPool<List<T>> _pool;
    private List<T>? _set;

    public PooledList()
        : this(ListPool<T>.Default)
    {
    }

    public PooledList(ObjectPool<List<T>> pool)
    {
        _pool = pool;
    }

    public void Dispose()
    {
        ClearAndFree();
    }

    public int Count
        => _set?.Count ?? 0;

    public void Add(T item)
    {
        _set ??= _pool.Get();
        _set.Add(item);
    }

    public void AddRange(IReadOnlyList<T> list)
    {
        if (list.Count == 0)
        {
            return;
        }

        _set ??= _pool.Get();
        _set.AddRange(list);
    }

    public void ClearAndFree()
    {
        if (_set is { } set)
        {
            _pool.Return(set);
            _set = null;
        }
    }

    public readonly T[] ToArray()
        => _set?.ToArray() ?? Array.Empty<T>();
}
