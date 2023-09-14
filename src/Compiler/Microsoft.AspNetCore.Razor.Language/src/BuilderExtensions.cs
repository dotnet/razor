// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language;

internal static class BuilderExtensions
{
    public static ImmutableArray<T> BuildAll<T, TBuilder>(this List<TBuilder> builders, ObjectPool<HashSet<T>> pool)
        where TBuilder : IBuilder<T>
    {
        if (builders.Count == 0)
        {
            return ImmutableArray<T>.Empty;
        }

        using var result = new PooledArrayBuilder<T>(capacity: builders.Count);
        using var _ = pool.GetPooledObject(out var set);

        foreach (var builder in builders)
        {
            var item = builder.Build();

            if (set.Add(item))
            {
                result.Add(item);
            }
        }

        return result.DrainToImmutable();
    }

    public static ImmutableArray<T> BuildAllOrEmpty<T, TBuilder>(this List<TBuilder>? builders, ObjectPool<HashSet<T>> pool)
        where TBuilder : IBuilder<T>
        => builders?.BuildAll(pool) ?? ImmutableArray<T>.Empty;
}
