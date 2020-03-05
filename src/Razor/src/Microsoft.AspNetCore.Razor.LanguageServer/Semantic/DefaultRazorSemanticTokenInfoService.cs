// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class DefaultRazorSemanticTokenInfoService : RazorSemanticTokenInfoService
    {
        private readonly SemanticTokenLegend _symanticTokenLegend;

        [ImportingConstructor]
        public DefaultRazorSemanticTokenInfoService()
        {
            _symanticTokenLegend = new SemanticTokenLegend();
        }

        public override SemanticTokens GetSemanticTokens(RazorCodeDocument codeDocument, SourceLocation? location = null)
        {
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }
            var syntaxTree = codeDocument.GetSyntaxTree();

            var syntaxTokens = VisitAllNodes(syntaxTree);

            var semanticTokens =  ConvertSyntaxTokensToSemanticTokens(syntaxTokens, codeDocument, _symanticTokenLegend);

            return semanticTokens;
        }

        private class TagHelperSpanVisitor : SyntaxWalker
        {
            private RazorSourceDocument _source;
            private List<SyntaxNode> _syntaxNodes;

            public TagHelperSpanVisitor(RazorSourceDocument source)
            {
                _source = source;
                _syntaxNodes = new List<SyntaxNode>();
            }

            public IReadOnlyList<SyntaxNode> TagHelperNodes => _syntaxNodes;

            public override void VisitMarkupTagHelperElement(MarkupTagHelperElementSyntax node)
            {
                _syntaxNodes.Add(node.StartTag.Name);

                base.VisitMarkupTagHelperElement(node);

                if (node.EndTag != null)
                {
                    _syntaxNodes.Add(node.EndTag.Name);
                }
            }

            public override void VisitMarkupMinimizedTagHelperAttribute(MarkupMinimizedTagHelperAttributeSyntax node)
            {
                if(node.TagHelperAttributeInfo.Bound)
                {
                    _syntaxNodes.Add(node.Name);
                }

                base.VisitMarkupMinimizedTagHelperAttribute(node);
            }

            public override void VisitMarkupTagHelperAttribute(MarkupTagHelperAttributeSyntax node)
            {
                if(node.TagHelperAttributeInfo.Bound)
                {
                    _syntaxNodes.Add(node.Name);
                }

                base.VisitMarkupTagHelperAttribute(node);
            }
        }

        private static IEnumerable<SyntaxNode> VisitAllNodes(RazorSyntaxTree syntaxTree)
        {
            var visitor = new TagHelperSpanVisitor(syntaxTree.Source);
            visitor.Visit(syntaxTree.Root);

            return visitor.TagHelperNodes;
        }

        private static SemanticTokens ConvertSyntaxTokensToSemanticTokens(
            IEnumerable<SyntaxNode> syntaxTokens,
            RazorCodeDocument razorCodeDocument,
            SemanticTokenLegend semanticTokensLegend)
        {
            SyntaxNode previousToken = null;

            var data = new List<uint>();
            foreach (var token in syntaxTokens)
            {
                var newData = GetData(token, previousToken, razorCodeDocument, semanticTokensLegend);
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
            SyntaxNode currentNode,
            SyntaxNode previousNode,
            RazorCodeDocument razorCodeDocument,
            SemanticTokenLegend legend)
        {
            var previousRange = previousNode?.GetRange(razorCodeDocument);
            var currentRange = currentNode.GetRange(razorCodeDocument);

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
            yield return (uint)currentNode.Span.Length;

            // tokenType
            yield return (uint)GetTokenTypeData(currentNode, legend);

            // tokenModifiers
            yield return (uint)GetTokenModifierData(currentNode, legend);
        }

        private static long GetTokenTypeData(SyntaxNode syntaxToken, SemanticTokenLegend legend)
        {
            switch(syntaxToken.Parent.Kind)
            {
                case SyntaxKind.MarkupTagHelperStartTag:
                case SyntaxKind.MarkupTagHelperEndTag:
                    return legend.TokenTypesLegend["razorTagHelperElement"];
                case SyntaxKind.MarkupTagHelperAttribute:
                case SyntaxKind.MarkupMinimizedTagHelperDirectiveAttribute:
                case SyntaxKind.MarkupTagHelperDirectiveAttribute:
                case SyntaxKind.MarkupMinimizedTagHelperAttribute:
                    return legend.TokenTypesLegend["razorTagHelperAttribute"];
                default:
                    throw new NotImplementedException();
            }
        }

        private static long GetTokenModifierData(SyntaxNode syntaxToken, SemanticTokenLegend legend)
        {
            // Real talk: We're not doing this yet.

            return 0;
        }
    }
}
