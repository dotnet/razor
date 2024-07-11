// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace System.Collections.Generic;

internal static class ReadOnlyListExtensions
{
    public static ImmutableArray<TResult> SelectAsArray<T, TResult>(this IReadOnlyList<T> source, Func<T, TResult> selector)
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

        static ImmutableArray<TResult> BuildResult(IReadOnlyList<T> items, Func<T, TResult> selector)
        {
            using var results = new PooledArrayBuilder<TResult>(capacity: items.Count);

            for (var i = 0; i < items.Count; i++)
            {
                results.Add(selector(items[i]));
            }

            return results.DrainToImmutable();
        }
    }

    /// <summary>
    ///  Determines whether a list contains any elements.
    /// </summary>
    /// <param name="list">
    ///  The <see cref="IReadOnlyList{T}"/> to check for emptiness.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if the list contains any elements; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool Any<T>(this IReadOnlyList<T> list)
        => list.Count > 0;

    /// <summary>
    ///  Determines whether any element of a list satisfies a condition.
    /// </summary>
    /// <param name="list">
    ///  An <see cref="IReadOnlyList{T}"/> whose elements to apply the predicate to.
    /// </param>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if the list is not empty and at least one of its elements passes
    ///  the test in the specified predicate; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool Any<T>(this IReadOnlyList<T> list, Func<T, bool> predicate)
    {
        foreach (var item in list.AsEnumerable())
        {
            if (predicate(item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Determines whether any element of a list satisfies a condition.
    /// </summary>
    /// <param name="list">
    ///  An <see cref="IReadOnlyList{T}"/> whose elements to apply the predicate to.
    /// </param>
    /// <param name="arg">
    ///  An argument to pass to <paramref name="predicate"/>.
    /// </param>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if the list is not empty and at least one of its elements passes
    ///  the test in the specified predicate; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool Any<T, TArg>(this IReadOnlyList<T> list, TArg arg, Func<T, TArg, bool> predicate)
    {
        foreach (var item in list.AsEnumerable())
        {
            if (predicate(item, arg))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Determines whether all elements of a list satisfy a condition.
    /// </summary>
    /// <param name="list">
    ///  An <see cref="IReadOnlyList{T}"/> whose elements to apply the predicate to.
    /// </param>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if every element of the list passes the test
    ///  in the specified predicate, or if the list is empty; otherwise,
    ///  <see langword="false"/>.</returns>
    public static bool All<T>(this IReadOnlyList<T> list, Func<T, bool> predicate)
    {
        foreach (var item in list.AsEnumerable())
        {
            if (!predicate(item))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///  Determines whether all elements of a list satisfy a condition.
    /// </summary>
    /// <param name="list">
    ///  An <see cref="IReadOnlyList{T}"/> whose elements to apply the predicate to.
    /// </param>
    /// <param name="arg">
    ///  An argument to pass to <paramref name="predicate"/>.
    /// </param>
    /// <param name="predicate">
    ///  A function to test each element for a condition.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if every element of the list passes the test
    ///  in the specified predicate, or if the list is empty; otherwise,
    ///  <see langword="false"/>.</returns>
    public static bool All<T, TArg>(this IReadOnlyList<T> list, TArg arg, Func<T, TArg, bool> predicate)
    {
        foreach (var item in list.AsEnumerable())
        {
            if (!predicate(item, arg))
            {
                return false;
            }
        }

        return true;
    }

    public static Enumerable<T> AsEnumerable<T>(this IReadOnlyList<T> source) => new(source);

    public readonly struct Enumerable<T>(IReadOnlyList<T> source) : IEnumerable<T>
    {
        public Enumerator<T> GetEnumerator() => new(source);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public struct Enumerator<T>(IReadOnlyList<T> source) : IEnumerator<T>
    {
        private int _index;
        private T _current;

        public readonly T Current => _current!;

        readonly object IEnumerator.Current => _current!;

        public readonly void Dispose()
        {
        }

        public bool MoveNext()
        {
            if (_index < source.Count)
            {
                _current = source[_index];
                _index++;
                return true;
            }

            return false;
        }

        public void Reset()
        {
            _index = 0;
            _current = default!;
        }
    }
}
