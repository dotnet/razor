// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.VisualStudio.Editor.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class TagHelperSemanticSpanVisitor : SyntaxWalker
    {
        private readonly List<SyntaxResult> _syntaxNodes;
        private readonly RazorCodeDocument _razorCodeDocument;
        private readonly Range _range;

        public TagHelperSemanticSpanVisitor(RazorCodeDocument razorCodeDocument, Range range)
        {
            _syntaxNodes = new List<SyntaxResult>();
            _razorCodeDocument = razorCodeDocument;
            _range = range;
        }

        public IReadOnlyList<SyntaxResult> TagHelperData => _syntaxNodes;

        public static IReadOnlyList<SyntaxResult> VisitAllNodes(RazorCodeDocument razorCodeDocument, Range range = null)
        {
            var visitor = new TagHelperSemanticSpanVisitor(razorCodeDocument, range);

            visitor.Visit(razorCodeDocument.GetSyntaxTree().Root);

            return visitor.TagHelperData;
        }

        public override void VisitMarkupTagHelperStartTag(MarkupTagHelperStartTagSyntax node)
        {
            if (ClassifyTagName((MarkupTagHelperElementSyntax)node.Parent))
            {
                var result = new SyntaxResult(node.Name, SyntaxKind.MarkupTagHelperStartTag, _razorCodeDocument);
                AddNode(result);
            }
            base.VisitMarkupTagHelperStartTag(node);
        }

        public override void VisitMarkupTagHelperEndTag(MarkupTagHelperEndTagSyntax node)
        {
            if (ClassifyTagName((MarkupTagHelperElementSyntax)node.Parent))
            {
                var result = new SyntaxResult(node.Name, SyntaxKind.MarkupTagHelperEndTag, _razorCodeDocument);
                AddNode(result);
            }
            base.VisitMarkupTagHelperEndTag(node);
        }

        public override void VisitMarkupMinimizedTagHelperAttribute(MarkupMinimizedTagHelperAttributeSyntax node)
        {
            if (node.TagHelperAttributeInfo.Bound)
            {
                var result = new SyntaxResult(node.Name, SyntaxKind.MarkupMinimizedTagHelperAttribute, _razorCodeDocument);
                AddNode(result);
            }

            base.VisitMarkupMinimizedTagHelperAttribute(node);
        }

        public override void VisitMarkupTagHelperAttribute(MarkupTagHelperAttributeSyntax node)
        {
            if (node.TagHelperAttributeInfo.Bound)
            {
                var result = new SyntaxResult(node.Name, SyntaxKind.MarkupTagHelperAttribute, _razorCodeDocument);
                AddNode(result);
            }

            base.VisitMarkupTagHelperAttribute(node);
        }

        public override void VisitMarkupTagHelperDirectiveAttribute(MarkupTagHelperDirectiveAttributeSyntax node)
        {
            if (node.TagHelperAttributeInfo.Bound)
            {
                var transition = new SyntaxResult(node.Transition, SyntaxKind.Transition, _razorCodeDocument);
                AddNode(transition);

                var directiveAttribute = new SyntaxResult(node.Name, SyntaxKind.MarkupTagHelperDirectiveAttribute, _razorCodeDocument);
                AddNode(directiveAttribute);

                if (node.Colon != null)
                {
                    var colon = new SyntaxResult(node.Colon, SyntaxKind.Colon, _razorCodeDocument);
                    AddNode(colon);
                }

                if (node.ParameterName != null)
                {
                    var parameterName = new SyntaxResult(node.ParameterName, SyntaxKind.MarkupTagHelperDirectiveAttribute, _razorCodeDocument);
                    AddNode(parameterName);
                }
            }

            base.VisitMarkupTagHelperDirectiveAttribute(node);
        }

        public override void VisitMarkupMinimizedTagHelperDirectiveAttribute(MarkupMinimizedTagHelperDirectiveAttributeSyntax node)
        {

            if (node.TagHelperAttributeInfo.Bound)
            {
                var transition = new SyntaxResult(node.Transition, SyntaxKind.Transition, _razorCodeDocument);
                AddNode(transition);

                var directiveAttribute = new SyntaxResult(node.Name, SyntaxKind.MarkupMinimizedTagHelperDirectiveAttribute, _razorCodeDocument);
                AddNode(directiveAttribute);

                if (node.Colon != null)
                {
                    var colon = new SyntaxResult(node.Colon, SyntaxKind.Colon, _razorCodeDocument);
                    AddNode(colon);
                }

                if (node.ParameterName != null)
                {
                    var parameterName = new SyntaxResult(node.ParameterName, SyntaxKind.MarkupMinimizedTagHelperDirectiveAttribute, _razorCodeDocument);

                    AddNode(parameterName);
                }
            }

            base.VisitMarkupMinimizedTagHelperDirectiveAttribute(node);
        }

        private void AddNode(SyntaxResult syntaxResult)
        {
            if (_range is null || syntaxResult.Range.OverlapsWith(_range))
            {
                _syntaxNodes.Add(syntaxResult);
            }
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
    }
}
