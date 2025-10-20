// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.Completion;

internal sealed class ElementCompletionResult
{
    public ReadOnlyDictionary<string, ImmutableArray<TagHelperDescriptor>> Completions { get; }

    public IEqualityComparer<string> Comparer { get; }

    private ElementCompletionResult(ReadOnlyDictionary<string, ImmutableArray<TagHelperDescriptor>> completions, IEqualityComparer<string> comparer)
    {
        Completions = completions;
        Comparer = comparer;
    }

    internal static ElementCompletionResult Create(Dictionary<string, HashSet<TagHelperDescriptor>> completions)
    {
        if (completions is null)
        {
            throw new ArgumentNullException(nameof(completions));
        }

        var readonlyCompletions = new Dictionary<string, ImmutableArray<TagHelperDescriptor>>(
            capacity: completions.Count,
            comparer: completions.Comparer);

        foreach (var (key, value) in completions)
        {
            readonlyCompletions.Add(key, value.ToImmutableArray());
        }

        return new ElementCompletionResult(new ReadOnlyDictionary<string, ImmutableArray<TagHelperDescriptor>>(readonlyCompletions), completions.Comparer);
    }
}
