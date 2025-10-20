// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.Completion;

internal sealed class ElementCompletionResult
{
    public ImmutableDictionary<string, ImmutableArray<TagHelperDescriptor>> Completions { get; }

    private ElementCompletionResult(ImmutableDictionary<string, ImmutableArray<TagHelperDescriptor>> completions)
    {
        Completions = completions;
    }

    internal static ElementCompletionResult Create(Dictionary<string, HashSet<TagHelperDescriptor>> completions)
    {
        var pool = AspNetCore.Razor.PooledObjects.DictionaryBuilderPool<string, ImmutableArray<TagHelperDescriptor>>.Create(completions.Comparer);
        using var builder = new AspNetCore.Razor.PooledObjects.PooledDictionaryBuilder<string, ImmutableArray<TagHelperDescriptor>>(pool);

        foreach (var (key, value) in completions)
        {
            builder.Add(key, value.ToImmutableArray());
        }

        return new ElementCompletionResult(builder.ToImmutable());
    }
}
