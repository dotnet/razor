// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Utilities;

internal static class SortHelper
{
    /// <summary>
    ///  Walk through <paramref name="items"/> and determine whether they are already ordered using
    ///  the provided <see cref="CompareHelper{T}"/>.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if the items are in order; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  When the items are already ordered, there's no need to perform a sort.
    /// </remarks>
    public static bool AreOrdered<T>(ReadOnlySpan<T> items, ref readonly CompareHelper<T> compareHelper)
    {
        var isOutOfOrder = false;

        for (var i = 1; i < items.Length; i++)
        {
            if (!compareHelper.InSortedOrder(items[i], items[i - 1]))
            {
                isOutOfOrder = true;
                break;
            }
        }

        return !isOutOfOrder;
    }

    /// <summary>
    ///  Walk through <paramref name="list"/> and determine whether they are already ordered using
    ///  the provided <see cref="CompareHelper{T}"/>.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if the items are in order; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  When the items are already ordered, there's no need to perform a sort.
    /// </remarks>
    public static bool AreOrdered<T>(IReadOnlyList<T> list, ref readonly CompareHelper<T> compareHelper)
    {
        var isOutOfOrder = false;
        var count = list.Count;

        for (var i = 1; i < count; i++)
        {
            if (!compareHelper.InSortedOrder(list[i], list[i - 1]))
            {
                isOutOfOrder = true;
                break;
            }
        }

        return !isOutOfOrder;
    }

    /// <summary>
    ///  Walk through <paramref name="items"/> and convert each element to a key using <paramref name="keySelector"/>.
    ///  While walking, each computed key is compared with the previous one using the provided <see cref="CompareHelper{T}"/>
    ///  to determine whether they are already ordered.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if the keys are in order; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  When the keys are already ordered, there's no need to perform a sort.
    /// </remarks>
    public static bool SelectKeys<TElement, TKey>(
        ReadOnlySpan<TElement> items, Func<TElement, TKey> keySelector, ref readonly CompareHelper<TKey> compareHelper, Span<TKey> keys)
    {
        var isOutOfOrder = false;

        keys[0] = keySelector(items[0]);

        for (var i = 1; i < items.Length; i++)
        {
            keys[i] = keySelector(items[i]);

            if (!isOutOfOrder && !compareHelper.InSortedOrder(keys[i], keys[i - 1]))
            {
                isOutOfOrder = true;

                // Continue processing to finish converting elements to keys. However, we can stop comparing keys.
            }
        }

        return !isOutOfOrder;
    }

    /// <summary>
    ///  Walk through <paramref name="list"/> and convert each element to a key using <paramref name="keySelector"/>.
    ///  While walking, each computed key is compared with the previous one using the provided <see cref="CompareHelper{T}"/>
    ///  to determine whether they are already ordered.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if the keys are in order; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  When the keys are already ordered, there's no need to perform a sort.
    /// </remarks>
    public static bool SelectKeys<TElement, TKey>(
        IReadOnlyList<TElement> list, Func<TElement, TKey> keySelector, ref readonly CompareHelper<TKey> compareHelper, Span<TKey> keys)
    {
        var isOutOfOrder = false;
        var count = list.Count;

        keys[0] = keySelector(list[0]);

        for (var i = 1; i < count; i++)
        {
            keys[i] = keySelector(list[i]);

            if (!isOutOfOrder && !compareHelper.InSortedOrder(keys[i], keys[i - 1]))
            {
                isOutOfOrder = true;

                // Continue processing to finish converting elements to keys. However, we can stop comparing keys.
            }
        }

        return !isOutOfOrder;
    }
}
