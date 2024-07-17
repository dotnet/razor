// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Microsoft.AspNetCore.Razor;

/// <summary>
///  Temporary builder that uses <see cref="ArrayPool{T}"/> to back a <see cref="Memory{T}"/>.
/// </summary>
internal ref struct MemoryBuilder<T>
{
    private Memory<T> _memory;
    private T[]? _arrayFromPool;
    private int _index;

    public MemoryBuilder(int initialCapacity = 1)
    {
        ArgHelper.ThrowIfNegativeOrZero(initialCapacity);
        _arrayFromPool = ArrayPool<T>.Shared.Rent(initialCapacity);
        _memory = _arrayFromPool;
    }

    public void Dispose()
    {
        var toReturn = _arrayFromPool;
        if (toReturn is not null)
        {
            _arrayFromPool = null;
            ArrayPool<T>.Shared.Return(toReturn);
        }
    }

    public readonly ReadOnlyMemory<T> AsMemory()
        => _memory[.._index];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(T item)
    {
        var index = _index;

        var memory = _memory;
        if ((uint)index < (uint)memory.Length)
        {
            memory.Span[index] = item;
            _index = index + 1;
        }
        else
        {
            AppendWithResize(item);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AppendWithResize(T item)
    {
        Debug.Assert(_index == _memory.Length);
        var index = _index;
        Grow(1);
        _memory.Span[index] = item;
        _index = index + 1;
    }

    private void Grow(int additionalCapacityRequired = 1)
    {
        const int ArrayMaxLength = 0x7FFFFFC7; // same as Array.MaxLength

        var nextCapacity = Math.Max(_memory.Length != 0 ? _memory.Length * 2 : 4, _memory.Length + additionalCapacityRequired);

        if ((uint)nextCapacity > ArrayMaxLength)
        {
            nextCapacity = Math.Max(Math.Max(_memory.Length + 1, ArrayMaxLength), _memory.Length);
        }

        var newArray = ArrayPool<T>.Shared.Rent(nextCapacity);
        _memory.Span.CopyTo(newArray);

        var toReturn = _arrayFromPool;
        _memory = newArray;
        _arrayFromPool = newArray;

        if (toReturn != null)
        {
            ArrayPool<T>.Shared.Return(toReturn);
        }
    }
}
