// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language;

/// <summary>
/// Enables retrieval of <see cref="TagHelperBinding"/>'s.
/// </summary>
internal sealed class TagHelperBinder
{
    private readonly ImmutableArray<TagHelperDescriptor> _catchAllDescriptors;
    private readonly Dictionary<string, ImmutableArray<TagHelperDescriptor>> _tagNameToDescriptorsMap;

    public string? TagHelperPrefix { get; }
    public ImmutableArray<TagHelperDescriptor> TagHelpers { get; }

    /// <summary>
    /// Instantiates a new instance of the <see cref="TagHelperBinder"/>.
    /// </summary>
    /// <param name="tagHelperPrefix">The tag helper prefix being used by the document.</param>
    /// <param name="tagHelpers">The <see cref="TagHelperDescriptor"/>s that the <see cref="TagHelperBinder"/>
    /// will pull from.</param>
    public TagHelperBinder(string? tagHelperPrefix, ImmutableArray<TagHelperDescriptor> tagHelpers)
    {
        TagHelperPrefix = tagHelperPrefix;
        TagHelpers = tagHelpers;

        using var catchAllDescriptors = new PooledArrayBuilder<TagHelperDescriptor>();
        using var pooledMap = StringDictionaryPool<ImmutableArray<TagHelperDescriptor>.Builder>.OrdinalIgnoreCase.GetPooledObject(out var mapBuilder);
        using var pooledSet = HashSetPool<TagHelperDescriptor>.GetPooledObject(out var processedDescriptors);

        // Build a map of tag name -> tag helpers.
        foreach (var descriptor in tagHelpers)
        {
            if (!processedDescriptors.Add(descriptor))
            {
                // We're already seen this descriptor, skip it.
                continue;
            }

            foreach (var rule in descriptor.TagMatchingRules)
            {
                if (rule.TagName == TagHelperMatchingConventions.ElementCatchAllName)
                {
                    // This is a catch-all descriptor, we can keep track of it separately.
                    catchAllDescriptors.Add(descriptor);
                    continue;
                }

                var tagName = tagHelperPrefix is not null
                    ? tagHelperPrefix + rule.TagName
                    : rule.TagName;

                var builder = mapBuilder.GetOrAdd(tagName, _ => ImmutableArray.CreateBuilder<TagHelperDescriptor>());

                builder.Add(descriptor);
            }
        }

        // Build the final dictionary with immutable arrays.
        _tagNameToDescriptorsMap = new(capacity: mapBuilder.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in mapBuilder)
        {
            _tagNameToDescriptorsMap.Add(key, value.DrainToImmutable());
        }

        // Build the catch all descriptors array.
        _catchAllDescriptors = catchAllDescriptors.DrainToImmutable();
    }

    /// <summary>
    /// Gets all tag helpers that match the given HTML tag criteria.
    /// </summary>
    /// <param name="tagName">The name of the HTML tag to match. Providing a '*' tag name
    /// retrieves catch-all <see cref="TagHelperDescriptor"/>s (descriptors that target every tag).</param>
    /// <param name="attributes">Attributes on the HTML tag.</param>
    /// <param name="parentTagName">The parent tag name of the given <paramref name="tagName"/> tag.</param>
    /// <param name="parentIsTagHelper">Is the parent tag of the given <paramref name="tagName"/> tag a tag helper.</param>
    /// <returns><see cref="TagHelperDescriptor"/>s that apply to the given HTML tag criteria.
    /// Will return <c>null</c> if no <see cref="TagHelperDescriptor"/>s are a match.</returns>
    public TagHelperBinding? GetBinding(
        string tagName,
        ImmutableArray<KeyValuePair<string, string>> attributes,
        string? parentTagName,
        bool parentIsTagHelper)
    {
        if (!TagHelperPrefix.IsNullOrEmpty() &&
            (tagName.Length <= TagHelperPrefix.Length ||
             !tagName.StartsWith(TagHelperPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            // The tagName doesn't have the tag helper prefix, we can short circuit.
            return null;
        }

        var tagNameSpan = tagName.AsSpanOrDefault();
        var parentTagNameSpan = parentTagName.AsSpanOrDefault();

        if (TagHelperPrefix is { Length: var length and > 0 })
        {
            tagNameSpan = tagNameSpan[length..];

            if (parentIsTagHelper)
            {
                parentTagNameSpan = parentTagNameSpan[length..];
            }
        }

        using var pooledSet = HashSetPool<TagHelperDescriptor>.GetPooledObject(out var distinctSet);
        using var resultsBuilder = new PooledArrayBuilder<TagHelperBoundRulesInfo>();
        using var tempRulesBuilder = new PooledArrayBuilder<TagMatchingRuleDescriptor>();

        // First, try any tag helpers with this tag name.
        if (_tagNameToDescriptorsMap.TryGetValue(tagName, out var matchingDescriptors))
        {
            CollectBoundRulesInfo(
                matchingDescriptors,
                tagNameSpan, parentTagNameSpan, attributes,
                distinctSet, ref resultsBuilder.AsRef(), ref tempRulesBuilder.AsRef());
        }

        // Next, try any "catch all" descriptors.
        CollectBoundRulesInfo(
            _catchAllDescriptors,
            tagNameSpan, parentTagNameSpan, attributes,
            distinctSet, ref resultsBuilder.AsRef(), ref tempRulesBuilder.AsRef());

        if (resultsBuilder.Count == 0)
        {
            return null;
        }

        return new TagHelperBinding(
            tagName,
            attributes,
            parentTagName,
            resultsBuilder.DrainToImmutable(),
            TagHelperPrefix);

        static void CollectBoundRulesInfo(
            ImmutableArray<TagHelperDescriptor> descriptors,
            ReadOnlySpan<char> tagName,
            ReadOnlySpan<char> parentTagName,
            ImmutableArray<KeyValuePair<string, string>> attributes,
            HashSet<TagHelperDescriptor> distinctSet,
            ref PooledArrayBuilder<TagHelperBoundRulesInfo> resultsBuilder,
            ref PooledArrayBuilder<TagMatchingRuleDescriptor> tempRulesBuilder)
        {
            foreach (var descriptor in descriptors)
            {
                if (!distinctSet.Add(descriptor))
                {
                    continue; // We've already seen this descriptor.
                }

                Debug.Assert(tempRulesBuilder.Count == 0);

                foreach (var rule in descriptor.TagMatchingRules)
                {
                    if (TagHelperMatchingConventions.SatisfiesRule(rule, tagName, parentTagName, attributes))
                    {
                        tempRulesBuilder.Add(rule);
                    }
                }

                if (tempRulesBuilder.Count > 0)
                {
                    resultsBuilder.Add(new(descriptor, tempRulesBuilder.ToImmutable()));
                }

                tempRulesBuilder.Clear();
            }
        }
    }
}
