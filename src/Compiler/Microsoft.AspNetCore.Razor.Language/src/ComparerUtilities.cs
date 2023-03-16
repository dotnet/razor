// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.Language;

internal static class ComparerUtilities
{
    public static bool Equals<T>(IReadOnlyList<T>? first, IReadOnlyList<T>? second, IEqualityComparer<T>? comparer)
    {
        if (first == second)
        {
            return true;
        }

        if (first is null)
        {
            return second is null;
        }
        else if (second is null)
        {
            return false;
        }

        // PERF: Often, the IReadOnlyLists are really just arrays. For those cases, take a faster path.
        if (first is T[] firstArray && second is T[] secondArray)
        {
            return AreArrayContentsEqual(firstArray, secondArray, comparer);
        }

        if (first.Count != second.Count)
        {
            return false;
        }

        comparer ??= EqualityComparer<T>.Default;

        for (var i = 0; i < first.Count; i++)
        {
            if (!comparer.Equals(first[i], second[i]))
            {
                return false;
            }
        }

        return true;

        static bool AreArrayContentsEqual(T[] first, T[] second, IEqualityComparer<T>? comparer)
        {
            var length = first.Length;

            if (length != second.Length)
            {
                return false;
            }

            comparer ??= EqualityComparer<T>.Default;

            for (var i = 0; i < length; i++)
            {
                if (!comparer.Equals(first[i], second[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public static void AddToHash<T>(ref HashCodeCombiner hash, IReadOnlyList<T> list, IEqualityComparer<T>? comparer)
    {
        comparer ??= EqualityComparer<T>.Default;

        // PERF: Often, IReadOnlyLists is array. If that's the case, take a faster path.
        if (list is T[] array)
        {
            foreach (var item in array)
            {
                hash.Add(item, comparer);
            }
        }
        else
        {
            for (var i = 0; i < list.Count; i++)
            {
                hash.Add(list[i], comparer);
            }
        }
    }

    public static bool Equals<TKey, TValue>(IReadOnlyDictionary<TKey, TValue>? first, IReadOnlyDictionary<TKey, TValue>? second, IEqualityComparer<TKey>? keyComparer, IEqualityComparer<TValue>? valueComparer)
        where TKey : notnull
    {
        if (first == second)
        {
            return true;
        }

        if (first is null)
        {
            return second is null;
        }
        else if (second is null)
        {
            return false;
        }

        if (first.Count != second.Count)
        {
            return false;
        }

        keyComparer ??= EqualityComparer<TKey>.Default;
        valueComparer ??= EqualityComparer<TValue>.Default;

        switch ((first, second))
        {
            case (Dictionary<TKey, TValue> firstDictionary, Dictionary<TKey, TValue> secondDictionary):
                {
                    // 🐇 Avoid enumerator allocations for Dictionary<TKey, TValue>
                    using var firstEnumerator = firstDictionary.GetEnumerator();
                    using var secondEnumerator = secondDictionary.GetEnumerator();
                    while (firstEnumerator.MoveNext() && secondEnumerator.MoveNext())
                    {
                        if (!keyComparer.Equals(firstEnumerator.Current.Key, secondEnumerator.Current.Key)
                            || !valueComparer.Equals(firstEnumerator.Current.Value, secondEnumerator.Current.Value))
                        {
                            return false;
                        }
                    }

                    Debug.Assert(!firstEnumerator.MoveNext() && !secondEnumerator.MoveNext(), "We already know the collections have same count.");
                    return true;
                }

            case (ImmutableDictionary<TKey, TValue> firstDictionary, ImmutableDictionary<TKey, TValue> secondDictionary):
                {
                    // 🐇 Avoid enumerator allocations for ImmutableDictionary<TKey, TValue>
                    using var firstEnumerator = firstDictionary.GetEnumerator();
                    using var secondEnumerator = secondDictionary.GetEnumerator();
                    while (firstEnumerator.MoveNext() && secondEnumerator.MoveNext())
                    {
                        if (!keyComparer.Equals(firstEnumerator.Current.Key, secondEnumerator.Current.Key)
                            || !valueComparer.Equals(firstEnumerator.Current.Value, secondEnumerator.Current.Value))
                        {
                            return false;
                        }
                    }

                    Debug.Assert(!firstEnumerator.MoveNext() && !secondEnumerator.MoveNext(), "We already know the collections have same count.");
                    return true;
                }

            default:
                {
                    using var firstEnumerator = first.GetEnumerator();
                    using var secondEnumerator = second.GetEnumerator();
                    while (firstEnumerator.MoveNext() && secondEnumerator.MoveNext())
                    {
                        if (!keyComparer.Equals(firstEnumerator.Current.Key, secondEnumerator.Current.Key)
                            || !valueComparer.Equals(firstEnumerator.Current.Value, secondEnumerator.Current.Value))
                        {
                            return false;
                        }
                    }

                    Debug.Assert(!firstEnumerator.MoveNext() && !secondEnumerator.MoveNext(), "We already know the collections have same count.");
                    return true;
                }
        }
    }

    public static void AddToHash<TKey, TValue>(ref HashCodeCombiner hash, IReadOnlyDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey>? keyComparer, IEqualityComparer<TValue>? valueComparer)
        where TKey : notnull
    {
        keyComparer ??= EqualityComparer<TKey>.Default;
        valueComparer ??= EqualityComparer<TValue>.Default;

        switch (dictionary)
        {
            case Dictionary<TKey, TValue> typedDictionary:
                // 🐇 Avoid enumerator allocations for Dictionary<TKey, TValue>
                foreach (var (key, value) in typedDictionary)
                {
                    hash.Add(key, keyComparer);
                    hash.Add(value, valueComparer);
                }

                break;

            case ImmutableDictionary<TKey, TValue> typedDictionary:
                // 🐇 Avoid enumerator allocations for ImmutableDictionary<TKey, TValue>
                foreach (var (key, value) in typedDictionary)
                {
                    hash.Add(key, keyComparer);
                    hash.Add(value, valueComparer);
                }

                break;

            default:
                foreach (var (key, value) in dictionary)
                {
                    hash.Add(key, keyComparer);
                    hash.Add(value, valueComparer);
                }

                break;
        }
    }
}
