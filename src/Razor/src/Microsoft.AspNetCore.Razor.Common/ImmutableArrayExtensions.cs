// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Common;

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
}
