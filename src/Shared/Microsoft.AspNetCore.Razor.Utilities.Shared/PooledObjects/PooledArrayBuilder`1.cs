// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
///  Wraps a pooled <see cref="ImmutableArray{T}.Builder"/> but doesn't allocate it until
///  it's needed. Note: Dispose this to ensure that the pooled array builder is returned
///  to the pool.
/// </summary>
internal ref struct PooledArrayBuilder<T>
{
    private readonly ObjectPool<ImmutableArray<T>.Builder> _pool;
    private ImmutableArray<T>.Builder? _builder;

    public PooledArrayBuilder()
        : this(ArrayBuilderPool<T>.Default)
    {
    }

    public PooledArrayBuilder(ObjectPool<ImmutableArray<T>.Builder> pool)
    {
        _pool = pool;
    }

    public void Dispose()
    {
        ClearAndFree();
    }

    public int Count
        => _builder?.Count ?? 0;

    public void Add(T item)
    {
        _builder ??= _pool.Get();
        _builder.Add(item);
    }

    public void AddRange(IReadOnlyList<T> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        _builder ??= _pool.Get();
        _builder.AddRange(items);
    }

    public void AddRange(IEnumerable<T> items)
    {
        _builder ??= _pool.Get();
        _builder.AddRange(items);
    }

    public void ClearAndFree()
    {
        if (_builder is { } builder)
        {
            _pool.Return(builder);
            _builder = null;
        }
    }

    public readonly ImmutableArray<T> ToImmutable()
        => _builder?.ToImmutable() ?? ImmutableArray<T>.Empty;

    public readonly T[] ToArray()
        => _builder?.ToArray() ?? Array.Empty<T>();
}
