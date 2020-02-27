// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.VisualStudio.Editor.Razor;
using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class DefaultRazorSemanticTokenInfoService : RazorSemanticTokenInfoService
    {
        private readonly TagHelperFactsService _tagHelperFactsService;
        private readonly TagHelperDescriptionFactory _tagHelperDescriptionFactory;
        private readonly HtmlFactsService _htmlFactsService;
        private readonly SemanticTokenLegend _symanticTokenLegend;

        [ImportingConstructor]
        public DefaultRazorSemanticTokenInfoService(
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

            _symanticTokenLegend = new SemanticTokenLegend();
        }

        public override SemanticTokens GetSemanticTokens(RazorCodeDocument codeDocument, SourceLocation? location = null)
        {
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }
            var syntaxTree = codeDocument.GetSyntaxTree();

            var tagHelperSpans = syntaxTree.GetTagHelperSpans();

            var syntaxTokens = VisitAllNodes(syntaxTree);
            syntaxTokens = syntaxTokens
                .Where(token => tagHelperSpans
                    .Any(tag => token.Span.IntersectsWith(new TextSpan(tag.Span.AbsoluteIndex, tag.Span.Length))));

            var elements = GetElementItem(tagHelperSpans, syntaxTree);

            var semanticTokens =  ConvertSyntaxTokensToSemanticTokens(elements, codeDocument, _symanticTokenLegend);

            return semanticTokens;
        }

        private IEnumerable<SyntaxToken> GetElementItem(IEnumerable<TagHelperSpanInternal> tokens, RazorSyntaxTree syntaxTree)
        {
            var result = new List<SyntaxToken>();
            foreach (var token in tokens)
            {
                var change = new SourceChange(token.Span.AbsoluteIndex, length: 0, newText: "");
                var owner = syntaxTree.Root.LocateOwner(change);

                if (owner == null)
                {
                    Debug.Fail("Owner should never be null.");
                    throw new NotImplementedException();
                }
                var parent = owner.Parent;

                if (_htmlFactsService.TryGetElementInfo(parent, out var containingTagNameToken, out var attributes))
                {
                    result.Add(containingTagNameToken);
                    var tagHelperAttributes = attributes.Where(node => node.GetType() == typeof(MarkupTagHelperAttributeSyntax)).Select(node => ((MarkupTagHelperAttributeSyntax)node).Name.LiteralTokens[0]);
                    //throw new NotImplementedException();
                    result.AddRange(tagHelperAttributes);
                }
            }

            return result;
        }

        //private IEnumerable<SyntaxToken> GetElementItem(IEnumerable<SyntaxToken> tokens, RazorSyntaxTree syntaxTree)
        //{
        //    var result = new List<SyntaxToken>();
        //    foreach(var token in tokens)
        //    {
        //        var change = new SourceChange(token.FullSpan.Start, length: 0, newText: "");
        //        var owner = syntaxTree.Root.LocateOwner(change);

        //        if (owner == null)
        //        {
        //            Debug.Fail("Owner should never be null.");
        //            throw new NotImplementedException();
        //        }
        //        var parent = owner.Parent;

        //        if (_htmlFactsService.TryGetElementInfo(parent, out var containingTagNameToken, out var attributes))
        //        {
        //            result.Add(containingTagNameToken);
        //            var tagHelperAttributes = attributes.Where(node => node.GetType() == typeof(MarkupTagHelperAttributeSyntax)).Select(node => ((MarkupTagHelperAttributeSyntax)node).Name.LiteralTokens[0]);
        //            //throw new NotImplementedException();
        //            result.AddRange(tagHelperAttributes);
        //        }
        //    }

        //    return result;
        //}

        private static IEnumerable<SyntaxToken> VisitAllNodes(RazorSyntaxTree syntaxTree)
        {
            return VisitNode(syntaxTree.Root);
        }

        private static IReadOnlyList<SyntaxToken> VisitNode(SyntaxNode syntaxNode)
        {
            var result = new List<SyntaxToken>();

            if (syntaxNode is null)
            {
                return result;
            }

            if (syntaxNode.IsToken)
            {
                result.Add((SyntaxToken)syntaxNode);
            }

            var children = syntaxNode.ChildNodes();
            foreach (var child in children)
            {
                result.AddRange(VisitNode(child));
            }

            return result;
        }

        private static SemanticTokens ConvertSyntaxTokensToSemanticTokens(
            IEnumerable<SyntaxToken> syntaxTokens,
            RazorCodeDocument razorCodeDocument,
            SemanticTokenLegend semanticTokensLegend)
        {
            SyntaxToken previousToken = null;

            var data = new List<long>();
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
        private static IEnumerable<long> GetData(
            SyntaxToken currentNode,
            SyntaxToken previousNode,
            RazorCodeDocument razorCodeDocument,
            SemanticTokenLegend legend)
        {
            var previousRange = previousNode?.GetRange(razorCodeDocument);
            var currentRange = currentNode.GetRange(razorCodeDocument);

            // deltaLine
            var previousLineIndex = previousNode == null ? 0 : previousRange.Start.Line;
            yield return currentRange.Start.Line - previousLineIndex;

            // deltaStart
            if (previousRange != null && previousRange?.Start.Line == currentRange.Start.Line)
            {
                yield return currentRange.Start.Character - previousRange.Start.Character;
            }
            else
            {
                yield return currentRange.Start.Character;
            }

            // length
            yield return currentNode.Span.Length;

            // tokenType]
            yield return GetTokenTypeData(currentNode, legend);

            // tokenModifiers
            yield return GetTokenModifierData(currentNode, legend);
        }

        private static long GetTokenTypeData(SyntaxToken syntaxToken, SemanticTokenLegend legend)
        {
            var type = syntaxToken.Parent.GetType();
            if (typeof(MarkupTagHelperStartTagSyntax) == syntaxToken.Parent.GetType())
            {
                return legend.TokenTypesLegend["razorTagHelperElement"];
            }
            else if (typeof(MarkupTagHelperAttributeSyntax) == syntaxToken.Parent.Parent.GetType())
            {
                return legend.TokenTypesLegend["razorTagHelperAttribute"];
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static long GetTokenModifierData(SyntaxToken syntaxToken, SemanticTokenLegend legend)
        {
            // Real talk: We're not doing this yet.

            return 0;
        }
    }
}
