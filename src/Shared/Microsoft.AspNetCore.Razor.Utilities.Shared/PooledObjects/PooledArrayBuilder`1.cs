// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Razor.Utilities;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
///  Wraps a pooled array but doesn't allocate it until it's needed. Provides
///  access to the backing array as a <see cref="Span{T}"/>.
///  Note: Disposal ensures the pooled array is returned to the pool.
/// </summary>
[NonCopyable]
[CollectionBuilder(typeof(PooledSpanBuilder), nameof(PooledSpanBuilder.Create))]
internal partial struct PooledSpanBuilder<T>(int capacity) : IDisposable
{
    public static PooledSpanBuilder<T> Empty => new();

    /// <summary>
    ///  An array to be used as storage.
    /// </summary>
    private T[] _array = capacity > 0 ? ArrayPool<T>.Shared.Rent(capacity) : [];

    /// <summary>
    /// Number of items in this collection.
    /// </summary>
    private int _count = 0;

    public PooledSpanBuilder()
        : this(capacity: 0)
    {
    }

    public void Dispose()
    {
        // Return _array to the pool if necessary.
        if (_array.Length > 0)
        {
            ArrayPool<T>.Shared.Return(_array, clearArray: true);
            _array = [];
            _count = 0;
        }
    }

    /// <summary>
    ///  Ensures the inner <see cref="_array"/>'s capacity is at least the specified value.
    /// </summary>
    /// <remarks>
    ///  This should only be used by methods that will add to the inner <see cref="_array"/>.
    /// </remarks>
    private void EnsureCapacity(int capacity)
    {
        if (_array.Length < capacity)
        {
            var newArray = ArrayPool<T>.Shared.Rent(capacity);
            Array.Copy(_array, 0, newArray, 0, Count);
            ArrayPool<T>.Shared.Return(_array);
            _array = newArray;
        }
    }

    public readonly T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return _array[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _array[index] = value;
        }
    }

    public T this[Index index]
    {
        readonly get => this[index.GetOffset(Count)];
        set => this[index.GetOffset(Count)] = value;
    }

    public readonly int Count
        => _count;

    public readonly int Capacity
        => _array.Length;

    public void Add(T item)
        => Insert(Count, item);

    public void AddRange(ImmutableArray<T> items)
        => InsertRange(Count, items);

    public void AddRange(ReadOnlySpan<T> items)
        => InsertRange(Count, items);

    public void AddRange<TList>(TList list)
        where TList : struct, IReadOnlyList<T>
        => InsertRange(Count, list);

    public void AddRange<TList>(TList list, int startIndex, int count)
        where TList : struct, IReadOnlyList<T>
        => InsertRange(Count, list, startIndex, count);

    public void AddRange(IEnumerable<T> items)
        => InsertRange(Count, items);

    public void Clear()
    {
        // Keep the array to avoid churn in the object pool.
        Array.Clear(_array, 0, Count);
        _count = 0;
    }

    public readonly Span<T>.Enumerator GetEnumerator()
        => AsSpan().GetEnumerator();

    public void Insert(int index, T item)
        => InsertSpan(index, [item]);

    public void InsertRange(int index, ImmutableArray<T> items)
        => InsertSpan(index, ImmutableCollectionsMarshal.AsArray(items).AsSpan());

    public void InsertRange(int index, ReadOnlySpan<T> items)
        => InsertSpan(index, items);

    private void InsertSpan(int index, ReadOnlySpan<T> items)
    {
        Debug.Assert(index >= 0 && index <= Count);

        var count = items.Length;
        if (count == 0)
        {
            return;
        }

        var newCount = Count + count;
        EnsureCapacity(newCount);

        if (index != Count)
        {
            Array.Copy(_array, index, _array, index + count, Count - index);
        }

        items.CopyTo(_array.AsSpan(index));
        _count = newCount;
    }

    public void InsertRange<TList>(int index, TList list)
        where TList : struct, IReadOnlyList<T>
        => InsertRange(index, list, startIndex: 0, list.Count);

    public void InsertRange<TList>(int index, TList list, int startIndex, int count)
        where TList : struct, IReadOnlyList<T>
    {
        if (count == 0)
        {
            return;
        }

        var newCount = Count + count;
        EnsureCapacity(newCount);

        if (startIndex != Count)
        {
            Array.Copy(_array, index, _array, index + count, Count - index);
        }

        list.CopyTo(_array.AsSpan(index));
        _count = newCount;
    }

    public void InsertRange(int index, IEnumerable<T> items)
    {
        if (!items.TryGetCount(out var count))
        {
            // We couldn't retrieve a count, so we have to enumerate the elements.
            foreach (var item in items)
            {
                Insert(index++, item);
            }

            return;
        }

        if (count == 0)
        {
            // No items, so there's nothing to do.
            return;
        }

        var newCount = Count + count;
        EnsureCapacity(newCount);

        if (index != Count)
        {
            Array.Copy(_array, index, _array, index + count, Count - index);
        }

        items.CopyTo(_array.AsSpan(index));
        _count = newCount;
    }

    public void RemoveAt(int index)
    {
        Array.Copy(_array, index + 1, _array, index, _array.Length - index - 1);
        _count--;
    }

    public void RemoveAt(Index index)
        => RemoveAt(index.GetOffset(Count));

    /// <summary>
    ///  Returns the current contents as an <see cref="ImmutableArray{T}"/> and changes
    ///  the collection to zero length.
    /// </summary>
    public ImmutableArray<T> ToImmutableAndClear()
    {
        var newArray = ToImmutable();

        Clear();

        return newArray;
    }

    public readonly ImmutableArray<T> ToImmutable()
        => AsSpan().ToImmutableArray();

    /// <summary>
    /// Returns a span representing the active portion of the underlying array.
    /// </summary>
    /// <remarks>The returned span should not be used after disposal.</remarks>
    public readonly Span<T> AsSpan()
        => _array.AsSpan(0, Count);

    public readonly T[] ToArray()
        => AsSpan().ToArray();

    public T[] ToArrayAndClear()
    {
        var result = ToArray();

        Clear();

        return result;
    }

    public void Push(T item)
        => Add(item);

    public readonly T Peek()
        => this[^1];

    public T Pop()
    {
        var index = ^1;
        var item = this[index];
        RemoveAt(index);

        return item;
    }

    public bool TryPop([MaybeNullWhen(false)] out T item)
    {
        if (Count == 0)
        {
            item = default;
            return false;
        }

        item = Pop();
        return true;
    }

    /// <summary>
    ///  Determines whether this builder contains any elements.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if this builder contains any elements; otherwise, <see langword="false"/>.
    /// </returns>
    public readonly bool Any()
        => Count > 0;

    /// <summary>
    ///  Determines whether any element in this builder satisfies a condition.
    /// </summary>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if this builder is not empty and at least one of its elements passes
    ///  the test in the specified predicate; otherwise, <see langword="false"/>.
    /// </returns>
    public readonly bool Any(Func<T, bool> predicate)
    {
        foreach (var item in this)
        {
            if (predicate(item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Determines whether any element in this builder satisfies a condition.
    /// </summary>
    /// <param name="arg">
    ///  An argument to pass to <paramref name="predicate"/>.
    /// </param>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if this builder is not empty and at least one of its elements passes
    ///  the test in the specified predicate; otherwise, <see langword="false"/>.
    /// </returns>
    public readonly bool Any<TArg>(TArg arg, Func<T, TArg, bool> predicate)
    {
        foreach (var item in this)
        {
            if (predicate(item, arg))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Determines whether all elements in this builder satisfy a condition.
    /// </summary>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if every element in this builder passes the test
    ///  in the specified predicate, or if the builder is empty; otherwise,
    ///  <see langword="false"/>.</returns>
    public readonly bool All(Func<T, bool> predicate)
    {
        foreach (var item in this)
        {
            if (!predicate(item))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///  Determines whether all elements in this builder satisfy a condition.
    /// </summary>
    /// <param name="arg">
    ///  An argument to pass to <paramref name="predicate"/>.
    /// </param>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if every element in this builder passes the test
    ///  in the specified predicate, or if the builder is empty; otherwise,
    ///  <see langword="false"/>.</returns>
    public readonly bool All<TArg>(TArg arg, Func<T, TArg, bool> predicate)
    {
        foreach (var item in this)
        {
            if (!predicate(item, arg))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///  Returns the first element in this builder.
    /// </summary>
    /// <returns>
    ///  The first element in this builder.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///  The builder is empty.
    /// </exception>
    public readonly T First()
        => Count > 0 ? this[0] : ThrowInvalidOperation(SR.Contains_no_elements);

    /// <summary>
    ///  Returns the first element in this builder that satisfies a specified condition.
    /// </summary>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <returns>
    ///  The first element in this builder that passes the test in the specified predicate function.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///  No element satisfies the condition in <paramref name="predicate"/>.
    /// </exception>
    public readonly T First(Func<T, bool> predicate)
    {
        foreach (var item in this)
        {
            if (predicate(item))
            {
                return item;
            }
        }

        return ThrowInvalidOperation(SR.Contains_no_matching_elements);
    }

    /// <summary>
    ///  Returns the first element in this builder that satisfies a specified condition.
    /// </summary>
    /// <param name="arg">
    ///  An argument to pass to <paramref name="predicate"/>.
    /// </param>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <returns>
    ///  The first element in this builder that passes the test in the specified predicate function.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///  No element satisfies the condition in <paramref name="predicate"/>.
    /// </exception>
    public readonly T First<TArg>(TArg arg, Func<T, TArg, bool> predicate)
    {
        foreach (var item in this)
        {
            if (predicate(item, arg))
            {
                return item;
            }
        }

        return ThrowInvalidOperation(SR.Contains_no_matching_elements);
    }

    /// <summary>
    ///  Returns the first element in this builder, or a default value if the builder is empty.
    /// </summary>
    /// <returns>
    ///  <see langword="default"/>(<typeparamref name="T"/>) if this builder is empty; otherwise,
    ///  the first element in this builder.
    /// </returns>
    public readonly T? FirstOrDefault()
        => Count > 0 ? this[0] : default;

    /// <summary>
    ///  Returns the first element in this builder, or a specified default value if the builder is empty.
    /// </summary>
    /// <param name="defaultValue">
    ///  The default value to return if this builder is empty.
    /// </param>
    /// <returns>
    ///  <paramref name="defaultValue"/> if this builder is empty; otherwise,
    ///  the first element in this builder.
    /// </returns>
    public readonly T FirstOrDefault(T defaultValue)
        => Count > 0 ? this[0] : defaultValue;

    /// <summary>
    ///  Returns the first element in this builder that satisfies a condition, or a default value
    ///  if no such element is found.
    /// </summary>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <returns>
    ///  <see langword="default"/>(<typeparamref name="T"/>) if this builder is empty or if no element
    ///  passes the test specified by <paramref name="predicate"/>; otherwise, the first element in this
    ///  builder that passes the test specified by <paramref name="predicate"/>.
    /// </returns>
    public readonly T? FirstOrDefault(Func<T, bool> predicate)
    {
        foreach (var item in this)
        {
            if (predicate(item))
            {
                return item;
            }
        }

        return default;
    }

    /// <summary>
    ///  Returns the first element in this builder that satisfies a condition, or a specified default value
    ///  if no such element is found.
    /// </summary>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <param name="defaultValue">
    ///  The default value to return if this builder is empty.
    /// </param>
    /// <returns>
    ///  <paramref name="defaultValue"/> if this builder is empty or if no element
    ///  passes the test specified by <paramref name="predicate"/>; otherwise, the first element in this
    ///  builder that passes the test specified by <paramref name="predicate"/>.
    /// </returns>
    public readonly T FirstOrDefault(Func<T, bool> predicate, T defaultValue)
    {
        foreach (var item in this)
        {
            if (predicate(item))
            {
                return item;
            }
        }

        return defaultValue;
    }

    /// <summary>
    ///  Returns the first element in this builder that satisfies a condition, or a default value
    ///  if no such element is found.
    /// </summary>
    /// <param name="arg">
    ///  An argument to pass to <paramref name="predicate"/>.
    /// </param>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <returns>
    ///  <see langword="default"/>(<typeparamref name="T"/>) if this builder is empty or if no element
    ///  passes the test specified by <paramref name="predicate"/>; otherwise, the first element in this
    ///  builder that passes the test specified by <paramref name="predicate"/>.
    /// </returns>
    public readonly T? FirstOrDefault<TArg>(TArg arg, Func<T, TArg, bool> predicate)
    {
        foreach (var item in this)
        {
            if (predicate(item, arg))
            {
                return item;
            }
        }

        return default;
    }

    /// <summary>
    ///  Returns the first element in this builder that satisfies a condition, or a default value
    ///  if no such element is found.
    /// </summary>
    /// <param name="arg">
    ///  An argument to pass to <paramref name="predicate"/>.
    /// </param>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <param name="defaultValue">
    ///  The default value to return if this builder is empty.
    /// </param>
    /// <returns>
    ///  <paramref name="defaultValue"/> if this builder is empty or if no element
    ///  passes the test specified by <paramref name="predicate"/>; otherwise, the first element in this
    ///  builder that passes the test specified by <paramref name="predicate"/>.
    /// </returns>
    public readonly T FirstOrDefault<TArg>(TArg arg, Func<T, TArg, bool> predicate, T defaultValue)
    {
        foreach (var item in this)
        {
            if (predicate(item, arg))
            {
                return item;
            }
        }

        return defaultValue;
    }

    /// <summary>
    ///  Returns the last element in this builder.
    /// </summary>
    /// <returns>
    ///  The value at the last position in this builder.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///  The builder is empty.
    /// </exception>
    public readonly T Last()
        => Count > 0 ? this[^1] : ThrowInvalidOperation(SR.Contains_no_elements);

    /// <summary>
    ///  Returns the last element in this builder that satisfies a specified condition.
    /// </summary>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <returns>
    ///  The last element in this builder that passes the test in the specified predicate function.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///  No element satisfies the condition in <paramref name="predicate"/>.
    /// </exception>
    public readonly T Last(Func<T, bool> predicate)
    {
        for (var i = Count - 1; i >= 0; i--)
        {
            var item = this[i];
            if (predicate(item))
            {
                return item;
            }
        }

        return ThrowInvalidOperation(SR.Contains_no_matching_elements);
    }

    /// <summary>
    ///  Returns the last element in this builder that satisfies a specified condition.
    /// </summary>
    /// <param name="arg">
    ///  An argument to pass to <paramref name="predicate"/>.
    /// </param>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <returns>
    ///  The last element in this builder that passes the test in the specified predicate function.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///  No element satisfies the condition in <paramref name="predicate"/>.
    /// </exception>
    public readonly T Last<TArg>(TArg arg, Func<T, TArg, bool> predicate)
    {
        for (var i = Count - 1; i >= 0; i--)
        {
            var item = this[i];
            if (predicate(item, arg))
            {
                return item;
            }
        }

        return ThrowInvalidOperation(SR.Contains_no_matching_elements);
    }

    /// <summary>
    ///  Returns the last element in this builder, or a default value if the builder is empty.
    /// </summary>
    /// <returns>
    ///  <see langword="default"/>(<typeparamref name="T"/>) if this builder is empty; otherwise,
    ///  the last element in this builder.
    /// </returns>
    public readonly T? LastOrDefault()
        => Count > 0 ? this[^1] : default;

    /// <summary>
    ///  Returns the last element in this builder, or a specified default value if the builder is empty.
    /// </summary>
    /// <param name="defaultValue">
    ///  The default value to return if this builder is empty.
    /// </param>
    /// <returns>
    ///  <paramref name="defaultValue"/> if this builder is empty; otherwise,
    ///  the last element in this builder.
    /// </returns>
    public readonly T LastOrDefault(T defaultValue)
        => Count > 0 ? this[^1] : defaultValue;

    /// <summary>
    ///  Returns the last element in this builder that satisfies a condition, or a default value
    ///  if no such element is found.
    /// </summary>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <returns>
    ///  <see langword="default"/>(<typeparamref name="T"/>) if this builder is empty or if no element
    ///  passes the test specified by <paramref name="predicate"/>; otherwise, the last element in this
    ///  builder that passes the test specified by <paramref name="predicate"/>.
    /// </returns>
    public readonly T? LastOrDefault(Func<T, bool> predicate)
    {
        for (var i = Count - 1; i >= 0; i--)
        {
            var item = this[i];
            if (predicate(item))
            {
                return item;
            }
        }

        return default;
    }

    /// <summary>
    ///  Returns the last element in this builder that satisfies a condition, or a specified default value
    ///  if no such element is found.
    /// </summary>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <param name="defaultValue">
    ///  The default value to return if this builder is empty.
    /// </param>
    /// <returns>
    ///  <paramref name="defaultValue"/> if this builder is empty or if no element
    ///  passes the test specified by <paramref name="predicate"/>; otherwise, the last element in this
    ///  builder that passes the test specified by <paramref name="predicate"/>.
    /// </returns>
    public readonly T LastOrDefault(Func<T, bool> predicate, T defaultValue)
    {
        for (var i = Count - 1; i >= 0; i--)
        {
            var item = this[i];
            if (predicate(item))
            {
                return item;
            }
        }

        return defaultValue;
    }

    /// <summary>
    ///  Returns the last element in this builder that satisfies a condition, or a default value
    ///  if no such element is found.
    /// </summary>
    /// <param name="arg">
    ///  An argument to pass to <paramref name="predicate"/>.
    /// </param>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <returns>
    ///  <see langword="default"/>(<typeparamref name="T"/>) if this builder is empty or if no element
    ///  passes the test specified by <paramref name="predicate"/>; otherwise, the last element in this
    ///  builder that passes the test specified by <paramref name="predicate"/>.
    /// </returns>
    public readonly T? LastOrDefault<TArg>(TArg arg, Func<T, TArg, bool> predicate)
    {
        for (var i = Count - 1; i >= 0; i--)
        {
            var item = this[i];
            if (predicate(item, arg))
            {
                return item;
            }
        }

        return default;
    }

    /// <summary>
    ///  Returns the last element in this builder that satisfies a condition, or a default value
    ///  if no such element is found.
    /// </summary>
    /// <param name="arg">
    ///  An argument to pass to <paramref name="predicate"/>.
    /// </param>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <param name="defaultValue">
    ///  The default value to return if this builder is empty.
    /// </param>
    /// <returns>
    ///  <paramref name="defaultValue"/> if this builder is empty or if no element
    ///  passes the test specified by <paramref name="predicate"/>; otherwise, the last element in this
    ///  builder that passes the test specified by <paramref name="predicate"/>.
    /// </returns>
    public readonly T LastOrDefault<TArg>(TArg arg, Func<T, TArg, bool> predicate, T defaultValue)
    {
        for (var i = Count - 1; i >= 0; i--)
        {
            var item = this[i];
            if (predicate(item, arg))
            {
                return item;
            }
        }

        return defaultValue;
    }

    /// <summary>
    ///  Returns the only element in this builder, and throws an exception if there is not exactly one element.
    /// </summary>
    /// <returns>
    ///  The single element in this builder.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///  The builder is empty.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///  The builder contains more than one element.
    /// </exception>
    public readonly T Single()
    {
        return Count switch
        {
            1 => this[0],
            0 => ThrowInvalidOperation(SR.Contains_no_elements),
            _ => ThrowInvalidOperation(SR.Contains_more_than_one_element)
        };
    }

    /// <summary>
    ///  Returns the only element in this builder that satisfies a specified condition,
    ///  and throws an exception if more than one such element exists.
    /// </summary>
    /// <param name="predicate">
    ///  A function to test an element for a condition.
    /// </param>
    /// <returns>
    ///  The single element in this builder that satisfies a condition.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///  No element satisfies the condition in <paramref name="predicate"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///  More than one element satisfies the condition in <paramref name="predicate"/>.
    /// </exception>
    public readonly T Single(Func<T, bool> predicate)
    {
        var firstSeen = false;
        T? result = default;

        foreach (var item in this)
        {
            if (predicate(item))
            {
                if (firstSeen)
                {
                    return ThrowInvalidOperation(SR.Contains_more_than_one_matching_element);
                }

                firstSeen = true;
                result = item;
            }
        }

        if (!firstSeen)
        {
            return ThrowInvalidOperation(SR.Contains_no_matching_elements);
        }

        return result!;
    }

    /// <summary>
    ///  Returns the only element in this builder that satisfies a specified condition,
    ///  and throws an exception if more than one such element exists.
    /// </summary>
    /// <param name="arg">
    ///  An argument to pass to <paramref name="predicate"/>.
    /// </param>
    /// <param name="predicate">
    ///  A function to test an element for a condition.
    /// </param>
    /// <returns>
    ///  The single element in this builder that satisfies a condition.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///  No element satisfies the condition in <paramref name="predicate"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///  More than one element satisfies the condition in <paramref name="predicate"/>.
    /// </exception>
    public readonly T Single<TArg>(TArg arg, Func<T, TArg, bool> predicate)
    {
        var firstSeen = false;
        T? result = default;

        foreach (var item in this)
        {
            if (predicate(item, arg))
            {
                if (firstSeen)
                {
                    return ThrowInvalidOperation(SR.Contains_more_than_one_matching_element);
                }

                firstSeen = true;
                result = item;
            }
        }

        if (!firstSeen)
        {
            return ThrowInvalidOperation(SR.Contains_no_matching_elements);
        }

        return result!;
    }

    /// <summary>
    ///  Returns the only element in this builder, or a default value if the builder is empty;
    ///  this method throws an exception if there is more than one element in the builder.
    /// </summary>
    /// <returns>
    ///  The single element in this builder, or <see langword="default"/>(<typeparamref name="T"/>)
    ///  if this builder contains no elements.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///  The builder contains more than one element.
    /// </exception>
    public readonly T? SingleOrDefault()
    {
        return Count switch
        {
            1 => this[0],
            0 => default,
            _ => ThrowInvalidOperation(SR.Contains_more_than_one_element)
        };
    }

    /// <summary>
    ///  Returns the only element in this builder, or a specified default value if the builder is empty;
    ///  this method throws an exception if there is more than one element in the builder.
    /// </summary>
    /// <param name="defaultValue">
    ///  The default value to return if this builder is empty.
    /// </param>
    /// <returns>
    ///  The single element in this builder, or <paramref name="defaultValue"/>
    ///  if this builder contains no elements.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///  The builder contains more than one element.
    /// </exception>
    public readonly T SingleOrDefault(T defaultValue)
    {
        return Count switch
        {
            1 => this[0],
            0 => defaultValue,
            _ => ThrowInvalidOperation(SR.Contains_more_than_one_element)
        };
    }

    /// <summary>
    ///  Returns the only element in this builder that satisfies a specified condition or a default
    ///  value if no such element exists; this method throws an exception if more than one element
    ///  satisfies the condition.
    /// </summary>
    /// <param name="predicate">
    ///  A function to test an element for a condition.
    /// </param>
    /// <returns>
    ///  The single element in this builder that satisfies the condition, or
    ///  <see langword="default"/>(<typeparamref name="T"/>) if no such element is found.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///  More than one element satisfies the condition in predicate.
    /// </exception>
    public readonly T? SingleOrDefault(Func<T, bool> predicate)
    {
        var firstSeen = false;
        T? result = default;

        foreach (var item in this)
        {
            if (predicate(item))
            {
                if (firstSeen)
                {
                    return ThrowInvalidOperation(SR.Contains_more_than_one_matching_element);
                }

                firstSeen = true;
                result = item;
            }
        }

        return result;
    }

    /// <summary>
    ///  Returns the only element in this builder that satisfies a specified condition or a specified default
    ///  value if no such element exists; this method throws an exception if more than one element
    ///  satisfies the condition.
    /// </summary>
    /// <param name="predicate">
    ///  A function to test an element for a condition.
    /// </param>
    /// <param name="defaultValue">
    ///  The default value to return if this builder is empty.
    /// </param>
    /// <returns>
    ///  The single element in this builder that satisfies the condition, or
    ///  <paramref name="defaultValue"/> if no such element is found.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///  More than one element satisfies the condition in predicate.
    /// </exception>
    public readonly T SingleOrDefault(Func<T, bool> predicate, T defaultValue)
    {
        var firstSeen = false;
        var result = defaultValue;

        foreach (var item in this)
        {
            if (predicate(item))
            {
                if (firstSeen)
                {
                    return ThrowInvalidOperation(SR.Contains_more_than_one_matching_element);
                }

                firstSeen = true;
                result = item;
            }
        }

        return result;
    }

    /// <summary>
    ///  Returns the only element in this builder that satisfies a specified condition or a default
    ///  value if no such element exists; this method throws an exception if more than one element
    ///  satisfies the condition.
    /// </summary>
    /// <param name="arg">
    ///  An argument to pass to <paramref name="predicate"/>.
    /// </param>
    /// <param name="predicate">
    ///  A function to test an element for a condition.
    /// </param>
    /// <returns>
    ///  The single element in this builder that satisfies the condition, or
    ///  <see langword="default"/>(<typeparamref name="T"/>) if no such element is found.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///  More than one element satisfies the condition in predicate.
    /// </exception>
    public readonly T? SingleOrDefault<TArg>(TArg arg, Func<T, TArg, bool> predicate)
    {
        var firstSeen = false;
        T? result = default;

        foreach (var item in this)
        {
            if (predicate(item, arg))
            {
                if (firstSeen)
                {
                    return ThrowInvalidOperation(SR.Contains_more_than_one_matching_element);
                }

                firstSeen = true;
                result = item;
            }
        }

        return result;
    }

    /// <summary>
    ///  Returns the only element in this builder that satisfies a specified condition or a specified default
    ///  value if no such element exists; this method throws an exception if more than one element
    ///  satisfies the condition.
    /// </summary>
    /// <param name="arg">
    ///  An argument to pass to <paramref name="predicate"/>.
    /// </param>
    /// <param name="predicate">
    ///  A function to test an element for a condition.
    /// </param>
    /// <param name="defaultValue">
    ///  The default value to return if this builder is empty.
    /// </param>
    /// <returns>
    ///  The single element in this builder that satisfies the condition, or
    ///  <paramref name="defaultValue"/> if no such element is found.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///  More than one element satisfies the condition in predicate.
    /// </exception>
    public readonly T SingleOrDefault<TArg>(TArg arg, Func<T, TArg, bool> predicate, T defaultValue)
    {
        var firstSeen = false;
        var result = defaultValue;

        foreach (var item in this)
        {
            if (predicate(item, arg))
            {
                if (firstSeen)
                {
                    return ThrowInvalidOperation(SR.Contains_more_than_one_matching_element);
                }

                firstSeen = true;
                result = item;
            }
        }

        return result;
    }

    /// <summary>
    ///  This is present to help the JIT inline methods that need to throw an <see cref="InvalidOperationException"/>.
    /// </summary>
    [DoesNotReturn]
    private static T ThrowInvalidOperation(string message)
        => ThrowHelper.ThrowInvalidOperationException<T>(message);

    /// <summary>
    ///  Sorts the contents of this builder.
    /// </summary>
    public readonly void Sort()
        => Array.Sort(_array);

    /// <summary>
    ///  Sorts the contents of this array using the provided <see cref="IComparer{T}"/>.
    /// </summary>
    public readonly void Sort(IComparer<T> comparer)
        => Array.Sort(_array, comparer);

    /// <summary>
    ///  Sorts the contents of this array using the provided <see cref="Comparison{T}"/>.
    /// </summary>
    public readonly void Sort(Comparison<T> comparison)
        => Array.Sort(_array, comparison);

    public readonly ImmutableArray<T> ToImmutableOrdered()
    {
        var result = ToImmutable();
        result.Unsafe().Order();

        return result;
    }

    public readonly ImmutableArray<T> ToImmutableOrdered(IComparer<T> comparer)
    {
        var result = ToImmutable();
        result.Unsafe().Order(comparer);

        return result;
    }

    public readonly ImmutableArray<T> ToImmutableOrdered(Comparison<T> comparison)
    {
        var result = ToImmutable();
        result.Unsafe().Order(comparison);

        return result;
    }

    public readonly ImmutableArray<T> ToImmutableOrderedDescending()
    {
        var result = ToImmutable();
        result.Unsafe().OrderDescending();

        return result;
    }

    public readonly ImmutableArray<T> ToImmutableOrderedDescending(IComparer<T> comparer)
    {
        var result = ToImmutable();
        result.Unsafe().OrderDescending(comparer);

        return result;
    }

    public readonly ImmutableArray<T> ToImmutableOrderedDescending(Comparison<T> comparison)
    {
        var result = ToImmutable();
        result.Unsafe().OrderDescending(comparison);

        return result;
    }

    public readonly ImmutableArray<T> ToImmutableOrderedBy<TKey>(Func<T, TKey> keySelector)
    {
        var result = ToImmutable();
        result.Unsafe().OrderBy(keySelector);

        return result;
    }

    public readonly ImmutableArray<T> ToImmutableOrderedBy<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer)
    {
        var result = ToImmutable();
        result.Unsafe().OrderBy(keySelector, comparer);

        return result;
    }

    public readonly ImmutableArray<T> ToImmutableOrderedBy<TKey>(Func<T, TKey> keySelector, Comparison<TKey> comparison)
    {
        var result = ToImmutable();
        result.Unsafe().OrderBy(keySelector, comparison);

        return result;
    }

    public readonly ImmutableArray<T> ToImmutableOrderedByDescending<TKey>(Func<T, TKey> keySelector)
    {
        var result = ToImmutable();
        result.Unsafe().OrderByDescending(keySelector);

        return result;
    }

    public readonly ImmutableArray<T> ToImmutableOrderedByDescending<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer)
    {
        var result = ToImmutable();
        result.Unsafe().OrderByDescending(keySelector, comparer);

        return result;
    }

    public readonly ImmutableArray<T> ToImmutableOrderedByDescending<TKey>(Func<T, TKey> keySelector, Comparison<TKey> comparison)
    {
        var result = ToImmutable();
        result.Unsafe().OrderByDescending(keySelector, comparison);

        return result;
    }

    public readonly ImmutableArray<T> ToImmutableReversed()
    {
        var result = ToImmutable();
        result.Unsafe().Reverse();

        return result;
    }

    public ImmutableArray<T> ToImmutableOrderedAndClear()
    {
        var result = ToImmutableAndClear();
        result.Unsafe().Order();

        return result;
    }

    public ImmutableArray<T> ToImmutableOrderedAndClear(IComparer<T> comparer)
    {
        var result = ToImmutableAndClear();
        result.Unsafe().Order(comparer);

        return result;
    }

    public ImmutableArray<T> ToImmutableOrderedAndClear(Comparison<T> comparison)
    {
        var result = ToImmutableAndClear();
        result.Unsafe().Order(comparison);

        return result;
    }

    public ImmutableArray<T> ToImmutableOrderedDescendingAndClear()
    {
        var result = ToImmutableAndClear();
        result.Unsafe().OrderDescending();

        return result;
    }

    public ImmutableArray<T> ToImmutableOrderedDescendingAndClear(IComparer<T> comparer)
    {
        var result = ToImmutableAndClear();
        result.Unsafe().OrderDescending(comparer);

        return result;
    }

    public ImmutableArray<T> ToImmutableOrderedDescendingAndClear(Comparison<T> comparison)
    {
        var result = ToImmutableAndClear();
        result.Unsafe().OrderDescending(comparison);

        return result;
    }

    public ImmutableArray<T> ToImmutableOrderedByAndClear<TKey>(Func<T, TKey> keySelector)
    {
        var result = ToImmutableAndClear();
        result.Unsafe().OrderBy(keySelector);

        return result;
    }

    public ImmutableArray<T> ToImmutableOrderedByAndClear<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer)
    {
        var result = ToImmutableAndClear();
        result.Unsafe().OrderBy(keySelector, comparer);

        return result;
    }

    public ImmutableArray<T> ToImmutableOrderedByAndClear<TKey>(Func<T, TKey> keySelector, Comparison<TKey> comparison)
    {
        var result = ToImmutableAndClear();
        result.Unsafe().OrderBy(keySelector, comparison);

        return result;
    }

    public ImmutableArray<T> ToImmutableOrderedByDescendingAndClear<TKey>(Func<T, TKey> keySelector)
    {
        var result = ToImmutableAndClear();
        result.Unsafe().OrderByDescending(keySelector);

        return result;
    }

    public ImmutableArray<T> ToImmutableOrderedByDescendingAndClear<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer)
    {
        var result = ToImmutableAndClear();
        result.Unsafe().OrderByDescending(keySelector, comparer);

        return result;
    }

    public ImmutableArray<T> ToImmutableOrderedByDescendingAndClear<TKey>(Func<T, TKey> keySelector, Comparison<TKey> comparison)
    {
        var result = ToImmutableAndClear();
        result.Unsafe().OrderByDescending(keySelector, comparison);

        return result;
    }

    public ImmutableArray<T> ToImmutableReversedAndClear()
    {
        var result = ToImmutableAndClear();
        result.Unsafe().Reverse();

        return result;
    }
}
