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

        private static IEnumerable<SyntaxNode> VisitAllNodes(RazorSyntaxTree syntaxTree)
        {
            return VisitNode(syntaxTree.Root);
        }

        private static IReadOnlyList<SyntaxNode> VisitNode(SyntaxNode syntaxNode)
        {
            var result = new List<SyntaxNode>();

            if (syntaxNode is null)
            {
                return result;
            }

            switch(syntaxNode.Kind)
            {
                case SyntaxKind.MarkupMinimizedTagHelperDirectiveAttribute:
                    var minimized = (MarkupMinimizedTagHelperDirectiveAttributeSyntax)syntaxNode;
                    result.Add(minimized.Name);
                    break;
                case SyntaxKind.MarkupTagHelperDirectiveAttribute:
                    var directive = (MarkupTagHelperDirectiveAttributeSyntax)syntaxNode;
                    result.Add(directive.Name);
                    break;
                case SyntaxKind.MarkupTagHelperStartTag:
                    var startTag = (MarkupTagHelperStartTagSyntax)syntaxNode;
                    result.Add(startTag.Name);
                    break;
                case SyntaxKind.MarkupTagHelperEndTag:
                    var endTag = (MarkupTagHelperEndTagSyntax)syntaxNode;
                    result.Add(endTag.Name);
                    break;
                case SyntaxKind.MarkupMinimizedTagHelperAttribute:
                    var minimizedAttributeTag = (MarkupMinimizedTagHelperAttributeSyntax)syntaxNode;
                    if (minimizedAttributeTag.TagHelperAttributeInfo.Bound)
                    {
                        result.Add(minimizedAttributeTag.Name);
                    }
                    break;
                case SyntaxKind.MarkupTagHelperAttribute:
                    var tagHelperAttributeTag = (MarkupTagHelperAttributeSyntax)syntaxNode;
                    if(tagHelperAttributeTag.TagHelperAttributeInfo.Bound)
                    {
                        result.Add(tagHelperAttributeTag.Name);
                    }
                    break;
                default:
                    break;
            }

            var children = syntaxNode.ChildNodes();
            foreach (var child in children)
            {
                result.AddRange(VisitNode(child));
            }

            return result;
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
