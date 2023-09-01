﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Editor.Razor;

internal class LanguageServerTagHelperCompletionService : TagHelperCompletionService
{
    private readonly ITagHelperFactsService _tagHelperFactsService;
    private static readonly HashSet<TagHelperDescriptor> s_emptyHashSet = new();

    [ImportingConstructor]
    public LanguageServerTagHelperCompletionService(ITagHelperFactsService tagHelperFactsService)
    {
        if (tagHelperFactsService is null)
        {
            throw new ArgumentNullException(nameof(tagHelperFactsService));
        }

        _tagHelperFactsService = tagHelperFactsService;
    }

    // This API attempts to understand a users context as they're typing in a Razor file to provide TagHelper based attribute IntelliSense.
    //
    // Scenarios for TagHelper attribute IntelliSense follows:
    // 1. TagHelperDescriptor's have matching required attribute names
    //  -> Provide IntelliSense for the required attributes of those descriptors to lead users towards a TagHelperified element.
    // 2. TagHelperDescriptor entirely applies to current element. Tag name, attributes, everything is fulfilled.
    //  -> Provide IntelliSense for the bound attributes for the applied descriptors.
    //
    // Within each of the above scenarios if an attribute completion has a corresponding bound attribute we associate it with the corresponding
    // BoundAttributeDescriptor. By doing this a user can see what C# type a TagHelper expects for the attribute.
    public override AttributeCompletionResult GetAttributeCompletions(AttributeCompletionContext completionContext)
    {
        if (completionContext is null)
        {
            throw new ArgumentNullException(nameof(completionContext));
        }

        var attributeCompletions = completionContext.ExistingCompletions.ToDictionary(
            completion => completion,
            _ => new HashSet<BoundAttributeDescriptor>(),
            StringComparer.OrdinalIgnoreCase);

        var documentContext = completionContext.DocumentContext;
        var descriptorsForTag = _tagHelperFactsService.GetTagHelpersGivenTag(documentContext, completionContext.CurrentTagName, completionContext.CurrentParentTagName);
        if (descriptorsForTag.Length == 0)
        {
            // If the current tag has no possible descriptors then we can't have any additional attributes.
            return AttributeCompletionResult.Create(attributeCompletions);
        }

        var prefix = documentContext.Prefix ?? string.Empty;
        Debug.Assert(completionContext.CurrentTagName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        var applicableTagHelperBinding = _tagHelperFactsService.GetTagHelperBinding(
            documentContext,
            completionContext.CurrentTagName,
            completionContext.Attributes,
            completionContext.CurrentParentTagName,
            completionContext.CurrentParentIsTagHelper);

        var applicableDescriptors = new HashSet<TagHelperDescriptor>(TagHelperChecksumComparer.Instance);

        if (applicableTagHelperBinding is { Descriptors: var descriptors })
        {
            applicableDescriptors.UnionWith(descriptors);
        }

        var unprefixedTagName = completionContext.CurrentTagName[prefix.Length..];

        if (!completionContext.InHTMLSchema(unprefixedTagName) &&
            applicableDescriptors.All(descriptor => descriptor.TagOutputHint is null))
        {
            // This isn't a known HTML tag and no descriptor has an output element hint. Remove all previous completions.
            attributeCompletions.Clear();
        }

        foreach (var descriptor in descriptorsForTag)
        {
            if (applicableDescriptors.Contains(descriptor))
            {
                foreach (var attributeDescriptor in descriptor.BoundAttributes)
                {
                    if (attributeDescriptor.Name != null)
                    {
                        UpdateCompletions(attributeDescriptor.Name, attributeDescriptor);
                    }

                    if (!string.IsNullOrEmpty(attributeDescriptor.IndexerNamePrefix))
                    {
                        UpdateCompletions(attributeDescriptor.IndexerNamePrefix + "...", attributeDescriptor);
                    }
                }
            }
            else
            {
                var htmlNameToBoundAttribute = new Dictionary<string, BoundAttributeDescriptor>(StringComparer.OrdinalIgnoreCase);
                foreach (var attributeDescriptor in descriptor.BoundAttributes)
                {
                    if (attributeDescriptor.Name != null)
                    {
                        htmlNameToBoundAttribute[attributeDescriptor.Name] = attributeDescriptor;
                    }

                    if (!string.IsNullOrEmpty(attributeDescriptor.IndexerNamePrefix))
                    {
                        htmlNameToBoundAttribute[attributeDescriptor.IndexerNamePrefix] = attributeDescriptor;
                    }
                }

                foreach (var rule in descriptor.TagMatchingRules)
                {
                    foreach (var requiredAttribute in rule.Attributes)
                    {
                        if (htmlNameToBoundAttribute.TryGetValue(requiredAttribute.Name, out var attributeDescriptor))
                        {
                            UpdateCompletions(requiredAttribute.DisplayName, attributeDescriptor);
                        }
                        else
                        {
                            UpdateCompletions(requiredAttribute.DisplayName, possibleDescriptor: null);
                        }
                    }
                }
            }
        }

        var completionResult = AttributeCompletionResult.Create(attributeCompletions);
        return completionResult;

        void UpdateCompletions(string attributeName, BoundAttributeDescriptor? possibleDescriptor)
        {
            if (completionContext.Attributes.Any(attribute => string.Equals(attribute.Key, attributeName, StringComparison.OrdinalIgnoreCase)) &&
                (completionContext.CurrentAttributeName is null ||
                !string.Equals(attributeName, completionContext.CurrentAttributeName, StringComparison.OrdinalIgnoreCase)))
            {
                // Attribute is already present on this element and it is not the attribute in focus.
                // It shouldn't exist in the completion list.
                return;
            }

            if (!attributeCompletions.TryGetValue(attributeName, out var rules))
            {
                rules = new HashSet<BoundAttributeDescriptor>();
                attributeCompletions[attributeName] = rules;
            }

            if (possibleDescriptor != null)
            {
                rules.Add(possibleDescriptor);
            }
        }
    }

    public override ElementCompletionResult GetElementCompletions(ElementCompletionContext completionContext)
    {
        if (completionContext is null)
        {
            throw new ArgumentNullException(nameof(completionContext));
        }

        var elementCompletions = new Dictionary<string, HashSet<TagHelperDescriptor>>(StringComparer.Ordinal);

        AddAllowedChildrenCompletions(completionContext, elementCompletions);

        if (elementCompletions.Count > 0)
        {
            // If the containing element is already a TagHelper and only allows certain children.
            var emptyResult = ElementCompletionResult.Create(elementCompletions);
            return emptyResult;
        }

        var tagAttributes = completionContext.Attributes;

        var catchAllDescriptors = new HashSet<TagHelperDescriptor>();
        var prefix = completionContext.DocumentContext.Prefix ?? string.Empty;
        var possibleChildDescriptors = _tagHelperFactsService.GetTagHelpersGivenParent(completionContext.DocumentContext, completionContext.ContainingParentTagName);
        possibleChildDescriptors = FilterFullyQualifiedCompletions(possibleChildDescriptors);
        foreach (var possibleDescriptor in possibleChildDescriptors)
        {
            var addRuleCompletions = false;
            var outputHint = possibleDescriptor.TagOutputHint;

            foreach (var rule in possibleDescriptor.TagMatchingRules)
            {
                if (!TagHelperMatchingConventions.SatisfiesParentTag(completionContext.ContainingParentTagName.AsSpanOrDefault(), rule))
                {
                    continue;
                }

                if (rule.TagName == TagHelperMatchingConventions.ElementCatchAllName)
                {
                    catchAllDescriptors.Add(possibleDescriptor);
                }
                else if (elementCompletions.ContainsKey(rule.TagName))
                {
                    // If we've previously added a completion item for this rules tag, then we want to add this item
                    addRuleCompletions = true;
                }
                else if (completionContext.ExistingCompletions.Contains(rule.TagName))
                {
                    // If Html wants to show a completion item for rules tag, then we want to add this item
                    addRuleCompletions = true;
                }
                else if (outputHint != null)
                {
                    // If the current descriptor has an output hint we need to make sure it shows up only when its output hint would normally show up.
                    // Example: We have a MyTableTagHelper that has an output hint of "table" and a MyTrTagHelper that has an output hint of "tr".
                    // If we try typing in a situation like this: <body > | </body>
                    // We'd expect to only get "my-table" as a completion because the "body" tag doesn't allow "tr" tags.
                    addRuleCompletions = completionContext.ExistingCompletions.Contains(outputHint);
                }
                else if (!completionContext.InHTMLSchema(rule.TagName) || rule.TagName.Any(c => char.IsUpper(c)))
                {
                    // If there is an unknown HTML schema tag that doesn't exist in the current completion we should add it. This happens for
                    // TagHelpers that target non-schema oriented tags.
                    // The second condition is a workaround for the fact that InHTMLSchema does a case insensitive comparison.
                    // We want completions to not dedupe by casing. E.g, we want to show both <div> and <DIV> completion items separately.
                    addRuleCompletions = true;
                }

                // If we think this completion should be added based on tag name, thats great, but lets also make sure the attributes are correct
                if (addRuleCompletions && TagHelperMatchingConventions.SatisfiesAttributes(tagAttributes, rule))
                {
                    UpdateCompletions(prefix + rule.TagName, possibleDescriptor, elementCompletions);
                }
            }
        }

        // We needed to track all catch-alls and update their completions after all other completions have been completed.
        // This way, any TagHelper added completions will also have catch-alls listed under their entries.
        foreach (var catchAllDescriptor in catchAllDescriptors)
        {
            foreach (var kvp in elementCompletions)
            {
                var completionTagName = kvp.Key;
                var tagHelperDescriptors = kvp.Value;

                if (tagHelperDescriptors.Count > 0 ||
                    (!string.IsNullOrEmpty(prefix) && completionTagName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    // The current completion either has other TagHelper's associated with it or is prefixed with a non-empty
                    // TagHelper prefix.
                    UpdateCompletions(completionTagName, catchAllDescriptor, elementCompletions, tagHelperDescriptors);
                }
            }
        }

        var result = ElementCompletionResult.Create(elementCompletions);
        return result;

        static void UpdateCompletions(string tagName, TagHelperDescriptor possibleDescriptor, Dictionary<string, HashSet<TagHelperDescriptor>> elementCompletions, HashSet<TagHelperDescriptor>? tagHelperDescriptors = null)
        {
            if (possibleDescriptor.BoundAttributes.Any(boundAttribute => boundAttribute.IsDirectiveAttribute()))
            {
                // This is a TagHelper that ultimately represents a DirectiveAttribute. In classic Razor TagHelper land TagHelpers with bound attribute descriptors
                // are valuable to show in the completion list to understand what was possible for a certain tag; however, with Blazor directive attributes stand
                // on their own and shouldn't be indicated at the element level completion.
                return;
            }

            HashSet<TagHelperDescriptor>? existingRuleDescriptors;
            if (tagHelperDescriptors is not null)
            {
                existingRuleDescriptors = tagHelperDescriptors;
            }
            else if (!elementCompletions.TryGetValue(tagName, out existingRuleDescriptors))
            {
                existingRuleDescriptors = new HashSet<TagHelperDescriptor>();
                elementCompletions[tagName] = existingRuleDescriptors;
            }

            existingRuleDescriptors.Add(possibleDescriptor);
        }
    }

    private void AddAllowedChildrenCompletions(
        ElementCompletionContext completionContext,
        Dictionary<string, HashSet<TagHelperDescriptor>> elementCompletions)
    {
        if (completionContext.ContainingTagName is null)
        {
            // If we're at the root then there's no containing TagHelper to specify allowed children.
            return;
        }

        var prefix = completionContext.DocumentContext.Prefix ?? string.Empty;

        var binding = _tagHelperFactsService.GetTagHelperBinding(
            completionContext.DocumentContext,
            completionContext.ContainingParentTagName,
            completionContext.Attributes,
            parentTag: null,
            parentIsTagHelper: false);

        if (binding is null)
        {
            // Containing tag is not a TagHelper; therefore, it allows any children.
            return;
        }

        foreach (var descriptor in binding.Descriptors)
        {
            foreach (var childTag in descriptor.AllowedChildTags)
            {
                var prefixedName = string.Concat(prefix, childTag.Name);
                var descriptors = _tagHelperFactsService.GetTagHelpersGivenTag(
                    completionContext.DocumentContext,
                    prefixedName,
                    completionContext.ContainingParentTagName);

                if (descriptors.Length == 0)
                {
                    if (!elementCompletions.ContainsKey(prefixedName))
                    {
                        elementCompletions[prefixedName] = s_emptyHashSet;
                    }

                    continue;
                }

                if (!elementCompletions.TryGetValue(prefixedName, out var existingRuleDescriptors))
                {
                    existingRuleDescriptors = new HashSet<TagHelperDescriptor>(TagHelperChecksumComparer.Instance);
                    elementCompletions[prefixedName] = existingRuleDescriptors;
                }

                existingRuleDescriptors.AddRange(descriptors);
            }
        }
    }

    private static ImmutableArray<TagHelperDescriptor> FilterFullyQualifiedCompletions(ImmutableArray<TagHelperDescriptor> possibleChildDescriptors)
    {
        // Iterate once through the list to tease apart fully qualified and short name TagHelpers
        using var fullyQualifiedTagHelpers = new PooledArrayBuilder<TagHelperDescriptor>();
        var shortNameTagHelpers = new HashSet<TagHelperDescriptor>(ShortNameToFullyQualifiedComparer.Instance);

        foreach (var descriptor in possibleChildDescriptors)
        {
            if (descriptor.IsComponentFullyQualifiedNameMatch())
            {
                fullyQualifiedTagHelpers.Add(descriptor);
            }
            else
            {
                shortNameTagHelpers.Add(descriptor);
            }
        }

        // Re-combine the short named & fully qualified TagHelpers but filter out any fully qualified TagHelpers that have a short
        // named representation already.
        using var filteredList = new PooledArrayBuilder<TagHelperDescriptor>(capacity: shortNameTagHelpers.Count);
        filteredList.AddRange(shortNameTagHelpers);

        foreach (var fullyQualifiedTagHelper in fullyQualifiedTagHelpers)
        {
            if (!shortNameTagHelpers.Contains(fullyQualifiedTagHelper))
            {
                // Unimported completion item that isn't represented in a short named form.
                filteredList.Add(fullyQualifiedTagHelper);
            }
            else
            {
                // There's already a shortname variant of this item, don't include it.
            }
        }

        return filteredList.DrainToImmutable();
    }
}
