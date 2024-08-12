// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Razor.Utilities;

namespace System.Collections.Immutable;

internal static partial class ImmutableArrayExtensions
{
    /// <summary>
    ///  Provides a set of unsafe operations that can be performed on an <see cref="ImmutableArray{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the elements in <paramref name="array"/>.</typeparam>
    /// <param name="array">The array that unsafe operations will target.</param>
    /// <returns>
    ///  Returns a struct that provides access to unsafe operations to perform on the
    ///  <see cref="ImmutableArray{T}"/>. These operations mutate the internal array
    ///  of the <see cref="ImmutableArray{T}"/> and should be only be used in
    ///  performance-critical code.
    /// </returns>
    public static UnsafeOperations<T> Unsafe<T>(this ImmutableArray<T> array)
        => new(array);

    public readonly ref struct UnsafeOperations<T>(ImmutableArray<T> array)
    {
        /// <summary>
        ///  Sorts the elements of this <see cref="ImmutableArray{T}"/> in ascending order.
        /// </summary>
        public void Order()
        {
            var compareHelper = new CompareHelper<T>(comparer: null, descending: false);
            array.UnsafeOrderCore(in compareHelper);
        }

        /// <summary>
        ///  Sorts the elements of this <see cref="ImmutableArray{T}"/> in ascending order.
        /// </summary>
        /// <param name="comparer">An <see cref="IComparer{T}"/> to compare elements.</param>
        public void Order(IComparer<T> comparer)
        {
            var compareHelper = new CompareHelper<T>(comparer, descending: false);
            array.UnsafeOrderCore(in compareHelper);
        }

        /// <summary>
        ///  Sorts the elements of this <see cref="ImmutableArray{T}"/> in ascending order.
        /// </summary>
        /// <param name="comparison">A <see cref="Comparison{T}"/> to compare elements.</param>
        public void Order(Comparison<T> comparison)
        {
            var compareHelper = new CompareHelper<T>(comparison, descending: false);
            array.UnsafeOrderCore(in compareHelper);
        }

        /// <summary>
        ///  Sorts the elements of this <see cref="ImmutableArray{T}"/> in descending order.
        /// </summary>
        public void OrderDescending()
        {
            var compareHelper = new CompareHelper<T>(comparer: null, descending: true);
            array.UnsafeOrderCore(in compareHelper);
        }

        /// <summary>
        ///  Sorts the elements of this <see cref="ImmutableArray{T}"/> in descending order.
        /// </summary>
        /// <param name="comparer">An <see cref="IComparer{T}"/> to compare elements.</param>
        public void OrderDescending(IComparer<T> comparer)
        {
            var compareHelper = new CompareHelper<T>(comparer, descending: true);
            array.UnsafeOrderCore(in compareHelper);
        }

        /// <summary>
        ///  Sorts the elements of this <see cref="ImmutableArray{T}"/> in descending order.
        /// </summary>
        /// <param name="comparison">A <see cref="Comparison{T}"/> to compare elements.</param>
        public void OrderDescending(Comparison<T> comparison)
        {
            var compareHelper = new CompareHelper<T>(comparison, descending: true);
            array.UnsafeOrderCore(in compareHelper);
        }

        /// <summary>
        ///  Sorts the elements of this <see cref="ImmutableArray{T}"/> in ascending order according to a key.
        /// </summary>
        /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
        /// <param name="keySelector">A function to extract a key from an element.</param>
        public void OrderBy<TKey>(Func<T, TKey> keySelector)
        {
            var compareHelper = new CompareHelper<TKey>(comparer: null, descending: false);
            array.UnsafeOrderByCore(keySelector, in compareHelper);
        }

        /// <summary>
        ///  Sorts the elements of this <see cref="ImmutableArray{T}"/> in ascending order according to a key.
        /// </summary>
        /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
        /// <param name="keySelector">A function to extract a key from an element.</param>
        /// <param name="comparer">An <see cref="IComparer{T}"/> to compare elements.</param>
        public void OrderBy<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer)
        {
            var compareHelper = new CompareHelper<TKey>(comparer, descending: false);
            array.UnsafeOrderByCore(keySelector, in compareHelper);
        }

        /// <summary>
        ///  Sorts the elements of this <see cref="ImmutableArray{T}"/> in ascending order according to a key.
        /// </summary>
        /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
        /// <param name="keySelector">A function to extract a key from an element.</param>
        /// <param name="comparison">An <see cref="Comparison{T}"/> to compare elements.</param>
        public void OrderBy<TKey>(Func<T, TKey> keySelector, Comparison<TKey> comparison)
        {
            var compareHelper = new CompareHelper<TKey>(comparison, descending: false);
            array.UnsafeOrderByCore(keySelector, in compareHelper);
        }

        /// <summary>
        ///  Sorts the elements of this <see cref="ImmutableArray{T}"/> in descending order according to a key.
        /// </summary>
        /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
        /// <param name="keySelector">A function to extract a key from an element.</param>
        public void OrderByDescending<TKey>(Func<T, TKey> keySelector)
        {
            var compareHelper = new CompareHelper<TKey>(comparer: null, descending: true);
            array.UnsafeOrderByCore(keySelector, in compareHelper);
        }

        /// <summary>
        ///  Sorts the elements of this <see cref="ImmutableArray{T}"/> in descending order according to a key.
        /// </summary>
        /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
        /// <param name="keySelector">A function to extract a key from an element.</param>
        /// <param name="comparer">An <see cref="IComparer{T}"/> to compare elements.</param>
        public void OrderByDescending<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer)
        {
            var compareHelper = new CompareHelper<TKey>(comparer, descending: true);
            array.UnsafeOrderByCore(keySelector, in compareHelper);
        }

        /// <summary>
        ///  Sorts the elements of this <see cref="ImmutableArray{T}"/> in descending order according to a key.
        /// </summary>
        /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
        /// <param name="keySelector">A function to extract a key from an element.</param>
        /// <param name="comparison">An <see cref="Comparison{T}"/> to compare elements.</param>
        public void OrderByDescending<TKey>(Func<T, TKey> keySelector, Comparison<TKey> comparison)
        {
            var compareHelper = new CompareHelper<TKey>(comparison, descending: true);
            array.UnsafeOrderByCore(keySelector, in compareHelper);
        }
    }

    private static ImmutableArray<T> UnsafeOrderCore<T>(this ImmutableArray<T> array, ref readonly CompareHelper<T> compareHelper)
    {
        // Note: Checking the length will throw if array.IsDefault returns true.
        // So, we can assume that the inner array below is non-null.
        if (array.Length <= 1)
        {
            return array;
        }

        var innerArray = ImmutableCollectionsMarshal.AsArray(array)!;
        var items = innerArray.AsSpan();

        if (AreOrdered(items, in compareHelper))
        {
            // No need to sort - items are already ordered.
            return array;
        }

        var comparer = compareHelper.GetOrCreateComparer();

        Array.Sort(innerArray, comparer);

        return array;
    }

    private static ImmutableArray<TElement> UnsafeOrderByCore<TElement, TKey>(
        this ImmutableArray<TElement> array, Func<TElement, TKey> keySelector, ref readonly CompareHelper<TKey> compareHelper)
    {
        // Note: Checking the length will throw if array.IsDefault returns true.
        // So, we can assume that the inner array below is non-null.
        if (array.Length <= 1)
        {
            return array;
        }

        var innerArray = ImmutableCollectionsMarshal.AsArray(array)!;
        var items = innerArray.AsSpan();
        var length = items.Length;

        using var keys = ArrayPool<TKey>.Shared.GetPooledArray(minimumLength: length);

        if (SelectKeys(items, keySelector, in compareHelper, keys.Span))
        {
            // No need to sort - keys are already ordered.
            return array;
        }

        var comparer = compareHelper.GetOrCreateComparer();

        Array.Sort(keys.Array, innerArray, 0, length, comparer);

        return array;
    }
}
