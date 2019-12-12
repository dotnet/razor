// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.VisualStudio.Editor.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using HoverModel = OmniSharp.Extensions.LanguageServer.Protocol.Models.Hover;
using RangeModel = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hover
{
    internal class DefaultRazorHoverInfoService : RazorHoverInfoService
    {
        private readonly TagHelperFactsService _tagHelperFactsService;
        private readonly TagHelperDescriptionFactory _tagHelperDescriptionFactory;
        private readonly HtmlFactsService _htmlFactsService;

        [ImportingConstructor]
        public DefaultRazorHoverInfoService(
            TagHelperFactsService tagHelperFactsService,
            TagHelperDescriptionFactory tagHelperDescriptionFactory,
            HtmlFactsService htmlFactsService)
        {
            if (tagHelperFactsService is null)
            {
                throw new ArgumentNullException(nameof(tagHelperFactsService));
            }

            if (tagHelperDescriptionFactory is null)
            {
                throw new ArgumentNullException(nameof(tagHelperDescriptionFactory));
            }

            if (htmlFactsService is null)
            {
                throw new ArgumentNullException(nameof(htmlFactsService));
            }

            _tagHelperFactsService = tagHelperFactsService;
            _tagHelperDescriptionFactory = tagHelperDescriptionFactory;
            _htmlFactsService = htmlFactsService;
        }

        public override HoverModel GetHoverInfo(RazorCodeDocument codeDocument, SourceSpan location)
        {
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            var syntaxTree = codeDocument.GetSyntaxTree();
            var change = new SourceChange(location, "");
            var owner = syntaxTree.Root.LocateOwner(change);

            if (owner == null)
            {
                Debug.Fail("Owner should never be null.");
                return null;
            }

            var parent = owner.Parent;
            var position = new Position(location.LineIndex, location.CharacterIndex);
            var tagHelperDocumentContext = codeDocument.GetTagHelperContext();

            if (_htmlFactsService.TryGetElementInfo(parent, out var containingTagNameToken, out var attributes) &&
                containingTagNameToken.Span.IntersectsWith(location.AbsoluteIndex))
            {
                // Hovering over HTML tag name
                var stringifiedAttributes = _tagHelperFactsService.StringifyAttributes(attributes);
                var binding = _tagHelperFactsService.GetTagHelperBinding(
                    tagHelperDocumentContext,
                    containingTagNameToken.Content,
                    stringifiedAttributes,
                    parentTag: null,
                    parentIsTagHelper: false);

                if (binding is null)
                {
                    // No matching tagHelpers, it's just HTML
                    return null;
                }
                else
                {
                    Debug.Assert(binding.Descriptors.Count() > 0);

                    var result = ElementInfoToHover(binding.Descriptors, position);
                    return result;
                }
            }

            if (_htmlFactsService.TryGetAttributeInfo(parent, out containingTagNameToken, out var selectedAttributeName, out attributes) &&
                attributes.Span.IntersectsWith(location.AbsoluteIndex))
            {
                // Hovering over HTML attribute name
                var stringifiedAttributes = _tagHelperFactsService.StringifyAttributes(attributes);

                var binding = _tagHelperFactsService.GetTagHelperBinding(
                    tagHelperDocumentContext,
                    containingTagNameToken.Content,
                    stringifiedAttributes,
                    parentTag: null,
                    parentIsTagHelper: false);

                var tagHelperAttributes = _tagHelperFactsService.GetBoundTagHelperAttributes(tagHelperDocumentContext, selectedAttributeName, binding);
                var attributeHoverModel = AttributeInfoToHover(tagHelperAttributes, position);

                return attributeHoverModel;
            }

            return null;
        }

        private HoverModel AttributeInfoToHover(IEnumerable<BoundAttributeDescriptor> descriptors, Position position)
        {
            var descriptionInfos = descriptors.Select(d => new TagHelperAttributeDescriptionInfo(d.DisplayName, d.GetPropertyName(), d.TypeName, d.Documentation))
                .ToList()
                .AsReadOnly();
            var attrDescriptionInfo = new AttributeDescriptionInfo(descriptionInfos);

            if(!_tagHelperDescriptionFactory.TryCreateDescription(attrDescriptionInfo, out var markdown))
            {
                return null;
            }

            var hover = new HoverModel {
                Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = markdown
                }),
                Range = new RangeModel(start: position, end: position)
            };

            return hover;
        }

        private HoverModel ElementInfoToHover(IEnumerable<TagHelperDescriptor> descriptors, Position position)
        {
            var descriptionInfos = descriptors.Select(d => new TagHelperDescriptionInfo(d.DisplayName, d.Documentation))
                .ToList()
                .AsReadOnly();
            var elementDescriptionInfo = new ElementDescriptionInfo(descriptionInfos);

            _tagHelperDescriptionFactory.TryCreateDescription(elementDescriptionInfo, out var markdown);

            var hover = new HoverModel
            {
                Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = markdown,
                }),
                Range = new RangeModel(start: position, end: position)
            };

            return hover;
        }
    }
}
