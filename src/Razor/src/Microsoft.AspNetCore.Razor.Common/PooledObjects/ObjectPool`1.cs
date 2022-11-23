// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

// Copied from https://github/dotnet/roslyn

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
/// Generic implementation of object pooling pattern with predefined pool size limit. The main
/// purpose is that limited number of frequently used objects can be kept in the pool for
/// further recycling.
/// </summary>
/// 
/// <remarks>
///  <para>Notes:</para>
/// 
///  <list type="number">
///   <item>
///   It is not the goal to keep all returned objects. The pool is not meant for storage.
///   If there is no space in the pool, extra returned objects will be dropped.
///   </item>
/// 
///   <item>
///   It is implied that if object was obtained from this pool, the caller will return it back in
///   a relatively short time. Keeping checked out objects for long durations is fine, but reduces
///   the usefulness of pooling. Just new up your own object.
///   </item>
///  </list>
///  
///  <para>
///  Not returning objects to the pool in not detrimental to the pool's work, but is a bad practice. 
///  Rationale: If there is no intent for reusing the object, do not use pool - just use "new".
///  </para>
/// </remarks>
internal class ObjectPool<T>
    where T : class
{
    [DebuggerDisplay("{Value,nq}")]
    private struct Element
    {
        public T? _value;
    }

    // Storage for the pool objects. The first item is stored in a dedicated field because we
    // expect to be able to satisfy most requests from it.
    private T? _firstItem;
    private readonly Element[] _items;

    private readonly Func<T> _factory;

    public ObjectPool(Func<T> factory)
        : this(factory, Environment.ProcessorCount * 2)
    {
    }

    public ObjectPool(Func<T> factory, int size)
    {
        _factory = factory;
        _items = new Element[size - 1];
    }

    public ObjectPool(Func<ObjectPool<T>, T> factory, int size)
    {
        _factory = () => factory(this);
        _items = new Element[size - 1];
    }

    private T CreateInstance() => _factory();

    public T Allocate()
    {
        // PERF: Examine the first element. If that fails, AllocateSlow will look at the remaining elements.
        // Note that the initial read is optimistically not synchronized. That is intentional.
        // We will interlock only when we have a candidate. in a worst case we may miss some
        // recently returned objects. Not a big deal.

        var item = _firstItem;
        if (item is null || item != Interlocked.CompareExchange(ref _firstItem, null, item))
        {
            item = AllocateSlow();
        }

        return item;
    }

    private T AllocateSlow()
    {
        var items = _items;

        for (var i = 0; i < items.Length; i++)
        {
            // Note that the initial read is optimistically not synchronized. That is intentional.
            // We will interlock only when we have a candidate. in a worst case we may miss some
            // recently returned objects. Not a big deal.

            var item = _items[i]._value;
            if (item is not null &&
                item == Interlocked.CompareExchange(ref items[i]._value, null, item))
            {
                return item;
            }
        }

        return CreateInstance();
    }

    public void Free(T obj)
    {
        if (_firstItem is null)
        {
            // Intentionally not using interlocked here.
            // In a worst case scenario two objects may be stored into same slot.
            // It is very unlikely to happen and will only mean that one of the objects will get collected.
            _firstItem = obj;
        }
        else
        {
            FreeSlow(obj);
        }
    }

    private void FreeSlow(T obj)
    {
        var items = _items;

        for (var i = 0; i < items.Length; i++)
        {
            if (items[i]._value is null)
            {
                // Intentionally not using interlocked here.
                // In a worst case scenario two objects may be stored into same slot.
                // It is very unlikely to happen and will only mean that one of the objects will get collected.
                items[i]._value = obj;
                break;
            }
        }
    }
}
