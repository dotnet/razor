﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Microsoft.VisualStudio.Editor.Razor;

namespace Microsoft.CodeAnalysis.Razor.Completion;

[Shared]
[Export(typeof(IRazorCompletionItemProvider))]
internal class DirectiveAttributeCompletionItemProvider : DirectiveAttributeCompletionItemProviderBase
{
    private readonly ITagHelperFactsService _tagHelperFactsService;

    [ImportingConstructor]
    public DirectiveAttributeCompletionItemProvider(ITagHelperFactsService tagHelperFactsService)
    {
        if (tagHelperFactsService is null)
        {
            throw new ArgumentNullException(nameof(tagHelperFactsService));
        }

        _tagHelperFactsService = tagHelperFactsService;
    }

    public override ImmutableArray<RazorCompletionItem> GetCompletionItems(RazorCompletionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.TagHelperDocumentContext is null)
        {
            throw new ArgumentNullException(nameof(context.TagHelperDocumentContext));
        }

        if (!FileKinds.IsComponent(context.SyntaxTree.Options.FileKind))
        {
            // Directive attributes are only supported in components
            return ImmutableArray<RazorCompletionItem>.Empty;
        }

        var owner = context.Owner;
        if (owner is null)
        {
            return ImmutableArray<RazorCompletionItem>.Empty;
        }

        if (!TryGetAttributeInfo(owner, out _, out var attributeName, out var attributeNameLocation, out _, out _))
        {
            // Either we're not in an attribute or the attribute is so malformed that we can't provide proper completions.
            return ImmutableArray<RazorCompletionItem>.Empty;
        }

        if (!attributeNameLocation.IntersectsWith(context.AbsoluteIndex))
        {
            // We're trying to retrieve completions on a portion of the name that is not supported (such as a parameter).
            return ImmutableArray<RazorCompletionItem>.Empty;
        }

        if (!TryGetElementInfo(owner.Parent.Parent, out var containingTagName, out var attributes))
        {
            // This should never be the case, it means that we're operating on an attribute that doesn't have a tag.
            return ImmutableArray<RazorCompletionItem>.Empty;
        }

        // At this point we've determined that completions have been requested for the name portion of the selected attribute.

        var completionItems = GetAttributeCompletions(attributeName, containingTagName, attributes, context.TagHelperDocumentContext);

        // We don't provide Directive Attribute completions when we're in the middle of
        // another unrelated (doesn't start with @) partially completed attribute.
        // <svg xml:| ></svg> (attributeName = "xml:") should not get any directive attribute completions.
        if (string.IsNullOrWhiteSpace(attributeName) || attributeName.StartsWith("@", StringComparison.Ordinal))
        {
            return completionItems;
        }

        return ImmutableArray<RazorCompletionItem>.Empty;
    }

    // Internal for testing
    internal ImmutableArray<RazorCompletionItem> GetAttributeCompletions(
        string selectedAttributeName,
        string containingTagName,
        IEnumerable<string> attributes,
        TagHelperDocumentContext tagHelperDocumentContext)
    {
        var descriptorsForTag = _tagHelperFactsService.GetTagHelpersGivenTag(tagHelperDocumentContext, containingTagName, parentTag: null);
        if (descriptorsForTag.Length == 0)
        {
            // If the current tag has no possible descriptors then we can't have any directive attributes.
            return ImmutableArray<RazorCompletionItem>.Empty;
        }

        // Attributes are case sensitive when matching
        var attributeCompletions = new Dictionary<string, (HashSet<BoundAttributeDescriptionInfo>, HashSet<string>)>(StringComparer.Ordinal);

        foreach (var descriptor in descriptorsForTag)
        {
            foreach (var attributeDescriptor in descriptor.BoundAttributes)
            {
                if (!attributeDescriptor.IsDirectiveAttribute())
                {
                    // We don't care about non-directive attributes
                    continue;
                }

                if (!TryAddCompletion(attributeDescriptor.Name, attributeDescriptor, descriptor) && attributeDescriptor.BoundAttributeParameters.Count > 0)
                {
                    // This attribute has parameters and the base attribute name (@bind) is already satisfied. We need to check if there are any valid
                    // parameters left to be provided, if so, we need to still represent the base attribute name in the completion list.

                    for (var j = 0; j < attributeDescriptor.BoundAttributeParameters.Count; j++)
                    {
                        var parameterDescriptor = attributeDescriptor.BoundAttributeParameters[j];
                        if (!attributes.Any(name => TagHelperMatchingConventions.SatisfiesBoundAttributeWithParameter(name, attributeDescriptor, parameterDescriptor)))
                        {
                            // This bound attribute parameter has not had a completion entry added for it, re-represent the base attribute name in the completion list
                            AddCompletion(attributeDescriptor.Name, attributeDescriptor, descriptor);
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(attributeDescriptor.IndexerNamePrefix))
                {
                    TryAddCompletion(attributeDescriptor.IndexerNamePrefix + "...", attributeDescriptor, descriptor);
                }
            }
        }

        using var completionItems = new PooledArrayBuilder<RazorCompletionItem>();

        foreach (var completion in attributeCompletions)
        {
            var insertText = completion.Key;
            if (insertText.EndsWith("...", StringComparison.Ordinal))
            {
                // Indexer attribute, we don't want to insert with the triple dot.
                insertText = insertText[..^3];
            }

            if (insertText.StartsWith("@", StringComparison.Ordinal))
            {
                // Strip off the @ from the insertion text. This change is here to align the insertion text with the
                // completion hooks into VS and VSCode. Basically, completion triggers when `@` is typed so we don't
                // want to insert `@bind` because `@` already exists.
                insertText = insertText[1..];
            }

            var (attributeDescriptionInfos, commitCharacters) = completion.Value;
            var razorCommitCharacters = commitCharacters.Select(static c => new RazorCommitCharacter(c)).ToList();

            var razorCompletionItem = new RazorCompletionItem(
                completion.Key,
                insertText,
                RazorCompletionItemKind.DirectiveAttribute,
                commitCharacters: razorCommitCharacters);
            var completionDescription = new AggregateBoundAttributeDescription(attributeDescriptionInfos.ToImmutableArray());
            razorCompletionItem.SetAttributeCompletionDescription(completionDescription);

            completionItems.Add(razorCompletionItem);
        }

        return completionItems.DrainToImmutable();

        bool TryAddCompletion(string attributeName, BoundAttributeDescriptor boundAttributeDescriptor, TagHelperDescriptor tagHelperDescriptor)
        {
            if (attributes.Any(name => string.Equals(name, attributeName, StringComparison.Ordinal)) &&
                !string.Equals(selectedAttributeName, attributeName, StringComparison.Ordinal))
            {
                // Attribute is already present on this element and it is not the selected attribute.
                // It shouldn't exist in the completion list.
                return false;
            }

            AddCompletion(attributeName, boundAttributeDescriptor, tagHelperDescriptor);
            return true;
        }

        void AddCompletion(string attributeName, BoundAttributeDescriptor boundAttributeDescriptor, TagHelperDescriptor tagHelperDescriptor)
        {
            if (!attributeCompletions.TryGetValue(attributeName, out var attributeDetails))
            {
                attributeDetails = (new HashSet<BoundAttributeDescriptionInfo>(), new HashSet<string>());
                attributeCompletions[attributeName] = attributeDetails;
            }

            (var attributeDescriptionInfos, var commitCharacters) = attributeDetails;

            var indexerCompletion = attributeName.EndsWith("...", StringComparison.Ordinal);
            var tagHelperTypeName = tagHelperDescriptor.GetTypeName();
            var descriptionInfo = BoundAttributeDescriptionInfo.From(boundAttributeDescriptor, isIndexer: indexerCompletion, tagHelperTypeName);
            attributeDescriptionInfos.Add(descriptionInfo);

            if (indexerCompletion)
            {
                // Indexer attribute, we don't want to commit with standard chars
                return;
            }

            commitCharacters.Add("=");

            if (tagHelperDescriptor.BoundAttributes.Any(b => b.IsBooleanProperty))
            {
                commitCharacters.Add(" ");
            }

            if (tagHelperDescriptor.BoundAttributes.Any(b => b.BoundAttributeParameters.Count > 0))
            {
                commitCharacters.Add(":");
            }
        }
    }
}
