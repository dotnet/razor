﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class ItemCollection : ICollection<KeyValuePair<object, object>>, ICollection
{
    private readonly ConcurrentDictionary<object, object> _inner;

    public ItemCollection()
    {
        _inner = new ConcurrentDictionary<object, object>();
    }

    public object this[object key]
    {
        get
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            _inner.TryGetValue(key, out var value);
            return value;
        }
        set
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            _inner[key] = value;
        }
    }

    public int Count => _inner.Count;

    public bool IsReadOnly => _inner != null;

    bool ICollection.IsSynchronized => ((ICollection)_inner).IsSynchronized;

    object ICollection.SyncRoot => ((ICollection)_inner).SyncRoot;

    public void Add(KeyValuePair<object, object> item)
    {
        if (item.Key == null)
        {
            throw new ArgumentException(Resources.KeyMustNotBeNull, nameof(item));
        }

        ((ICollection<KeyValuePair<object, object>>)_inner).Add(item);
    }

    public void Add(object key, object value)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        ((IDictionary<object, object>)_inner).Add(key, value);
    }

    public void Clear()
    {
        _inner.Clear();
    }

    public bool Contains(KeyValuePair<object, object> item)
    {
        if (item.Key == null)
        {
            throw new ArgumentException(Resources.KeyMustNotBeNull, nameof(item));
        }

        return ((ICollection<KeyValuePair<object, object>>)_inner).Contains(item);
    }

    public void CopyTo(KeyValuePair<object, object>[] array, int arrayIndex)
    {
        if (array == null)
        {
            throw new ArgumentNullException(nameof(array));
        }

        if (arrayIndex < 0 || arrayIndex > array.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }
        else if (array.Length - arrayIndex < Count)
        {
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        }

        ((ICollection<KeyValuePair<object, object>>)_inner).CopyTo(array, arrayIndex);
    }

    public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
    {
        return _inner.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public bool Remove(KeyValuePair<object, object> item)
    {
        if (item.Key == null)
        {
            throw new ArgumentException(Resources.KeyMustNotBeNull, nameof(item));
        }

        return ((ICollection<KeyValuePair<object, object>>)_inner).Remove(item);
    }

    void ICollection.CopyTo(Array array, int index)
    {
        ((ICollection)_inner).CopyTo(array, index);
    }

    internal bool TryGetValue<TKey, TValue>(TKey key, [MaybeNullWhen(false)] out TValue value)
        where TKey : notnull
    {
        if (!_inner.TryGetValue(key, out var objValue))
        {
            value = default;
            return false;
        }

        value = (TValue)objValue;
        return true;
    }
}
