﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text.Adornments;
using VisualStudioMarkupKind = Microsoft.VisualStudio.LanguageServer.Protocol.MarkupKind;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hover;

internal sealed class HoverInfoService : IHoverInfoService
{
    private readonly ITagHelperFactsService _tagHelperFactsService;
    private readonly LSPTagHelperTooltipFactory _lspTagHelperTooltipFactory;
    private readonly VSLSPTagHelperTooltipFactory _vsLspTagHelperTooltipFactory;
    private readonly HtmlFactsService _htmlFactsService;

    [ImportingConstructor]
    public HoverInfoService(
        ITagHelperFactsService tagHelperFactsService,
        LSPTagHelperTooltipFactory lspTagHelperTooltipFactory,
        VSLSPTagHelperTooltipFactory vsLspTagHelperTooltipFactory,
        HtmlFactsService htmlFactsService)
    {
        if (tagHelperFactsService is null)
        {
            throw new ArgumentNullException(nameof(tagHelperFactsService));
        }

        if (lspTagHelperTooltipFactory is null)
        {
            throw new ArgumentNullException(nameof(lspTagHelperTooltipFactory));
        }

        if (vsLspTagHelperTooltipFactory is null)
        {
            throw new ArgumentNullException(nameof(vsLspTagHelperTooltipFactory));
        }

        if (htmlFactsService is null)
        {
            throw new ArgumentNullException(nameof(htmlFactsService));
        }

        _tagHelperFactsService = tagHelperFactsService;
        _lspTagHelperTooltipFactory = lspTagHelperTooltipFactory;
        _vsLspTagHelperTooltipFactory = vsLspTagHelperTooltipFactory;
        _htmlFactsService = htmlFactsService;
    }

    public VSInternalHover? GetHoverInfo(RazorCodeDocument codeDocument, SourceLocation location, VSInternalClientCapabilities clientCapabilities)
    {
        if (codeDocument is null)
        {
            throw new ArgumentNullException(nameof(codeDocument));
        }

        var syntaxTree = codeDocument.GetSyntaxTree();

        var owner = syntaxTree.Root.FindInnermostNode(location.AbsoluteIndex);
        if (owner is null)
        {
            Debug.Fail("Owner should never be null.");
            return null;
        }

        // For cases where the point in the middle of an attribute,
        // such as <any tes$$t=""></any>
        // the node desired is the *AttributeSyntax
        if (owner.Kind is SyntaxKind.MarkupTextLiteral)
        {
            owner = owner.Parent;
        }

        var position = new Position(location.LineIndex, location.CharacterIndex);
        var tagHelperDocumentContext = codeDocument.GetTagHelperContext();

        // We want to find the parent tag, but looking up ancestors in the tree can find other things,
        // for example when hovering over a start tag, the first ancestor is actually the element it
        // belongs to, or in other words, the exact same tag! To work around this we just make sure we
        // only check nodes that are at a different location in the file.
        var ownerStart = owner.SpanStart;
        var ancestors = owner.Ancestors().Where(n => n.SpanStart != ownerStart);
        var (parentTag, parentIsTagHelper) = _tagHelperFactsService.GetNearestAncestorTagInfo(ancestors);

        if (_htmlFactsService.TryGetElementInfo(owner, out var containingTagNameToken, out var attributes) &&
            containingTagNameToken.Span.IntersectsWith(location.AbsoluteIndex))
        {
            // Hovering over HTML tag name
            var stringifiedAttributes = _tagHelperFactsService.StringifyAttributes(attributes);
            var binding = _tagHelperFactsService.GetTagHelperBinding(
                tagHelperDocumentContext,
                containingTagNameToken.Content,
                stringifiedAttributes,
                parentTag: parentTag,
                parentIsTagHelper: parentIsTagHelper);

            if (binding is null)
            {
                // No matching tagHelpers, it's just HTML
                return null;
            }
            else
            {
                Debug.Assert(binding.Descriptors.Any());

                var range = containingTagNameToken.GetRange(codeDocument.Source);

                var result = ElementInfoToHover(binding.Descriptors, range, clientCapabilities);
                return result;
            }
        }

        if (_htmlFactsService.TryGetAttributeInfo(owner, out containingTagNameToken, out _, out var selectedAttributeName, out var selectedAttributeNameLocation, out attributes) &&
            selectedAttributeNameLocation?.IntersectsWith(location.AbsoluteIndex) == true)
        {
            // Hovering over HTML attribute name
            var stringifiedAttributes = _tagHelperFactsService.StringifyAttributes(attributes);

            var binding = _tagHelperFactsService.GetTagHelperBinding(
                tagHelperDocumentContext,
                containingTagNameToken.Content,
                stringifiedAttributes,
                parentTag: parentTag,
                parentIsTagHelper: parentIsTagHelper);

            if (binding is null)
            {
                // No matching TagHelpers, it's just HTML
                return null;
            }
            else
            {
                Debug.Assert(binding.Descriptors.Any());
                var tagHelperAttributes = _tagHelperFactsService.GetBoundTagHelperAttributes(tagHelperDocumentContext, selectedAttributeName, binding);

                // Grab the first attribute that we find that intersects with this location. That way if there are multiple attributes side-by-side aka hovering over:
                //      <input checked| minimized />
                // Then we take the left most attribute (attributes are returned in source order).
                var attribute = attributes.First(a => a.Span.IntersectsWith(location.AbsoluteIndex));
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
                var range = attribute.GetRange(codeDocument.Source);

                // Include the @ in the range
                switch (attribute.Parent.Kind)
                {
                    case SyntaxKind.MarkupTagHelperDirectiveAttribute:
                        var directiveAttribute = (MarkupTagHelperDirectiveAttributeSyntax)attribute.Parent;
                        range.Start.Character -= directiveAttribute.Transition.FullWidth;
                        attributeName = "@" + attributeName;
                        break;
                    case SyntaxKind.MarkupMinimizedTagHelperDirectiveAttribute:
                        var minimizedAttribute = (MarkupMinimizedTagHelperDirectiveAttributeSyntax)containingTagNameToken.Parent;
                        range.Start.Character -= minimizedAttribute.Transition.FullWidth;
                        attributeName = "@" + attributeName;
                        break;
                }

                var attributeHoverModel = AttributeInfoToHover(tagHelperAttributes, range, attributeName, clientCapabilities);

                return attributeHoverModel;
            }
        }

        return null;
    }

    private VSInternalHover? AttributeInfoToHover(ImmutableArray<BoundAttributeDescriptor> boundAttributes, Range range, string attributeName, VSInternalClientCapabilities clientCapabilities)
    {
        var descriptionInfos = boundAttributes.SelectAsArray(boundAttribute =>
        {
            var isIndexer = TagHelperMatchingConventions.SatisfiesBoundAttributeIndexer(attributeName.AsSpan(), boundAttribute);
            return BoundAttributeDescriptionInfo.From(boundAttribute, isIndexer);
        });

        var attrDescriptionInfo = new AggregateBoundAttributeDescription(descriptionInfos);

        var isVSClient = clientCapabilities.SupportsVisualStudioExtensions;
        if (isVSClient && _vsLspTagHelperTooltipFactory.TryCreateTooltip(attrDescriptionInfo, out ContainerElement? classifiedTextElement))
        {
            var vsHover = new VSInternalHover
            {
                Contents = Array.Empty<SumType<string, MarkedString>>(),
                Range = range,
                RawContent = classifiedTextElement,
            };

            return vsHover;
        }
        else
        {
            var hoverContentFormat = GetHoverContentFormat(clientCapabilities);

            if (!_lspTagHelperTooltipFactory.TryCreateTooltip(attrDescriptionInfo, hoverContentFormat, out var vsMarkupContent))
            {
                return null;
            }

            var markupContent = new MarkupContent()
            {
                Value = vsMarkupContent.Value,
                Kind = vsMarkupContent.Kind,
            };

            var hover = new VSInternalHover
            {
                Contents = markupContent,
                Range = range,
            };

            return hover;
        }
    }

    private VSInternalHover? ElementInfoToHover(IEnumerable<TagHelperDescriptor> descriptors, Range range, VSInternalClientCapabilities clientCapabilities)
    {
        var descriptionInfos = descriptors.SelectAsArray(BoundElementDescriptionInfo.From);
        var elementDescriptionInfo = new AggregateBoundElementDescription(descriptionInfos);

        var isVSClient = clientCapabilities.SupportsVisualStudioExtensions;
        if (isVSClient && _vsLspTagHelperTooltipFactory.TryCreateTooltip(elementDescriptionInfo, out ContainerElement? classifiedTextElement))
        {
            var vsHover = new VSInternalHover
            {
                Contents = Array.Empty<SumType<string, MarkedString>>(),
                Range = range,
                RawContent = classifiedTextElement,
            };

            return vsHover;
        }
        else
        {
            var hoverContentFormat = GetHoverContentFormat(clientCapabilities);

            if (!_lspTagHelperTooltipFactory.TryCreateTooltip(elementDescriptionInfo, hoverContentFormat, out var vsMarkupContent))
            {
                return null;
            }

            var markupContent = new MarkupContent()
            {
                Value = vsMarkupContent.Value,
                Kind = vsMarkupContent.Kind,
            };

            var hover = new VSInternalHover
            {
                Contents = markupContent,
                Range = range
            };

            return hover;
        }
    }

    private static VisualStudioMarkupKind GetHoverContentFormat(ClientCapabilities clientCapabilities)
    {
        var hoverContentFormat = clientCapabilities.TextDocument?.Hover?.ContentFormat;
        var hoverKind = hoverContentFormat?.Contains(VisualStudioMarkupKind.Markdown) == true ? VisualStudioMarkupKind.Markdown : VisualStudioMarkupKind.PlainText;
        return hoverKind;
    }
}
