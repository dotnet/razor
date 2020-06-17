// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal partial class DefaultRazorSemanticTokenInfoService : RazorSemanticTokenInfoService
    {
        // This cache is not created for performance, but rather to restrict memory growth.
        // We need to keep track of the last couple of requests for use in previousResultId, but if we let the grow unbounded it could quickly allocate a lot of memory.
        // Solution: an in-memory cache
        private static readonly MemoryCache<IReadOnlyList<uint>> _semanticTokenCache = new MemoryCache<IReadOnlyList<uint>>();

        public override SemanticTokens GetSemanticTokens(RazorCodeDocument codeDocument)
        {
            return GetSemanticTokens(codeDocument, range: null);
        }

        public override SemanticTokens GetSemanticTokens(RazorCodeDocument codeDocument, Range range)
        {
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            var syntaxNodes = TagHelperSpanVisitor.VisitAllNodes(codeDocument, range);

            var semanticTokens = ConvertSyntaxTokensToSemanticTokens(syntaxNodes, codeDocument);

            return semanticTokens;
        }

        public override SemanticTokensOrSemanticTokensEdits GetSemanticTokenEdits(RazorCodeDocument codeDocument, string previousResultId)
        {
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            if (string.IsNullOrEmpty(previousResultId))
            {
                throw new ArgumentException(nameof(previousResultId));
            }

            var syntaxNodes = TagHelperSpanVisitor.VisitAllNodes(codeDocument);
            
            var previousResults = _semanticTokenCache.Get(previousResultId);
            var newTokens = ConvertSyntaxTokensToSemanticTokens(syntaxNodes, codeDocument);

            var semanticEdits = SyntaxTokenToSemanticTokensMethods.ConvertSyntaxTokensToSemanticEdits(newTokens, previousResults);

            return semanticEdits;
        }

        private static SemanticTokens ConvertSyntaxTokensToSemanticTokens(
            IReadOnlyList<SyntaxResult> syntaxResults,
            RazorCodeDocument razorCodeDocument)
        {
            if (syntaxResults is null)
            {
                return null;
            }

            SyntaxResult previousResult = null;

            var data = new List<uint>();
            foreach (var result in syntaxResults)
            {
                var newData = GetData(result, previousResult, razorCodeDocument);
                data.AddRange(newData);

                previousResult = result;
            }

            var resultId = Guid.NewGuid();

            var tokensResult = new SemanticTokens
            {
                Data = data.ToArray(),
                ResultId = resultId.ToString()
            };

            _semanticTokenCache.Set(resultId.ToString(), data);

            return tokensResult;
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
            SyntaxResult currentNode,
            SyntaxResult previousNode,
            RazorCodeDocument razorCodeDocument)
        {
            var previousRange = previousNode?.Range;
            var currentRange = currentNode.Range;

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
                yield return (uint)currentRange.Start.Character;
            }

            // length
            var textSpan = currentNode.Range.AsTextSpan(razorCodeDocument.GetSourceText());
            var length = textSpan.Length;
            Debug.Assert(length > 0);
            yield return (uint)length;

            // tokenType
            yield return GetTokenTypeData(currentNode.Kind);

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
    }
}
