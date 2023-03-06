// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language;

internal static class BuilderExtensions
{
    public static T[] BuildAll<T, TBuilder>(this List<TBuilder> builders, ObjectPool<HashSet<T>> pool)
        where TBuilder : IBuilder<T>
    {
        using var set = new PooledHashSet<T>(pool);

        foreach (var builder in builders)
        {
            set.Add(builder.Build());
        }

        return set.ToArray();
    }

    public static T[] BuildAllOrEmpty<T, TBuilder>(this List<TBuilder>? builders, ObjectPool<HashSet<T>> pool)
        where TBuilder : IBuilder<T>
        => builders?.BuildAll(pool) ?? Array.Empty<T>();
}
