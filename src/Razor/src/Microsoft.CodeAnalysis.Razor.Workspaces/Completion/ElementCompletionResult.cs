// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.CodeAnalysis.Razor.Completion;

internal sealed class ElementCompletionResult
{
    private static readonly Dictionary<IEqualityComparer<string>, ObjectPool<ImmutableSortedDictionary<string, ImmutableArray<TagHelperDescriptor>>.Builder>> s_poolCache = [];

    public ImmutableSortedDictionary<string, ImmutableArray<TagHelperDescriptor>> Completions { get; }

    private ElementCompletionResult(ImmutableSortedDictionary<string, ImmutableArray<TagHelperDescriptor>> completions)
    {
        Completions = completions;
    }

    internal static ElementCompletionResult Create(Dictionary<string, HashSet<TagHelperDescriptor>> completions)
    {
        // ImmutableSortedDictionary uses the key comparer for both equality and ordering
        var comparer = completions.Comparer as IComparer<string> ?? StringComparer.Ordinal;

        if (!s_poolCache.TryGetValue(completions.Comparer, out var pool))
        {
            pool = DefaultPool.Create(new SortedDictionaryBuilderPolicy(comparer));
            s_poolCache[completions.Comparer] = pool;
        }

        using var _ = pool.GetPooledObject(out var builder);

        foreach (var (key, value) in completions)
        {
            builder.Add(key, [.. value]);
        }

        return new ElementCompletionResult(builder.ToImmutable());
    }

    private sealed class SortedDictionaryBuilderPolicy(IComparer<string> comparer) : IPooledObjectPolicy<ImmutableSortedDictionary<string, ImmutableArray<TagHelperDescriptor>>.Builder>
    {
        public ImmutableSortedDictionary<string, ImmutableArray<TagHelperDescriptor>>.Builder Create()
            => ImmutableSortedDictionary.CreateBuilder<string, ImmutableArray<TagHelperDescriptor>>(comparer);

        public bool Return(ImmutableSortedDictionary<string, ImmutableArray<TagHelperDescriptor>>.Builder obj)
        {
            obj.Clear();
            return true;
        }
    }
}
