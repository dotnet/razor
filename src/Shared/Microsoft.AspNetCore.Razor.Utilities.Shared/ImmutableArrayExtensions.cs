// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace System.Collections.Immutable;

/// <summary>
/// <see cref="ImmutableArray{T}"/> extension methods
/// </summary>
internal static class ImmutableArrayExtensions
{
    public static bool Any<T, TArg>(this ImmutableArray<T> array, Func<T, TArg, bool> predicate, TArg arg)
    {
        foreach (var item in array)
        {
            if (predicate(item, arg))
            {
                return true;
            }
        }

        return false;
    }

    public static bool Contains<T, TComparer>(this ImmutableArray<T> array, T item, TComparer comparer)
        where TComparer : IEqualityComparer<T>
    {
        foreach (var actual in array)
        {
            if (comparer.Equals(actual, item))
            {
                return true;
            }
        }

        return false;
    }

    public static T First<T, TArg>(this ImmutableArray<T> array, Func<T, TArg, bool> predicate, TArg arg)
    {
        foreach (var item in array)
        {
            if (predicate(item, arg))
            {
                return item;
            }
        }

        // Throw the same exception that System.Linq would
        return ImmutableArray<T>.Empty.First(static t => false);
    }

    public static T? FirstOrDefault<T, TArg>(this ImmutableArray<T> array, Func<T, TArg, bool> predicate, TArg arg)
    {
        foreach (var item in array)
        {
            if (predicate(item, arg))
            {
                return item;
            }
        }

        return default;
    }

    /// <summary>
    /// Returns an empty array if the input array is null (default)
    /// </summary>
    public static ImmutableArray<T> NullToEmpty<T>(this ImmutableArray<T> array)
    {
        return array.IsDefault ? ImmutableArray<T>.Empty : array;
    }
}
