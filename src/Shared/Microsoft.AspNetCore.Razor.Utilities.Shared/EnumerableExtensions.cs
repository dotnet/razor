// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Utilities;
using ImmutableArrayExtensions = System.Collections.Immutable.ImmutableArrayExtensions;

namespace System.Collections.Generic;

internal static class EnumerableExtensions
{
    public static ImmutableArray<TResult> SelectAsArray<T, TResult>(this IEnumerable<T> source, Func<T, TResult> selector)
    {
        if (source is ImmutableArray<T> array)
        {
            return ImmutableArrayExtensions.SelectAsArray(array, selector);
        }

        if (source is IReadOnlyList<T> list)
        {
            return list.SelectAsArray(selector);
        }

        return BuildResult(source, selector);

        static ImmutableArray<TResult> BuildResult(IEnumerable<T> items, Func<T, TResult> selector)
        {
            using var results = new PooledArrayBuilder<TResult>();

            foreach (var item in items)
            {
                results.Add(selector(item));
            }

            return results.DrainToImmutable();
        }
    }

    public static bool TryGetCount<T>(this IEnumerable<T> sequence, out int count)
    {
#if NET6_0_OR_GREATER
        return Linq.Enumerable.TryGetNonEnumeratedCount(sequence, out count);
#else
        return TryGetCount<T>((IEnumerable)sequence, out count);
#endif
    }

    public static bool TryGetCount<T>(this IEnumerable sequence, out int count)
    {
        switch (sequence)
        {
            case ICollection collection:
                count = collection.Count;
                return true;

            case ICollection<T> collection:
                count = collection.Count;
                return true;

            case IReadOnlyCollection<T> collection:
                count = collection.Count;
                return true;
        }

        count = 0;
        return false;
    }

    /// <summary>
    ///  Copies the contents of the sequence to a destination <see cref="Span{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="sequence">The sequence to copy items from.</param>
    /// <param name="destination">The span to copy items into.</param>
    /// <exception cref="ArgumentException">
    ///  The destination span is shorter than the source sequence.
    /// </exception>
    public static void CopyTo<T>(this IEnumerable<T> sequence, Span<T> destination)
    {
        // First, check a few common cases.
        switch (sequence)
        {
            // We specifically test ImmutableArray<T> to avoid boxing it as an IReadOnlyList.
            case ImmutableArray<T> array:
                ArgHelper.ThrowIfDestinationTooShort(destination, array.Length);
                array.CopyTo(destination);
                break;

            // HashSet<T> has special enumerator and doesn't implement IReadOnlyList<T>
            case HashSet<T> set:
                set.CopyTo(destination);
                break;

            case IReadOnlyList<T> list:
                list.CopyTo(destination);
                break;

            default:
                CopySequence(sequence, destination);
                break;
        }

        static void CopySequence(IEnumerable<T> sequence, Span<T> destination)
        {
            if (sequence.TryGetCount(out var count))
            {
                ArgHelper.ThrowIfDestinationTooShort(destination, count);

                var index = 0;

                foreach (var item in sequence)
                {
                    destination[index++] = item;
                }
            }
            else
            {
                var index = 0;

                foreach (var item in sequence)
                {
                    ArgHelper.ThrowIfDestinationTooShort(destination, index + 1);

                    destination[index++] = item;
                }
            }
        }
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="IEnumerable{T}"/> in ascending order.
    /// </summary>
    /// <typeparam name="T">The type of the elements in <paramref name="sequence"/>.</typeparam>
    /// <param name="sequence">An <see cref="IEnumerable{T}"/> to ordered.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in ascending order.
    /// </returns>
    public static ImmutableArray<T> OrderAsArray<T>(this IEnumerable<T> sequence)
    {
        if (sequence is ImmutableArray<T> array)
        {
            return ImmutableArrayExtensions.OrderAsArray(array);
        }

        if (sequence is IReadOnlyList<T> list)
        {
            return ReadOnlyListExtensions.OrderAsArray(list);
        }

        var compareHelper = new CompareHelper<T>(comparer: null, descending: false);
        return sequence.OrderAsArrayCore(in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="IEnumerable{T}"/> in ascending order.
    /// </summary>
    /// <typeparam name="T">The type of the elements in <paramref name="sequence"/>.</typeparam>
    /// <param name="sequence">An <see cref="IEnumerable{T}"/> to ordered.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> to compare elements.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in ascending order.
    /// </returns>
    public static ImmutableArray<T> OrderAsArray<T>(this IEnumerable<T> sequence, IComparer<T> comparer)
    {
        if (sequence is ImmutableArray<T> array)
        {
            return ImmutableArrayExtensions.OrderAsArray(array, comparer);
        }

        if (sequence is IReadOnlyList<T> list)
        {
            return ReadOnlyListExtensions.OrderAsArray(list, comparer);
        }

        var compareHelper = new CompareHelper<T>(comparer, descending: false);
        return sequence.OrderAsArrayCore(in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="IEnumerable{T}"/> in ascending order.
    /// </summary>
    /// <typeparam name="T">The type of the elements in <paramref name="sequence"/>.</typeparam>
    /// <param name="sequence">An <see cref="IEnumerable{T}"/> to ordered.</param>
    /// <param name="comparison">An <see cref="Comparison{T}"/> to compare elements.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in ascending order.
    /// </returns>
    public static ImmutableArray<T> OrderAsArray<T>(this IEnumerable<T> sequence, Comparison<T> comparison)
    {
        if (sequence is ImmutableArray<T> array)
        {
            return ImmutableArrayExtensions.OrderAsArray(array, comparison);
        }

        if (sequence is IReadOnlyList<T> list)
        {
            return ReadOnlyListExtensions.OrderAsArray(list, comparison);
        }

        var compareHelper = new CompareHelper<T>(comparison, descending: false);
        return sequence.OrderAsArrayCore(in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="IEnumerable{T}"/> in descending order.
    /// </summary>
    /// <typeparam name="T">The type of the elements in <paramref name="sequence"/>.</typeparam>
    /// <param name="sequence">An <see cref="IEnumerable{T}"/> to ordered.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in descending order.
    /// </returns>
    public static ImmutableArray<T> OrderDescendingAsArray<T>(this IEnumerable<T> sequence)
    {
        if (sequence is ImmutableArray<T> array)
        {
            return ImmutableArrayExtensions.OrderDescendingAsArray(array);
        }

        if (sequence is IReadOnlyList<T> list)
        {
            return ReadOnlyListExtensions.OrderDescendingAsArray(list);
        }

        var compareHelper = new CompareHelper<T>(comparer: null, descending: true);
        return sequence.OrderAsArrayCore(in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="IEnumerable{T}"/> in descending order.
    /// </summary>
    /// <typeparam name="T">The type of the elements in <paramref name="sequence"/>.</typeparam>
    /// <param name="sequence">An <see cref="IEnumerable{T}"/> to ordered.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> to compare elements.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in descending order.
    /// </returns>
    public static ImmutableArray<T> OrderDescendingAsArray<T>(this IEnumerable<T> sequence, IComparer<T> comparer)
    {
        if (sequence is ImmutableArray<T> array)
        {
            return ImmutableArrayExtensions.OrderDescendingAsArray(array, comparer);
        }

        if (sequence is IReadOnlyList<T> list)
        {
            return ReadOnlyListExtensions.OrderDescendingAsArray(list, comparer);
        }

        var compareHelper = new CompareHelper<T>(comparer, descending: true);
        return sequence.OrderAsArrayCore(in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="IEnumerable{T}"/> in descending order.
    /// </summary>
    /// <typeparam name="T">The type of the elements in <paramref name="sequence"/>.</typeparam>
    /// <param name="sequence">An <see cref="IEnumerable{T}"/> to ordered.</param>
    /// <param name="comparison">An <see cref="Comparison{T}"/> to compare elements.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in descending order.
    /// </returns>
    public static ImmutableArray<T> OrderDescendingAsArray<T>(this IEnumerable<T> sequence, Comparison<T> comparison)
    {
        if (sequence is ImmutableArray<T> array)
        {
            return ImmutableArrayExtensions.OrderDescendingAsArray(array, comparison);
        }

        if (sequence is IReadOnlyList<T> list)
        {
            return ReadOnlyListExtensions.OrderDescendingAsArray(list, comparison);
        }

        var compareHelper = new CompareHelper<T>(comparison, descending: true);
        return sequence.OrderAsArrayCore(in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="IEnumerable{T}"/> in ascending order according to a key.
    /// </summary>
    /// <typeparam name="TElement">The type of the elements in <paramref name="sequence"/>.</typeparam>
    /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
    /// <param name="sequence">An <see cref="IEnumerable{T}"/> to ordered.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in ascending order according to a key.
    /// </returns>
    public static ImmutableArray<TElement> OrderByAsArray<TElement, TKey>(
        this IEnumerable<TElement> sequence, Func<TElement, TKey> keySelector)
    {
        if (sequence is ImmutableArray<TElement> array)
        {
            return ImmutableArrayExtensions.OrderByAsArray(array, keySelector);
        }

        if (sequence is IReadOnlyList<TElement> list)
        {
            return ReadOnlyListExtensions.OrderByAsArray(list, keySelector);
        }

        var compareHelper = new CompareHelper<TKey>(comparer: null, descending: false);
        return sequence.OrderByAsArrayCore(keySelector, in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="IEnumerable{T}"/> in ascending order according to a key.
    /// </summary>
    /// <typeparam name="TElement">The type of the elements in <paramref name="sequence"/>.</typeparam>
    /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
    /// <param name="sequence">An <see cref="IEnumerable{T}"/> to ordered.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> to compare keys.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in ascending order according to a key.
    /// </returns>
    public static ImmutableArray<TElement> OrderByAsArray<TElement, TKey>(
        this IEnumerable<TElement> sequence, Func<TElement, TKey> keySelector, IComparer<TKey> comparer)
    {
        if (sequence is ImmutableArray<TElement> array)
        {
            return ImmutableArrayExtensions.OrderByAsArray(array, keySelector, comparer);
        }

        if (sequence is IReadOnlyList<TElement> list)
        {
            return ReadOnlyListExtensions.OrderByAsArray(list, keySelector, comparer);
        }

        var compareHelper = new CompareHelper<TKey>(comparer, descending: false);
        return sequence.OrderByAsArrayCore(keySelector, in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="IEnumerable{T}"/> in ascending order according to a key.
    /// </summary>
    /// <typeparam name="TElement">The type of the elements in <paramref name="sequence"/>.</typeparam>
    /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
    /// <param name="sequence">An <see cref="IEnumerable{T}"/> to ordered.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <param name="comparison">An <see cref="Comparison{T}"/> to compare keys.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in ascending order according to a key.
    /// </returns>
    public static ImmutableArray<TElement> OrderByAsArray<TElement, TKey>(
        this IEnumerable<TElement> sequence, Func<TElement, TKey> keySelector, Comparison<TKey> comparison)
    {
        if (sequence is ImmutableArray<TElement> array)
        {
            return ImmutableArrayExtensions.OrderByAsArray(array, keySelector, comparison);
        }

        if (sequence is IReadOnlyList<TElement> list)
        {
            return ReadOnlyListExtensions.OrderByAsArray(list, keySelector, comparison);
        }

        var compareHelper = new CompareHelper<TKey>(comparison, descending: false);
        return sequence.OrderByAsArrayCore(keySelector, in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="IEnumerable{T}"/> in descending order according to a key.
    /// </summary>
    /// <typeparam name="TElement">The type of the elements in <paramref name="sequence"/>.</typeparam>
    /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
    /// <param name="sequence">An <see cref="IEnumerable{T}"/> to ordered.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in descending order according to a key.
    /// </returns>
    public static ImmutableArray<TElement> OrderByDescendingAsArray<TElement, TKey>(
        this IEnumerable<TElement> sequence, Func<TElement, TKey> keySelector)
    {
        if (sequence is ImmutableArray<TElement> array)
        {
            return ImmutableArrayExtensions.OrderByDescendingAsArray(array, keySelector);
        }

        if (sequence is IReadOnlyList<TElement> list)
        {
            return ReadOnlyListExtensions.OrderByDescendingAsArray(list, keySelector);
        }

        var compareHelper = new CompareHelper<TKey>(comparer: null, descending: true);
        return sequence.OrderByAsArrayCore(keySelector, in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="IEnumerable{T}"/> in descending order according to a key.
    /// </summary>
    /// <typeparam name="TElement">The type of the elements in <paramref name="sequence"/>.</typeparam>
    /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
    /// <param name="sequence">An <see cref="IEnumerable{T}"/> to ordered.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> to compare keys.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in descending order according to a key.
    /// </returns>
    public static ImmutableArray<TElement> OrderByDescendingAsArray<TElement, TKey>(
        this IEnumerable<TElement> sequence, Func<TElement, TKey> keySelector, IComparer<TKey> comparer)
    {
        if (sequence is ImmutableArray<TElement> array)
        {
            return ImmutableArrayExtensions.OrderByDescendingAsArray(array, keySelector, comparer);
        }

        if (sequence is IReadOnlyList<TElement> list)
        {
            return ReadOnlyListExtensions.OrderByDescendingAsArray(list, keySelector, comparer);
        }

        var compareHelper = new CompareHelper<TKey>(comparer, descending: true);
        return sequence.OrderByAsArrayCore(keySelector, in compareHelper);
    }

    /// <summary>
    ///  Sorts the elements of an <see cref="IEnumerable{T}"/> in descending order according to a key.
    /// </summary>
    /// <typeparam name="TElement">The type of the elements in <paramref name="sequence"/>.</typeparam>
    /// <typeparam name="TKey">The type of key returned by <paramref name="keySelector"/>.</typeparam>
    /// <param name="sequence">An <see cref="IEnumerable{T}"/> to ordered.</param>
    /// <param name="keySelector">A function to extract a key from an element.</param>
    /// <param name="comparison">An <see cref="Comparison{T}"/> to compare keys.</param>
    /// <returns>
    ///  Returns a new <see cref="ImmutableArray{T}"/> whose elements are sorted in descending order according to a key.
    /// </returns>
    public static ImmutableArray<TElement> OrderByDescendingAsArray<TElement, TKey>(
        this IEnumerable<TElement> sequence, Func<TElement, TKey> keySelector, Comparison<TKey> comparison)
    {
        if (sequence is ImmutableArray<TElement> array)
        {
            return ImmutableArrayExtensions.OrderByDescendingAsArray(array, keySelector, comparison);
        }

        if (sequence is IReadOnlyList<TElement> list)
        {
            return ReadOnlyListExtensions.OrderByDescendingAsArray(list, keySelector, comparison);
        }

        var compareHelper = new CompareHelper<TKey>(comparison, descending: true);
        return sequence.OrderByAsArrayCore(keySelector, in compareHelper);
    }

    private static ImmutableArray<T> OrderAsArrayCore<T>(this IEnumerable<T> sequence, ref readonly CompareHelper<T> compareHelper)
    {
        var newArray = BuildArray(sequence);

        if (newArray.Length <= 1 || SortHelper.AreOrdered(newArray.AsSpan(), in compareHelper))
        {
            // No need to sort - items are already ordered.
            return ImmutableCollectionsMarshal.AsImmutableArray(newArray);
        }

        var comparer = compareHelper.GetOrCreateComparer();

        Array.Sort(newArray, comparer);

        return ImmutableCollectionsMarshal.AsImmutableArray(newArray);
    }

    private static ImmutableArray<TElement> OrderByAsArrayCore<TElement, TKey>(
        this IEnumerable<TElement> sequence, Func<TElement, TKey> keySelector, ref readonly CompareHelper<TKey> compareHelper)
    {
        var newArray = BuildArray(sequence);

        if (newArray.Length <= 1)
        {
            return ImmutableCollectionsMarshal.AsImmutableArray(newArray);
        }

        var length = newArray.Length;

        using var keys = ArrayPool<TKey>.Shared.GetPooledArray(minimumLength: length);

        if (SortHelper.SelectKeys(newArray.AsSpan(), keySelector, in compareHelper, keys.Span))
        {
            // No need to sort - keys are already ordered.
            return ImmutableCollectionsMarshal.AsImmutableArray(newArray);
        }

        var comparer = compareHelper.GetOrCreateComparer();

        Array.Sort(keys.Array, newArray, 0, length, comparer);

        return ImmutableCollectionsMarshal.AsImmutableArray(newArray);
    }

    private static T[] BuildArray<T>(IEnumerable<T> sequence)
    {
        if (!sequence.TryGetCount(out var count))
        {
            return BuildSlow(sequence);
        }

        if (count == 0)
        {
            return [];
        }

        var result = new T[count];
        sequence.CopyTo(result);

        return result;

        static T[] BuildSlow(IEnumerable<T> sequence)
        {
            using var builder = new PooledArrayBuilder<T>();

            foreach (var item in sequence)
            {
                builder.Add(item);
            }

            return builder.ToArray();
        }
    }
}
