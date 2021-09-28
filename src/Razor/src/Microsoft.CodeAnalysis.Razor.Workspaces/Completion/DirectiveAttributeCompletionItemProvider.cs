﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Microsoft.VisualStudio.Editor.Razor;

namespace Microsoft.CodeAnalysis.Razor.Completion
{
    [Shared]
    [Export(typeof(RazorCompletionItemProvider))]
    internal class DirectiveAttributeCompletionItemProvider : DirectiveAttributeCompletionItemProviderBase
    {
        private static readonly RazorCompletionItem[] s_noDirectiveAttributeCompletionItems = Array.Empty<RazorCompletionItem>();

        private readonly TagHelperFactsService _tagHelperFactsService;

        [ImportingConstructor]
        public DirectiveAttributeCompletionItemProvider(TagHelperFactsService tagHelperFactsService)
        {
            if (tagHelperFactsService is null)
            {
                throw new ArgumentNullException(nameof(tagHelperFactsService));
            }

            _tagHelperFactsService = tagHelperFactsService;
        }

        public override IReadOnlyList<RazorCompletionItem> GetCompletionItems(RazorCompletionContext context, SourceSpan location)
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
                return s_noDirectiveAttributeCompletionItems;
            }

            var change = new SourceChange(location, string.Empty);
            var owner = context.SyntaxTree.Root.LocateOwner(change);

            if (owner == null)
            {
                return s_noDirectiveAttributeCompletionItems;
            }

            if (!TryGetAttributeInfo(owner, out _, out var attributeName, out var attributeNameLocation, out _, out _))
            {
                // Either we're not in an attribute or the attribute is so malformed that we can't provide proper completions.
                return s_noDirectiveAttributeCompletionItems;
            }

            if (!attributeNameLocation.IntersectsWith(location.AbsoluteIndex))
            {
                // We're trying to retrieve completions on a portion of the name that is not supported (such as a parameter).
                return s_noDirectiveAttributeCompletionItems;
            }

            if (!TryGetElementInfo(owner.Parent.Parent, out var containingTagName, out var attributes))
            {
                // This should never be the case, it means that we're operating on an attribute that doesn't have a tag.
                return s_noDirectiveAttributeCompletionItems;
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

            return s_noDirectiveAttributeCompletionItems;
        }

        // Internal for testing
        internal IReadOnlyList<RazorCompletionItem> GetAttributeCompletions(
            string selectedAttributeName,
            string containingTagName,
            IEnumerable<string> attributes,
            TagHelperDocumentContext tagHelperDocumentContext)
        {
            var descriptorsForTag = _tagHelperFactsService.GetTagHelpersGivenTag(tagHelperDocumentContext, containingTagName, parentTag: null);
            if (descriptorsForTag.Count == 0)
            {
                // If the current tag has no possible descriptors then we can't have any directive attributes.
                return Array.Empty<RazorCompletionItem>();
            }

            // Attributes are case sensitive when matching
            var attributeCompletions = new Dictionary<string, (HashSet<BoundAttributeDescriptionInfo>, HashSet<string>)>(StringComparer.Ordinal);
            for (var i = 0; i < descriptorsForTag.Count; i++)
            {
                var descriptor = descriptorsForTag[i];

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

            var completionItems = new List<RazorCompletionItem>();
            foreach (var completion in attributeCompletions)
            {
                var insertText = completion.Key;
                if (insertText.EndsWith("...", StringComparison.Ordinal))
                {
                    // Indexer attribute, we don't want to insert with the triple dot.
                    insertText = insertText.Substring(0, insertText.Length - 3);
                }

                if (insertText.StartsWith("@", StringComparison.Ordinal))
                {
                    // Strip off the @ from the insertion text. This change is here to align the insertion text with the
                    // completion hooks into VS and VSCode. Basically, completion triggers when `@` is typed so we don't
                    // want to insert `@bind` because `@` already exists.
                    insertText = insertText.Substring(1);
                }

                var (attributeDescriptionInfos, commitCharacters) = completion.Value;

                var razorCompletionItem = new RazorCompletionItem(
                    completion.Key,
                    insertText,
                    RazorCompletionItemKind.DirectiveAttribute,
                    commitCharacters: commitCharacters);
                var completionDescription = new AggregateBoundAttributeDescription(attributeDescriptionInfos.ToArray());
                razorCompletionItem.SetAttributeCompletionDescription(completionDescription);

                completionItems.Add(razorCompletionItem);
            }

            return completionItems;

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
                var descriptionInfo = BoundAttributeDescriptionInfo.From(boundAttributeDescriptor, indexer: indexerCompletion, tagHelperTypeName);
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
}
