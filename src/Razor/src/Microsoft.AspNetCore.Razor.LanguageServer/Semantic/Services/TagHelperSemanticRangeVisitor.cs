// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.VisualStudio.Editor.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class TagHelperSemanticRangeVisitor : SyntaxWalker
    {
        private readonly List<SemanticRange> _semanticRanges;
        private readonly RazorCodeDocument _razorCodeDocument;
        private readonly Range _range;

        private TagHelperSemanticRangeVisitor(RazorCodeDocument razorCodeDocument, Range range)
        {
            _semanticRanges = new List<SemanticRange>();
            _razorCodeDocument = razorCodeDocument;
            _range = range;
        }

        public static IReadOnlyList<SemanticRange> VisitAllNodes(RazorCodeDocument razorCodeDocument, Range range = null)
        {
            var visitor = new TagHelperSemanticRangeVisitor(razorCodeDocument, range);

            visitor.Visit(razorCodeDocument.GetSyntaxTree().Root);

            return visitor._semanticRanges;
        }

        public override void VisitRazorCommentBlock(RazorCommentBlockSyntax node)
        {
            AddSemanticRange(node, SyntaxKind.RazorComment);
            base.VisitRazorCommentBlock(node);
        }

        public override void VisitRazorDirective(RazorDirectiveSyntax node)
        {
            AddSemanticRange(node.Transition, SyntaxKind.Transition);
            base.VisitRazorDirective(node);
        }

        public override void VisitRazorDirectiveBody(RazorDirectiveBodySyntax node)
        {
            AddSemanticRange(node.Keyword, SyntaxKind.RazorDirective);
            base.VisitRazorDirectiveBody(node);
        }

        public override void VisitMarkupTagHelperStartTag(MarkupTagHelperStartTagSyntax node)
        {
            if (ClassifyTagName((MarkupTagHelperElementSyntax)node.Parent))
            {
                AddSemanticRange(node.Name, SyntaxKind.MarkupTagHelperStartTag);
            }

            base.VisitMarkupTagHelperStartTag(node);
        }

        public override void VisitMarkupTagHelperEndTag(MarkupTagHelperEndTagSyntax node)
        {
            if (ClassifyTagName((MarkupTagHelperElementSyntax)node.Parent))
            {
                AddSemanticRange(node.Name, SyntaxKind.MarkupTagHelperEndTag);
            }

            base.VisitMarkupTagHelperEndTag(node);
        }

        public override void VisitMarkupMinimizedTagHelperAttribute(MarkupMinimizedTagHelperAttributeSyntax node)
        {
            if (node.TagHelperAttributeInfo.Bound)
            {
                AddSemanticRange(node.Name, SyntaxKind.MarkupMinimizedTagHelperAttribute);
            }

            base.VisitMarkupMinimizedTagHelperAttribute(node);
        }

        public override void VisitMarkupTagHelperAttribute(MarkupTagHelperAttributeSyntax node)
        {
            if (node.TagHelperAttributeInfo.Bound)
            {
                AddSemanticRange(node.Name, SyntaxKind.MarkupTagHelperAttribute);
            }

            base.VisitMarkupTagHelperAttribute(node);
        }

        public override void VisitMarkupTagHelperDirectiveAttribute(MarkupTagHelperDirectiveAttributeSyntax node)
        {
            if (node.TagHelperAttributeInfo.Bound)
            {
                AddSemanticRange(node.Transition, SyntaxKind.Transition);
                AddSemanticRange(node.Name, SyntaxKind.MarkupTagHelperDirectiveAttribute);

                if (node.Colon != null)
                {
                    AddSemanticRange(node.Colon, SyntaxKind.Colon);
                }

                if (node.ParameterName != null)
                {
                    AddSemanticRange(node.ParameterName, SyntaxKind.MarkupTagHelperDirectiveAttribute);
                }
            }

            base.VisitMarkupTagHelperDirectiveAttribute(node);
        }

        public override void VisitMarkupMinimizedTagHelperDirectiveAttribute(MarkupMinimizedTagHelperDirectiveAttributeSyntax node)
        {
            if (node.TagHelperAttributeInfo.Bound)
            {
                AddSemanticRange(node.Transition, SyntaxKind.Transition);
                AddSemanticRange(node.Name, SyntaxKind.MarkupMinimizedTagHelperDirectiveAttribute);

                if (node.Colon != null)
                {
                    AddSemanticRange(node.Colon, SyntaxKind.Colon);
                }

                if (node.ParameterName != null)
                {
                    AddSemanticRange(node.ParameterName, SyntaxKind.MarkupMinimizedTagHelperDirectiveAttribute);
                }
            }

            base.VisitMarkupMinimizedTagHelperDirectiveAttribute(node);
        }

        // We don't want to classify TagNames of well-known HTML
        // elements as TagHelpers (even if they are). So the 'input' in`<input @onclick='...' />`
        // needs to not be marked as a TagHelper, but `<Input @onclick='...' />` should be.
        private bool ClassifyTagName(MarkupTagHelperElementSyntax node)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (node.StartTag != null && node.StartTag.Name != null)
            {
                var name = node.StartTag.Name.Content;

                if (!HtmlFactsService.IsHtmlTagName(name))
                {
                    // We always classify non-HTML tag names as TagHelpers if they're within a MarkupTagHelperElementSyntax
                    return true;
                }

                // This must be a well-known HTML tag name like 'input', 'br'.

                var binding = node.TagHelperInfo.BindingResult;
                foreach (var descriptor in binding.Descriptors)
                {
                    if (!descriptor.IsComponentTagHelper())
                    {
                        return false;
                    }
                }

                if (name.Length > 0 && char.IsUpper(name[0]))
                {
                    // pascal cased Component TagHelper tag name such as <Input>
                    return true;
                }
            }

            return false;
        }

        private void AddSemanticRange(SyntaxNode node, SyntaxKind kind)
        {
            int semanticKind;
            switch (kind)
            {
                case SyntaxKind.MarkupTagHelperDirectiveAttribute:
                case SyntaxKind.MarkupMinimizedTagHelperDirectiveAttribute:
                    semanticKind = RazorSemanticTokensLegend.TokenTypesLegend[RazorSemanticTokensLegend.RazorDirectiveAttribute];
                    break;
                case SyntaxKind.MarkupTagHelperStartTag:
                case SyntaxKind.MarkupTagHelperEndTag:
                    semanticKind = RazorSemanticTokensLegend.TokenTypesLegend[RazorSemanticTokensLegend.RazorTagHelperElement];
                    break;
                case SyntaxKind.MarkupTagHelperAttribute:
                case SyntaxKind.MarkupMinimizedTagHelperAttribute:
                    semanticKind = RazorSemanticTokensLegend.TokenTypesLegend[RazorSemanticTokensLegend.RazorTagHelperAttribute];
                    break;
                case SyntaxKind.Transition:
                    semanticKind = RazorSemanticTokensLegend.TokenTypesLegend[RazorSemanticTokensLegend.RazorTransition];
                    break;
                case SyntaxKind.Colon:
                    semanticKind = RazorSemanticTokensLegend.TokenTypesLegend[RazorSemanticTokensLegend.RazorDirectiveColon];
                    break;
                case SyntaxKind.RazorDirective:
                    semanticKind = RazorSemanticTokensLegend.TokenTypesLegend[RazorSemanticTokensLegend.RazorDirective];
                    break;
                case SyntaxKind.RazorComment:
                    semanticKind = RazorSemanticTokensLegend.TokenTypesLegend[RazorSemanticTokensLegend.RazorComment];
                    break;
                default:
                    throw new NotImplementedException();
            }

            var source = _razorCodeDocument.Source;
            var range = node.GetRange(source);

            var semanticRange = new SemanticRange(semanticKind, range);

            if (_range is null || semanticRange.Range.OverlapsWith(_range))
            {
                _semanticRanges.Add(semanticRange);
            }
        }
    }
}
