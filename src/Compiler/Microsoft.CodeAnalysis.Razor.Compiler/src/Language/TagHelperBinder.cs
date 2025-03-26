// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language;

/// <summary>
/// Enables retrieval of <see cref="TagHelperBinding"/>'s.
/// </summary>
internal sealed class TagHelperBinder
{
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
                var tagName = rule.TagName == TagHelperMatchingConventions.ElementCatchAllName
                    ? TagHelperMatchingConventions.ElementCatchAllName
                    : TagHelperPrefix + rule.TagName;

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

        var tagNameWithoutPrefix = tagName.AsSpanOrDefault();
        var parentTagNameWithoutPrefix = parentTagName.AsSpanOrDefault();

        if (TagHelperPrefix is { Length: var length and > 0 })
        {
            tagNameWithoutPrefix = tagNameWithoutPrefix[length..];

            if (parentIsTagHelper)
            {
                parentTagNameWithoutPrefix = parentTagNameWithoutPrefix[length..];
            }
        }

        using var _ = DictionaryPool<TagHelperDescriptor, ImmutableArray<TagMatchingRuleDescriptor>>.GetPooledObject(out var applicableDescriptors);

        // First, try any tag helpers with this tag name.
        if (_tagNameToDescriptorsMap.TryGetValue(tagName, out var matchingDescriptors))
        {
            FindApplicableDescriptors(matchingDescriptors, tagNameWithoutPrefix, parentTagNameWithoutPrefix, attributes, applicableDescriptors);
        }

        // Next, try any "catch all" descriptors.
        if (_tagNameToDescriptorsMap.TryGetValue(TagHelperMatchingConventions.ElementCatchAllName, out var catchAllDescriptors))
        {
            FindApplicableDescriptors(catchAllDescriptors, tagNameWithoutPrefix, parentTagNameWithoutPrefix, attributes, applicableDescriptors);
        }

        if (applicableDescriptors.Count == 0)
        {
            return null;
        }

        return new TagHelperBinding(
            tagName,
            attributes,
            parentTagName,
            applicableDescriptors.ToFrozenDictionary(),
            TagHelperPrefix);

        static void FindApplicableDescriptors(
            ImmutableArray<TagHelperDescriptor> descriptors,
            ReadOnlySpan<char> tagNameWithoutPrefix,
            ReadOnlySpan<char> parentTagNameWithoutPrefix,
            ImmutableArray<KeyValuePair<string, string>> attributes,
            Dictionary<TagHelperDescriptor, ImmutableArray<TagMatchingRuleDescriptor>> applicableDescriptors)
        {
            using var applicableRules = new PooledArrayBuilder<TagMatchingRuleDescriptor>();

            foreach (var descriptor in descriptors)
            {
                foreach (var rule in descriptor.TagMatchingRules)
                {
                    if (TagHelperMatchingConventions.SatisfiesRule(rule, tagNameWithoutPrefix, parentTagNameWithoutPrefix, attributes))
                    {
                        applicableRules.Add(rule);
                    }
                }

                if (applicableRules.Count > 0)
                {
                    applicableDescriptors[descriptor] = applicableRules.DrainToImmutable();
                }

                applicableRules.Clear();
            }
        }
    }
}
