// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Utilities;

namespace System.Collections.Immutable;

/// <summary>
/// <see cref="ImmutableArray{T}"/> extension methods
/// </summary>
internal static partial class ImmutableArrayExtensions
{
    /// <summary>
    /// Returns an empty array if the input array is null (default)
    /// </summary>
    public static ImmutableArray<T> NullToEmpty<T>(this ImmutableArray<T> array)
    {
        return array.IsDefault ? [] : array;
    }

    public static void SetCapacityIfLarger<T>(this ImmutableArray<T>.Builder builder, int newCapacity)
    {
        if (builder.Capacity < newCapacity)
        {
            builder.Capacity = newCapacity;
        }
    }

    public static ImmutableArray<TResult> SelectAsArray<T, TResult>(this ImmutableArray<T> source, Func<T, TResult> selector)
    {
        return source switch
        {
            [] => [],
            [var item] => [selector(item)],
            [var item1, var item2] => [selector(item1), selector(item2)],
            [var item1, var item2, var item3] => [selector(item1), selector(item2), selector(item3)],
            [var item1, var item2, var item3, var item4] => [selector(item1), selector(item2), selector(item3), selector(item4)],
            var items => BuildResult(items, selector)
        };

        static ImmutableArray<TResult> BuildResult(ImmutableArray<T> items, Func<T, TResult> selector)
        {
            using var results = new PooledArrayBuilder<TResult>(capacity: items.Length);

            foreach (var item in items)
            {
                results.Add(selector(item));
            }

            return results.DrainToImmutable();
        }
    }

    public static ImmutableArray<TResult> SelectManyAsArray<TSource, TResult>(this IReadOnlyCollection<TSource>? source, Func<TSource, ImmutableArray<TResult>> selector)
    {
        if (source is null || source.Count == 0)
        {
            return [];
        }

        using var builder = new PooledArrayBuilder<TResult>(capacity: source.Count);
        foreach (var item in source)
        {
            builder.AddRange(selector(item));
        }

        return builder.DrainToImmutable();
    }

    public static ImmutableArray<T> WhereAsArray<T>(this ImmutableArray<T> source, Func<T, bool> predicate)
    {
        if (source is [])
        {
            return [];
        }

        using var builder = new PooledArrayBuilder<T>();

        foreach (var item in source)
        {
            if (predicate(item))
            {
                builder.Add(item);
            }
        }

        return builder.DrainToImmutable();
    }

    /// <summary>
    /// Returns an <see cref="ImmutableArray{T}"/> that contains no duplicates from the <paramref name="source"/> array
    /// and returns the most recent copy of each item.
    /// </summary>
    public static ImmutableArray<T> GetMostRecentUniqueItems<T>(this ImmutableArray<T> source, IEqualityComparer<T> comparer)
    {
        if (source.IsEmpty)
        {
            return [];
        }

#if !NETSTANDARD2_0
        var uniqueItems = new HashSet<T>(capacity: source.Length, comparer);
#else
        var uniqueItems = new HashSet<T>(comparer);
#endif

        using var stack = new PooledArrayBuilder<T>(capacity: source.Length);

        // Walk the next batch in reverse to identify unique items.
        // We push them on a stack so that we can pop them in order later
        for (var i = source.Length - 1; i >= 0; i--)
        {
            var item = source[i];

            if (uniqueItems.Add(item))
            {
                stack.Push(item);
            }
        }

        // Did we actually dedupe anything? If not, just return the original.
        if (stack.Count == source.Length)
        {
            return source;
        }

        using var result = new PooledArrayBuilder<T>(capacity: stack.Count);

        while (stack.Count > 0)
        {
            result.Add(stack.Pop());
        }

        return result.DrainToImmutable();
    }

    /// <summary>
    /// Executes a binary search over an array, but allows the caller to decide what constitutes a match
    /// </summary>
    /// <typeparam name="T">Type of the elements in the array</typeparam>
    /// <typeparam name="TArg">Type of the argument to pass to the comparer</typeparam>
    /// <param name="array">The array to search</param>
    /// <param name="arg">An argument to pass to the comparison function</param>
    /// <param name="comparer">A comparison function that evaluates an item in the array. Return 0 if the item is a match,
    /// or -1 if the item indicates a successful match will be found in the left branch, or 1 if the item indicates a successful
    /// match will be found in the right branch.</param>
    /// <returns>The index of the element found</returns>
    public static int BinarySearchBy<T, TArg>(this ImmutableArray<T> array, TArg arg, Func<T, TArg, int> comparer)
    {
        var min = 0;
        var max = array.Length - 1;

        while (min <= max)
        {
            var mid = (min + max) / 2;
            var comparison = comparer(array[mid], arg);
            if (comparison == 0)
            {
                return mid;
            }

            if (comparison < 0)
            {
                min = mid + 1;
            }
            else
            {
                max = mid - 1;
            }
        }

        return ~min;
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="ImmutableArray{T}"/> in ascending order.
    /// </summary>
    /// <typeparam name="T">The type of the elements in <paramref name="array"/>.</typeparam>
    /// <param name="array">An array to ordered.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in ascending order.
    /// </returns>
    public static ImmutableArray<T> OrderAsArray<T>(this ImmutableArray<T> array)
    {
        var compareHelper = new CompareHelper<T>(comparer: null, descending: false);
        return array.OrderAsArrayCore(in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="ImmutableArray{T}"/> in ascending order.
    /// </summary>
    /// <typeparam name="T">The type of the elements in <paramref name="array"/>.</typeparam>
    /// <param name="array">An array to ordered.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> to compare elements.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in ascending order.
    /// </returns>
    public static ImmutableArray<T> OrderAsArray<T>(this ImmutableArray<T> array, IComparer<T> comparer)
    {
        var compareHelper = new CompareHelper<T>(comparer, descending: false);
        return array.OrderAsArrayCore(in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="ImmutableArray{T}"/> in ascending order.
    /// </summary>
    /// <typeparam name="T">The type of the elements in <paramref name="array"/>.</typeparam>
    /// <param name="array">An array to ordered.</param>
    /// <param name="comparison">A <see cref="Comparison{T}"/> to compare elements.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in ascending order.
    /// </returns>
    public static ImmutableArray<T> OrderAsArray<T>(this ImmutableArray<T> array, Comparison<T> comparison)
    {
        var compareHelper = new CompareHelper<T>(comparison, descending: false);
        return array.OrderAsArrayCore(in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="ImmutableArray{T}"/> in descending order.
    /// </summary>
    /// <typeparam name="T">The type of the elements in <paramref name="array"/>.</typeparam>
    /// <param name="array">An array to ordered.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in descending order.
    /// </returns>
    public static ImmutableArray<T> OrderDescendingAsArray<T>(this ImmutableArray<T> array)
    {
        var compareHelper = new CompareHelper<T>(comparer: null, descending: true);
        return array.OrderAsArrayCore(in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="ImmutableArray{T}"/> in descending order.
    /// </summary>
    /// <typeparam name="T">The type of the elements in <paramref name="array"/>.</typeparam>
    /// <param name="array">An array to ordered.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> to compare elements.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in descending order.
    /// </returns>
    public static ImmutableArray<T> OrderDescendingAsArray<T>(this ImmutableArray<T> array, IComparer<T> comparer)
    {
        var compareHelper = new CompareHelper<T>(comparer, descending: true);
        return array.OrderAsArrayCore(in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="ImmutableArray{T}"/> in descending order.
    /// </summary>
    /// <typeparam name="T">The type of the elements in <paramref name="array"/>.</typeparam>
    /// <param name="array">An array to ordered.</param>
    /// <param name="comparison">A <see cref="Comparison{T}"/> to compare elements.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in descending order.
    /// </returns>
    public static ImmutableArray<T> OrderDescendingAsArray<T>(this ImmutableArray<T> array, Comparison<T> comparison)
    {
        var compareHelper = new CompareHelper<T>(comparison, descending: true);
        return array.OrderAsArrayCore(in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="ImmutableArray{T}"/> in ascending order according to a key.
    /// </summary>
    /// <typeparam name="TElement">The type of the elements in <paramref name="array"/>.</typeparam>
    /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
    /// <param name="array">An array to ordered.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in ascending order according to a key.
    /// </returns>
    public static ImmutableArray<TElement> OrderByAsArray<TElement, TKey>(
        this ImmutableArray<TElement> array, Func<TElement, TKey> keySelector)
    {
        var compareHelper = new CompareHelper<TKey>(comparer: null, descending: false);
        return array.OrderByAsArrayCore(keySelector, in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="ImmutableArray{T}"/> in ascending order according to a key.
    /// </summary>
    /// <typeparam name="TElement">The type of the elements in <paramref name="array"/>.</typeparam>
    /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
    /// <param name="array">An array to ordered.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> to compare keys.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in ascending order according to a key.
    /// </returns>
    public static ImmutableArray<TElement> OrderByAsArray<TElement, TKey>(
        this ImmutableArray<TElement> array, Func<TElement, TKey> keySelector, IComparer<TKey> comparer)
    {
        var compareHelper = new CompareHelper<TKey>(comparer, descending: false);
        return array.OrderByAsArrayCore(keySelector, in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="ImmutableArray{T}"/> in ascending order according to a key.
    /// </summary>
    /// <typeparam name="TElement">The type of the elements in <paramref name="array"/>.</typeparam>
    /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
    /// <param name="array">An array to ordered.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <param name="comparison">A <see cref="Comparison{T}"/> to compare keys.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in ascending order according to a key.
    /// </returns>
    public static ImmutableArray<TElement> OrderByAsArray<TElement, TKey>(
        this ImmutableArray<TElement> array, Func<TElement, TKey> keySelector, Comparison<TKey> comparison)
    {
        var compareHelper = new CompareHelper<TKey>(comparison, descending: false);
        return array.OrderByAsArrayCore(keySelector, in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="ImmutableArray{T}"/> in descending order according to a key.
    /// </summary>
    /// <typeparam name="TElement">The type of the elements in <paramref name="array"/>.</typeparam>
    /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
    /// <param name="array">An array to ordered.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in descending order according to a key.
    /// </returns>
    public static ImmutableArray<TElement> OrderByDescendingAsArray<TElement, TKey>(
        this ImmutableArray<TElement> array, Func<TElement, TKey> keySelector)
    {
        var compareHelper = new CompareHelper<TKey>(comparer: null, descending: true);
        return array.OrderByAsArrayCore(keySelector, in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="ImmutableArray{T}"/> in descending order according to a key.
    /// </summary>
    /// <typeparam name="TElement">The type of the elements in <paramref name="array"/>.</typeparam>
    /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
    /// <param name="array">An array to ordered.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> to compare keys.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in descending order according to a key.
    /// </returns>
    public static ImmutableArray<TElement> OrderByDescendingAsArray<TElement, TKey>(
        this ImmutableArray<TElement> array, Func<TElement, TKey> keySelector, IComparer<TKey> comparer)
    {
        var compareHelper = new CompareHelper<TKey>(comparer, descending: true);
        return array.OrderByAsArrayCore(keySelector, in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="ImmutableArray{T}"/> in descending order according to a key.
    /// </summary>
    /// <typeparam name="TElement">The type of the elements in <paramref name="array"/>.</typeparam>
    /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
    /// <param name="array">An array to ordered.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <param name="comparison">A <see cref="Comparison{T}"/> to compare keys.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in descending order according to a key.
    /// </returns>
    public static ImmutableArray<TElement> OrderByDescendingAsArray<TElement, TKey>(
        this ImmutableArray<TElement> array, Func<TElement, TKey> keySelector, Comparison<TKey> comparison)
    {
        var compareHelper = new CompareHelper<TKey>(comparison, descending: true);
        return array.OrderByAsArrayCore(keySelector, in compareHelper);
    }

    private static ImmutableArray<T> OrderAsArrayCore<T>(this ImmutableArray<T> array, ref readonly CompareHelper<T> compareHelper)
    {
        if (array.Length > 1)
        {
            var items = array.AsSpan();

            if (SortHelper.AreOrdered(items, in compareHelper))
            {
                // No need to sort - items are already ordered.
                return array;
            }

            var length = items.Length;
            var newArray = new T[length];
            items.CopyTo(newArray);

            var comparer = compareHelper.GetOrCreateComparer();

            Array.Sort(newArray, comparer);

            return ImmutableCollectionsMarshal.AsImmutableArray(newArray);
        }

        return array;
    }

    private static ImmutableArray<TElement> OrderByAsArrayCore<TElement, TKey>(
        this ImmutableArray<TElement> array, Func<TElement, TKey> keySelector, ref readonly CompareHelper<TKey> compareHelper)
    {
        if (array.Length > 1)
        {
            var items = array.AsSpan();
            var length = items.Length;

            using var keys = ArrayPool<TKey>.Shared.GetPooledArray(minimumLength: length);

            if (SortHelper.SelectKeys(items, keySelector, in compareHelper, keys.Span))
            {
                // No need to sort - keys are already ordered.
                return array;
            }

            var newArray = new TElement[length];
            items.CopyTo(newArray);

            var comparer = compareHelper.GetOrCreateComparer();

            Array.Sort(keys.Array, newArray, 0, length, comparer);

            return ImmutableCollectionsMarshal.AsImmutableArray(newArray);
        }

        return array;
    }
}
