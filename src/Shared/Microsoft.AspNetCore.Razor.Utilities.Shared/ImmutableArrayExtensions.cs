// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace System.Collections.Immutable;

/// <summary>
/// <see cref="ImmutableArray{T}"/> extension methods
/// </summary>
internal static class ImmutableArrayExtensions
{
    /// <summary>
    /// Returns an empty array if the input array is null (default)
    /// </summary>
    public static ImmutableArray<T> NullToEmpty<T>(this ImmutableArray<T> array)
    {
        return array.IsDefault ? ImmutableArray<T>.Empty : array;
    }

    public static void SetCapacityIfLarger<T>(this ImmutableArray<T>.Builder builder, int newCapacity)
    {
        if (builder.Capacity < newCapacity)
        {
            builder.Capacity = newCapacity;
        }
    }

    /// <summary>
    ///  Returns the current contents as an <see cref="ImmutableArray{T}"/> and sets
    ///  the collection to a zero length array.
    /// </summary>
    /// <remarks>
    ///  If <see cref="ImmutableArray{T}.Builder.Capacity"/> equals
    ///  <see cref="ImmutableArray{T}.Builder.Count"/>, the internal array will be extracted
    ///  as an <see cref="ImmutableArray{T}"/> without copying the contents. Otherwise, the
    ///  contents will be copied into a new array. The collection will then be set to a
    ///  zero-length array.
    /// </remarks>
    /// <returns>An immutable array.</returns>
    public static ImmutableArray<T> DrainToImmutable<T>(this ImmutableArray<T>.Builder builder)
    {
#if NET8_0_OR_GREATER
        return builder.DrainToImmutable();
#else
        if (builder.Count == 0)
        {
            return ImmutableArray<T>.Empty;
        }

        if (builder.Count == builder.Capacity)
        {
            return builder.MoveToImmutable();
        }

        var result = builder.ToImmutable();
        builder.Clear();
        return result;
#endif
    }
}
