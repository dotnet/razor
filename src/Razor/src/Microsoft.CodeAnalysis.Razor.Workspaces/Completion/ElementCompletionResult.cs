// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.CodeAnalysis.Razor.Completion;

internal sealed class ElementCompletionResult
{
    private static readonly Dictionary<IEqualityComparer<string>, ObjectPool<ImmutableDictionary<string, ImmutableArray<TagHelperDescriptor>>.Builder>> s_poolCache = [];

    public ImmutableDictionary<string, ImmutableArray<TagHelperDescriptor>> Completions { get; }

    private ElementCompletionResult(ImmutableDictionary<string, ImmutableArray<TagHelperDescriptor>> completions)
    {
        Completions = completions;
    }

    internal static ElementCompletionResult Create(Dictionary<string, HashSet<TagHelperDescriptor>> completions)
    {
        if (!s_poolCache.TryGetValue(completions.Comparer, out var pool))
        {
            pool = DictionaryBuilderPool<string, ImmutableArray<TagHelperDescriptor>>.Create(completions.Comparer);
            s_poolCache[completions.Comparer] = pool;
        }

        using var _ = pool.GetPooledObject(out var builder);
        foreach (var (key, value) in completions)
        {
            builder.Add(key, [.. value]);
        }

        return new ElementCompletionResult(builder.ToImmutable());
    }
}
