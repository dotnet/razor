// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hover
{
    [Shared]
    [Export(typeof(RazorHoverInfoService))]
    internal class DefaultRazorHoverInfoService : RazorHoverInfoService
    {
        private readonly TagHelperFactsService _tagHelperFactsService;
        private readonly TagHelperDescriptionFactory _tagHelperDescriptionFactory;

        [ImportingConstructor]
        public DefaultRazorHoverInfoService(TagHelperFactsService tagHelperFactsService, TagHelperDescriptionFactory tagHelperDescriptionFactory)
        {
            if (tagHelperFactsService is null)
            {
                throw new ArgumentNullException(nameof(tagHelperFactsService));
            }

            if (tagHelperDescriptionFactory is null)
            {
                throw new ArgumentNullException(nameof(tagHelperDescriptionFactory));
            }

            _tagHelperFactsService = tagHelperFactsService;
            _tagHelperDescriptionFactory = tagHelperDescriptionFactory;
        }

        public override ExpandedHoverModel GetHoverInfo(RazorCodeDocument codeDocument, TagHelperDocumentContext tagHelperDocumentContext, SourceSpan location)
        {
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            if (tagHelperDocumentContext is null)
            {
                throw new ArgumentNullException(nameof(tagHelperDocumentContext));
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

            if (TagHelperStaticMethods.TryGetElementInfo(parent, out var containingTagNameToken, out var attributes) &&
                containingTagNameToken.Span.IntersectsWith(location.AbsoluteIndex))
            {
                var stringifiedAttributes = TagHelperStaticMethods.StringifyAttributes(attributes);
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

                    return ElementInfoToHover(binding.Descriptors.First(), position);
                }
            }

            if (TagHelperStaticMethods.TryGetAttributeInfo(parent, out containingTagNameToken, out var selectedAttributeName, out attributes) &&
                attributes.Span.IntersectsWith(location.AbsoluteIndex))
            {
                var stringifiedAttributes = TagHelperStaticMethods.StringifyAttributes(attributes);

                var binding = _tagHelperFactsService.GetTagHelperBinding(
                    tagHelperDocumentContext,
                    containingTagNameToken.Content,
                    stringifiedAttributes,
                    parentTag: null,
                    parentIsTagHelper: false);

                var tagHelperAttrs = _tagHelperFactsService.GetBoundTagHelperAttributes(tagHelperDocumentContext, selectedAttributeName, binding);
                var attrHoverModel = AttributeInfoToHover(tagHelperAttrs.First(), position);

                return attrHoverModel;
            }

            return null;
        }

        private ExpandedHoverModel AttributeInfoToHover(BoundAttributeDescriptor descriptor, Position position)
        {
            var attrDescriptionInfo = new AttributeDescriptionInfo(
                new[] {
                    new TagHelperAttributeDescriptionInfo(descriptor.DisplayName, descriptor.GetPropertyName(), descriptor.TypeName, descriptor.Documentation)
                });

            _tagHelperDescriptionFactory.TryCreateDescription(attrDescriptionInfo, out var markdown);

            var hover = new ExpandedHoverModel {
                Name = descriptor.DisplayName,
                Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = markdown
                }),
                Range = new RangeModel(start: position, end: position)
            };

            return hover;
        }

        private ExpandedHoverModel ElementInfoToHover(TagHelperDescriptor descriptor, Position position)
        {
            var elementDescriptionInfo = new ElementDescriptionInfo(
                new [] {
                    new TagHelperDescriptionInfo(descriptor.DisplayName, descriptor.Documentation)
                });

            _tagHelperDescriptionFactory.TryCreateDescription(elementDescriptionInfo, out var markdown);
            var hover = new ExpandedHoverModel
            {
                Name = descriptor.DisplayName,
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
