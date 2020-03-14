// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Editor.Razor;
using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class DefaultRazorSemanticTokenInfoService : RazorSemanticTokenInfoService
    {
        public DefaultRazorSemanticTokenInfoService()
        {
        }

        public override SemanticTokens GetSemanticTokens(RazorCodeDocument codeDocument, SourceLocation? location = null)
        {
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            var syntaxNodes = VisitAllNodes(codeDocument);

            var semanticTokens = ConvertSyntaxTokensToSemanticTokens(syntaxNodes, codeDocument);

            return semanticTokens;
        }

        private static IEnumerable<(SyntaxNode, SyntaxKind)> VisitAllNodes(RazorCodeDocument razorCodeDocument)
        {
            var visitor = new TagHelperSpanVisitor();
            visitor.Visit(razorCodeDocument.GetSyntaxTree().Root);

            return visitor.TagHelperNodes;
        }

        private static SemanticTokens ConvertSyntaxTokensToSemanticTokens(
            IEnumerable<(SyntaxNode, SyntaxKind)> syntaxTokens,
            RazorCodeDocument razorCodeDocument)
        {
            (SyntaxNode, SyntaxKind)? previousToken = null;

            var data = new List<uint>();
            foreach (var token in syntaxTokens)
            {
                var newData = GetData(token, previousToken, razorCodeDocument);
                data.AddRange(newData);

                previousToken = token;
            }

            return new SemanticTokens
            {
                Data = data
            };
        }

        /**
         * In short, each token takes 5 integers to represent, so a specific token `i` in the file consists of the following array indices:
         *  - at index `5*i`   - `deltaLine`: token line number, relative to the previous token
         *  - at index `5*i+1` - `deltaStart`: token start character, relative to the previous token (relative to 0 or the previous token's start if they are on the same line)
         *  - at index `5*i+2` - `length`: the length of the token. A token cannot be multiline.
         *  - at index `5*i+3` - `tokenType`: will be looked up in `SemanticTokensLegend.tokenTypes`
         *  - at index `5*i+4` - `tokenModifiers`: each set bit will be looked up in `SemanticTokensLegend.tokenModifiers`
        **/
        private static IEnumerable<uint> GetData(
            (SyntaxNode, SyntaxKind) currentNode,
            (SyntaxNode, SyntaxKind)? previousNode,
            RazorCodeDocument razorCodeDocument)
        {
            var previousRange = previousNode?.Item1.GetRange(razorCodeDocument.Source);
            var currentRange = currentNode.Item1.GetRange(razorCodeDocument.Source);

            // deltaLine
            var previousLineIndex = previousNode == null ? 0 : previousRange.Start.Line;
            yield return (uint)(currentRange.Start.Line - previousLineIndex);

            // deltaStart
            if (previousRange != null && previousRange?.Start.Line == currentRange.Start.Line)
            {
                yield return (uint)(currentRange.Start.Character - previousRange.Start.Character);
            }
            else
            {
                yield return (uint)(currentRange.Start.Character);
            }

            // length
            Debug.Assert(currentNode.Item1.Span.Length > 0);
            yield return (uint)currentNode.Item1.Span.Length;

            // tokenType
            yield return GetTokenTypeData(currentNode.Item2);

            // tokenModifiers
            // We don't currently have any need for tokenModifiers
            yield return 0;
        }

        private static uint GetTokenTypeData(SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.MarkupTagHelperDirectiveAttribute:
                case SyntaxKind.MarkupMinimizedTagHelperDirectiveAttribute:
                    return SemanticTokenLegend.TokenTypesLegend[SemanticTokenLegend.RazorDirectiveAttribute];
                case SyntaxKind.MarkupTagHelperStartTag:
                case SyntaxKind.MarkupTagHelperEndTag:
                    return SemanticTokenLegend.TokenTypesLegend[SemanticTokenLegend.RazorTagHelperElement];
                case SyntaxKind.MarkupTagHelperAttribute:
                case SyntaxKind.MarkupMinimizedTagHelperAttribute:
                    return SemanticTokenLegend.TokenTypesLegend[SemanticTokenLegend.RazorTagHelperAttribute];
                case SyntaxKind.Transition:
                    return SemanticTokenLegend.TokenTypesLegend[SemanticTokenLegend.RazorTransition];
                case SyntaxKind.Colon:
                    return SemanticTokenLegend.TokenTypesLegend[SemanticTokenLegend.RazorDirectiveColon];
                default:
                    throw new NotImplementedException();
            }
        }

        private class TagHelperSpanVisitor : Language.Syntax.SyntaxWalker
        {
            private readonly List<(SyntaxNode, SyntaxKind)> _syntaxNodes;

            public TagHelperSpanVisitor()
            {
                _syntaxNodes = new List<(SyntaxNode, SyntaxKind)>();
            }

            public IReadOnlyList<(SyntaxNode, SyntaxKind)> TagHelperNodes => _syntaxNodes;

            public override void VisitMarkupTagHelperStartTag(MarkupTagHelperStartTagSyntax node)
            {
                if (IsTagHelper((MarkupTagHelperElementSyntax)node.Parent))
                {
                    _syntaxNodes.Add((node.Name, SyntaxKind.MarkupTagHelperStartTag));
                }
                base.VisitMarkupTagHelperStartTag(node);
            }

            public override void VisitMarkupTagHelperEndTag(MarkupTagHelperEndTagSyntax node)
            {
                if (IsTagHelper((MarkupTagHelperElementSyntax)node.Parent))
                {
                    _syntaxNodes.Add((node.Name, SyntaxKind.MarkupTagHelperEndTag));
                }
                base.VisitMarkupTagHelperEndTag(node);
            }

            public override void VisitMarkupMinimizedTagHelperAttribute(MarkupMinimizedTagHelperAttributeSyntax node)
            {
                if (node.TagHelperAttributeInfo.Bound)
                {
                    _syntaxNodes.Add((node.Name, SyntaxKind.MarkupMinimizedTagHelperAttribute));
                }

                base.VisitMarkupMinimizedTagHelperAttribute(node);
            }

            public override void VisitMarkupTagHelperAttribute(MarkupTagHelperAttributeSyntax node)
            {
                if (node.TagHelperAttributeInfo.Bound)
                {
                    _syntaxNodes.Add((node.Name, SyntaxKind.MarkupTagHelperAttribute));
                }

                base.VisitMarkupTagHelperAttribute(node);
            }

            public override void VisitMarkupTagHelperDirectiveAttribute(MarkupTagHelperDirectiveAttributeSyntax node)
            {
                if (node.TagHelperAttributeInfo.Bound)
                {
                    _syntaxNodes.Add((node.Transition, SyntaxKind.Transition));
                    _syntaxNodes.Add((node.Name, SyntaxKind.MarkupTagHelperDirectiveAttribute));

                    if (node.Colon != null && node.ParameterName != null)
                    {
                        _syntaxNodes.Add((node.Colon, SyntaxKind.Colon));
                        _syntaxNodes.Add((node.ParameterName, SyntaxKind.MarkupTagHelperDirectiveAttribute));
                    }
                }

                base.VisitMarkupTagHelperDirectiveAttribute(node);
            }

            public override void VisitMarkupMinimizedTagHelperDirectiveAttribute(MarkupMinimizedTagHelperDirectiveAttributeSyntax node)
            {
                if (node.TagHelperAttributeInfo.Bound)
                {
                    _syntaxNodes.Add((node.Transition, SyntaxKind.Transition));
                    _syntaxNodes.Add((node.Name, SyntaxKind.MarkupMinimizedTagHelperDirectiveAttribute));

                    if (node.Colon != null && node.ParameterName != null)
                    {
                        _syntaxNodes.Add((node.Colon, SyntaxKind.Colon));
                        _syntaxNodes.Add((node.ParameterName, SyntaxKind.MarkupMinimizedTagHelperDirectiveAttribute));
                    }
                }

                base.VisitMarkupMinimizedTagHelperDirectiveAttribute(node);
            }

            private bool IsTagHelper(MarkupTagHelperElementSyntax node)
            {
                var name = node.StartTag.Name.Content;
                if (IsHtmlTagName(name))
                {
                    var binding = node.TagHelperInfo.BindingResult;
                    var isCaseSensitive = binding.Descriptors.All(thd => thd.CaseSensitive);
                    return isCaseSensitive && name.Any(c => char.IsUpper(c));
                }

                return true;
            }

            private bool IsHtmlTagName(string name)
            {
                return HtmlFactsService.HtmlSchemaTagNames.Contains(name);
            }
        }
    }
}
