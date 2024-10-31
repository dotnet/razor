﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text.Adornments;

namespace Microsoft.CodeAnalysis.Razor.Hover;

internal static class HoverFactory
{
    public static Task<VSInternalHover?> GetHoverAsync(
        RazorCodeDocument codeDocument,
        string documentFilePath,
        int absoluteIndex,
        HoverDisplayOptions options,
        ISolutionQueryOperations solutionQueryOperations,
        CancellationToken cancellationToken)
    {
        var syntaxTree = codeDocument.GetSyntaxTree();

        var owner = syntaxTree.Root.FindInnermostNode(absoluteIndex);
        if (owner is null)
        {
            Debug.Fail("Owner should never be null.");
            return SpecializedTasks.Null<VSInternalHover>();
        }

        // For cases where the point in the middle of an attribute,
        // such as <any tes$$t=""></any>
        // the node desired is the *AttributeSyntax
        if (owner.Kind is SyntaxKind.MarkupTextLiteral)
        {
            owner = owner.Parent;
        }

        var tagHelperDocumentContext = codeDocument.GetTagHelperContext();

        if (HtmlFacts.TryGetElementInfo(owner, out var containingTagNameToken, out var attributes, closingForwardSlashOrCloseAngleToken: out _) &&
            containingTagNameToken.Span.IntersectsWith(absoluteIndex))
        {
            if (owner is MarkupStartTagSyntax or MarkupEndTagSyntax &&
                containingTagNameToken.Content.Equals(SyntaxConstants.TextTagName, StringComparison.OrdinalIgnoreCase))
            {
                // It's possible for there to be a <Text> component that is in scope, and would be found by the GetTagHelperBinding
                // call below, but a text tag, regardless of casing, inside C# code, is always just a text tag, not a component.
                return SpecializedTasks.Null<VSInternalHover>();
            }

            // We want to find the parent tag, but looking up ancestors in the tree can find other things,
            // for example when hovering over a start tag, the first ancestor is actually the element it
            // belongs to, or in other words, the exact same tag! To work around this we just make sure we
            // only check nodes that are at a different location in the file.
            var ownerStart = owner.SpanStart;

            // Hovering over HTML tag name
            var ancestors = owner.Ancestors().Where(n => n.SpanStart != ownerStart);
            var (parentTag, parentIsTagHelper) = TagHelperFacts.GetNearestAncestorTagInfo(ancestors);
            var stringifiedAttributes = TagHelperFacts.StringifyAttributes(attributes);
            var binding = TagHelperFacts.GetTagHelperBinding(
                tagHelperDocumentContext,
                containingTagNameToken.Content,
                stringifiedAttributes,
                parentTag: parentTag,
                parentIsTagHelper: parentIsTagHelper);

            if (binding is null)
            {
                // No matching tagHelpers, it's just HTML
                return SpecializedTasks.Null<VSInternalHover>();
            }
            else if (binding.IsAttributeMatch)
            {
                // Hovered over a HTML tag name but the binding matches an attribute
                return SpecializedTasks.Null<VSInternalHover>();
            }

            Debug.Assert(binding.Descriptors.Any());

            var span = containingTagNameToken.GetLinePositionSpan(codeDocument.Source);

            return ElementInfoToHoverAsync(
                documentFilePath, binding.Descriptors, span, options, solutionQueryOperations, cancellationToken);
        }

        if (HtmlFacts.TryGetAttributeInfo(owner, out containingTagNameToken, out _, out var selectedAttributeName, out var selectedAttributeNameLocation, out attributes) &&
            selectedAttributeNameLocation?.IntersectsWith(absoluteIndex) == true)
        {
            // When finding parents for attributes, we make sure to find the parent of the containing tag, otherwise these methods
            // would return the parent of the attribute, which is not helpful, as its just going to be the containing element
            var containingTag = containingTagNameToken.Parent;
            var ancestors = containingTag.Ancestors().Where(n => n.SpanStart != containingTag.SpanStart);
            var (parentTag, parentIsTagHelper) = TagHelperFacts.GetNearestAncestorTagInfo(ancestors);

            // Hovering over HTML attribute name
            var stringifiedAttributes = TagHelperFacts.StringifyAttributes(attributes);

            var binding = TagHelperFacts.GetTagHelperBinding(
                tagHelperDocumentContext,
                containingTagNameToken.Content,
                stringifiedAttributes,
                parentTag: parentTag,
                parentIsTagHelper: parentIsTagHelper);

            if (binding is null)
            {
                // No matching TagHelpers, it's just HTML
                return SpecializedTasks.Null<VSInternalHover>();
            }

            Debug.Assert(binding.Descriptors.Any());
            var tagHelperAttributes = TagHelperFacts.GetBoundTagHelperAttributes(
                tagHelperDocumentContext,
                selectedAttributeName.AssumeNotNull(),
                binding);

            // Grab the first attribute that we find that intersects with this location. That way if there are multiple attributes side-by-side aka hovering over:
            //      <input checked| minimized />
            // Then we take the left most attribute (attributes are returned in source order).
            var attribute = attributes.First(a => a.Span.IntersectsWith(absoluteIndex));
            if (attribute is MarkupTagHelperAttributeSyntax thAttributeSyntax)
            {
                attribute = thAttributeSyntax.Name;
            }
            else if (attribute is MarkupMinimizedTagHelperAttributeSyntax thMinimizedAttribute)
            {
                attribute = thMinimizedAttribute.Name;
            }
            else if (attribute is MarkupTagHelperDirectiveAttributeSyntax directiveAttribute)
            {
                attribute = directiveAttribute.Name;
            }
            else if (attribute is MarkupMinimizedTagHelperDirectiveAttributeSyntax miniDirectiveAttribute)
            {
                attribute = miniDirectiveAttribute;
            }

            var attributeName = attribute.GetContent();
            var span = attribute.GetLinePositionSpan(codeDocument.Source);

            // Include the @ in the range
            switch (attribute.Parent.Kind)
            {
                case SyntaxKind.MarkupTagHelperDirectiveAttribute:
                    var directiveAttribute = (MarkupTagHelperDirectiveAttributeSyntax)attribute.Parent;
                    span = span.WithStart(start => start.WithCharacter(ch => ch - directiveAttribute.Transition.FullWidth));
                    attributeName = "@" + attributeName;
                    break;

                case SyntaxKind.MarkupMinimizedTagHelperDirectiveAttribute:
                    var minimizedAttribute = (MarkupMinimizedTagHelperDirectiveAttributeSyntax)containingTag;
                    span = span.WithStart(start => start.WithCharacter(ch => ch - minimizedAttribute.Transition.FullWidth));
                    attributeName = "@" + attributeName;
                    break;
            }

            return Task.FromResult(AttributeInfoToHover(tagHelperAttributes, attributeName, span, options));
        }

        return SpecializedTasks.Null<VSInternalHover>();
    }

    private static VSInternalHover? AttributeInfoToHover(
        ImmutableArray<BoundAttributeDescriptor> boundAttributes,
        string attributeName,
        LinePositionSpan span,
        HoverDisplayOptions options)
    {
        var descriptionInfos = boundAttributes.SelectAsArray(boundAttribute =>
        {
            var isIndexer = TagHelperMatchingConventions.SatisfiesBoundAttributeIndexer(boundAttribute, attributeName.AsSpan());
            return BoundAttributeDescriptionInfo.From(boundAttribute, isIndexer);
        });

        var attrDescriptionInfo = new AggregateBoundAttributeDescription(descriptionInfos);

        if (options.SupportsVisualStudioExtensions &&
            ClassifiedTagHelperTooltipFactory.TryCreateTooltip(attrDescriptionInfo, out ContainerElement? classifiedTextElement))
        {
            return new VSInternalHover
            {
                Contents = Array.Empty<SumType<string, MarkedString>>(),
                Range = span.ToRange(),
                RawContent = classifiedTextElement,
            };
        }

        if (!MarkupTagHelperTooltipFactory.TryCreateTooltip(attrDescriptionInfo, options.MarkupKind, out var tooltipContent))
        {
            return null;
        }

        return new VSInternalHover
        {
            Contents = new MarkupContent()
            {
                Value = tooltipContent.Value,
                Kind = tooltipContent.Kind,
            },
            Range = span.ToRange(),
        };
    }

    private static async Task<VSInternalHover?> ElementInfoToHoverAsync(
        string documentFilePath,
        ImmutableArray<TagHelperDescriptor> descriptors,
        LinePositionSpan span,
        HoverDisplayOptions options,
        ISolutionQueryOperations solutionQueryOperations,
        CancellationToken cancellationToken)
    {
        var descriptionInfos = descriptors.SelectAsArray(BoundElementDescriptionInfo.From);
        var elementDescriptionInfo = new AggregateBoundElementDescription(descriptionInfos);

        if (options.SupportsVisualStudioExtensions)
        {
            var classifiedTextElement = await ClassifiedTagHelperTooltipFactory
                .TryCreateTooltipContainerAsync(documentFilePath, elementDescriptionInfo, solutionQueryOperations, cancellationToken)
                .ConfigureAwait(false);

            if (classifiedTextElement is not null)
            {
                return new VSInternalHover
                {
                    Contents = Array.Empty<SumType<string, MarkedString>>(),
                    Range = span.ToRange(),
                    RawContent = classifiedTextElement,
                };
            }
        }

        var tooltipContent = await MarkupTagHelperTooltipFactory
            .TryCreateTooltipAsync(documentFilePath, elementDescriptionInfo, solutionQueryOperations, options.MarkupKind, cancellationToken)
            .ConfigureAwait(false);

        if (tooltipContent is null)
        {
            return null;
        }

        return new VSInternalHover
        {
            Contents = new MarkupContent()
            {
                Value = tooltipContent.Value,
                Kind = tooltipContent.Kind,
            },
            Range = span.ToRange()
        };
    }
}
