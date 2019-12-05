// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.Completion;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using HoverModel = OmniSharp.Extensions.LanguageServer.Protocol.Models.Hover;
using RangeModel = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hover
{
    [Shared]
    [Export(typeof(RazorHoverInfoService))]
    internal class DefaultRazorHoverInfoService : RazorHoverInfoService
    {
        private readonly RazorCompletionFactsService _razorCompletionFactsService;

        [ImportingConstructor]
        public DefaultRazorHoverInfoService(RazorCompletionFactsService razorCompletionFactsService)
        {
            if (razorCompletionFactsService is null)
            {
                throw new ArgumentNullException(nameof(razorCompletionFactsService));
            }

            _razorCompletionFactsService = razorCompletionFactsService;
        }

        public override HoverModel GetHoverInfo(RazorSyntaxTree syntaxTree, TagHelperDocumentContext tagHelperDocumentContext, SourceSpan location)
        {
            if (syntaxTree is null)
            {
                throw new ArgumentNullException(nameof(syntaxTree));
            }

            if (tagHelperDocumentContext is null)
            {
                throw new ArgumentNullException(nameof(tagHelperDocumentContext));
            }

            var position = new Position(location.LineIndex, location.CharacterIndex);
            var range = new RangeModel(start: position, end: position);

            return new HoverModel
            {
                Contents = new MarkedStringsOrMarkupContent(new MarkedString("Hello World!")),
                Range = range
            };

            var items = _razorCompletionFactsService.GetCompletionItems(syntaxTree, tagHelperDocumentContext, location);

            Debug.Assert(items.Count == 1);
            var item = items[0];

            if (Convert(item, location, out var hoverModel))
            {
                return hoverModel;
            }

            throw new NotImplementedException();
        }

        private static bool Convert(RazorCompletionItem razorCompletionItem, SourceSpan location, out HoverModel hoverModel)
        {
            switch(razorCompletionItem.Kind)
            {
                case RazorCompletionItemKind.Directive:
                    var descriptionInfo = razorCompletionItem.GetDirectiveCompletionDescription();
                    var postion = new Position(location.LineIndex, location.CharacterIndex);
                    hoverModel = new HoverModel()
                    {
                        Contents = new MarkedStringsOrMarkupContent(
                            new MarkupContent {
                                Kind = MarkupKind.Markdown,
                                Value = descriptionInfo.Description
                            }),
                        Range = new RangeModel(start: postion, end: postion)
                    };
                    return true;
                case RazorCompletionItemKind.DirectiveAttribute:
                    throw new NotImplementedException();
                    return false;
                case RazorCompletionItemKind.DirectiveAttributeParameter:
                    throw new NotImplementedException();
                    return false;
                default:
                    throw new NotImplementedException("There's a new ItemKind that we don't handle!");
            }
        }
    }
}
