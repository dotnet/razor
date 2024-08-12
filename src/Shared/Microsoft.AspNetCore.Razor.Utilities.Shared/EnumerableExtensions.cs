// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace System.Collections.Generic;

internal static class EnumerableExtensions
{
    public static ImmutableArray<TResult> SelectAsArray<T, TResult>(this IEnumerable<T> source, Func<T, TResult> selector)
    {
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
        // Check a couple of common cases.
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
        }

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
