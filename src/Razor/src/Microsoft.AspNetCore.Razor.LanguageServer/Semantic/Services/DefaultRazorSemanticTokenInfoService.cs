// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Services;
using Microsoft.Extensions.Internal;
using Microsoft.VisualStudio.Editor.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class DefaultRazorSemanticTokenInfoService : RazorSemanticTokenInfoService
    {
        // This cache is not created for performance, but rather to restrict memory growth.
        // We need to keep track of the last couple of requests for use in previousResultId, but if we let the grow unbounded it could quickly allocate a lot of memory.
        // Solution: an in-memory cache
        private static readonly MemoryCache<IReadOnlyList<uint>> _semanticTokenCache = new MemoryCache<IReadOnlyList<uint>>();

        public override SemanticTokens GetSemanticTokens(RazorCodeDocument codeDocument, Range range = null)
        {
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            var syntaxRanges = VisitAllNodes(codeDocument, range);

            var semanticTokens = ConvertSyntaxTokensToSemanticTokens(syntaxRanges, codeDocument);

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

            var syntaxRanges = VisitAllNodes(codeDocument);
            var previousResults = _semanticTokenCache.Get(previousResultId);

            var semanticEdits = ConvertSyntaxTokensToSemanticEdits(syntaxRanges, previousResults, codeDocument);

            return semanticEdits;
        }

        private static IReadOnlyList<SyntaxResult> VisitAllNodes(RazorCodeDocument razorCodeDocument, Range range = null)
        {
            var visitor = new TagHelperSpanVisitor(razorCodeDocument, range);
            visitor.Visit(razorCodeDocument.GetSyntaxTree().Root);

            return visitor.TagHelperData;
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

        private static SemanticTokensOrSemanticTokensEdits ConvertSyntaxTokensToSemanticEdits(
            IReadOnlyList<SyntaxResult> syntaxResults,
            IReadOnlyList<uint> previousResults,
            RazorCodeDocument codeDocument)
        {
            var newTokens = ConvertSyntaxTokensToSemanticTokens(syntaxResults, codeDocument);
            var oldData = previousResults;

            if (oldData is null || oldData.Count == 0)
            {
                return newTokens;
            }

            // The below algorithm was taken from OmniSharp/csharp-language-server-protocol at
            // https://github.com/OmniSharp/csharp-language-server-protocol/blob/bdec4c73240be52fbb25a81f6ad7d409f77b5215/src/Protocol/Document/Proposals/SemanticTokensDocument.cs#L156

            var prevData = oldData;
            var prevDataLength = oldData.Count;
            var dataLength = newTokens.Data.Length;
            var startIndex = 0;
            while (startIndex < dataLength && startIndex < prevDataLength && prevData[startIndex] ==
                newTokens.Data[startIndex])
            {
                startIndex++;
            }

            if (startIndex < dataLength && startIndex < prevDataLength)
            {
                // Find end index
                var endIndex = 0;
                while (endIndex < dataLength && endIndex < prevDataLength &&
                       prevData[prevDataLength - 1 - endIndex] == newTokens.Data[dataLength - 1 - endIndex])
                {
                    endIndex++;
                }

                var newData = ImmutableArray.Create(newTokens.Data, startIndex, dataLength - endIndex - startIndex);
                var result = new SemanticTokensEdits
                {
                    ResultId = newTokens.ResultId,
                    Edits = new[] {
                        new SemanticTokensEdit {
                            Start = startIndex,
                            DeleteCount = prevDataLength - endIndex - startIndex,
                            Data = newData
                        }
                    }
                };
                return result;
            }

            if (startIndex < dataLength)
            {
                return new SemanticTokensEdits
                {
                    ResultId = newTokens.ResultId,
                    Edits = new[] {
                        new SemanticTokensEdit {
                            Start = startIndex,
                            DeleteCount = 0,
                            Data = ImmutableArray.Create(newTokens.Data, startIndex, newTokens.Data.Length - startIndex)
                        }
                    }
                };
            }

            if (startIndex < prevDataLength)
            {
                return new SemanticTokensEdits
                {
                    ResultId = newTokens.ResultId,
                    Edits = new[] {
                        new SemanticTokensEdit {
                            Start = startIndex,
                            DeleteCount = prevDataLength - startIndex
                        }
                    }
                };
            }

            return new SemanticTokensEdits
            {
                ResultId = newTokens.ResultId,
                Edits = Array.Empty<SemanticTokensEdit>()
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

        private class SyntaxResult
        {
            public SyntaxResult(SyntaxNode node, SyntaxKind kind, RazorCodeDocument razorCodeDocument)
            {
                var range = node.GetRange(razorCodeDocument.Source);
                Range = range;
                Kind = kind;
            }

            public Range Range { get; set; }

            public SyntaxKind Kind { get; set; }

            public bool Transmited { get; set; }

            public override bool Equals(object obj)
            {
                var other = obj as SyntaxResult;

                var result = other.Kind == Kind && other.Range.Equals(Range);
                return result;
            }

            public override int GetHashCode()
            {
                var hash = HashCodeCombiner.Start();
                hash.Add(Range);
                hash.Add(Kind);

                return hash.CombinedHash;
            }
        }

        private class TagHelperSpanVisitor : SyntaxWalker
        {
            private readonly List<SyntaxResult> _syntaxNodes;
            private readonly RazorCodeDocument _razorCodeDocument;
            private readonly Range _range;

            public TagHelperSpanVisitor(RazorCodeDocument razorCodeDocument, Range range)
            {
                _syntaxNodes = new List<SyntaxResult>();
                _razorCodeDocument = razorCodeDocument;
                _range = range;
            }

            public IReadOnlyList<SyntaxResult> TagHelperData => _syntaxNodes.Where(n => n.Transmited).ToList();

            public override void VisitMarkupTagHelperStartTag(MarkupTagHelperStartTagSyntax node)
            {
                if (ClassifyTagName((MarkupTagHelperElementSyntax)node.Parent))
                {
                    var result = new SyntaxResult(node.Name, SyntaxKind.MarkupTagHelperStartTag, _razorCodeDocument);
                    AddNodes(result);
                }
                base.VisitMarkupTagHelperStartTag(node);
            }

            public override void VisitMarkupTagHelperEndTag(MarkupTagHelperEndTagSyntax node)
            {
                if (ClassifyTagName((MarkupTagHelperElementSyntax)node.Parent))
                {
                    var result = new SyntaxResult(node.Name, SyntaxKind.MarkupTagHelperEndTag, _razorCodeDocument);
                    AddNodes(result);
                }
                base.VisitMarkupTagHelperEndTag(node);
            }

            public override void VisitMarkupMinimizedTagHelperAttribute(MarkupMinimizedTagHelperAttributeSyntax node)
            {
                if (node.TagHelperAttributeInfo.Bound)
                {
                    var result = new SyntaxResult(node.Name, SyntaxKind.MarkupMinimizedTagHelperAttribute, _razorCodeDocument);
                    AddNodes(result);
                }

                base.VisitMarkupMinimizedTagHelperAttribute(node);
            }

            public override void VisitMarkupTagHelperAttribute(MarkupTagHelperAttributeSyntax node)
            {
                if (node.TagHelperAttributeInfo.Bound)
                {
                    var result = new SyntaxResult(node.Name, SyntaxKind.MarkupTagHelperAttribute, _razorCodeDocument);
                    AddNodes(result);
                }

                base.VisitMarkupTagHelperAttribute(node);
            }

            public override void VisitMarkupTagHelperDirectiveAttribute(MarkupTagHelperDirectiveAttributeSyntax node)
            {
                if (node.TagHelperAttributeInfo.Bound)
                {
                    var transition = new SyntaxResult(node.Transition, SyntaxKind.Transition, _razorCodeDocument);
                    AddNodes(transition);

                    var directiveAttribute = new SyntaxResult(node.Name, SyntaxKind.MarkupTagHelperDirectiveAttribute, _razorCodeDocument);
                    AddNodes(directiveAttribute);

                    if (node.Colon != null)
                    {
                        var colon = new SyntaxResult(node.Colon, SyntaxKind.Colon, _razorCodeDocument);
                        AddNodes(colon);
                    }

                    if (node.ParameterName != null)
                    {
                        var parameterName = new SyntaxResult(node.ParameterName, SyntaxKind.MarkupTagHelperDirectiveAttribute, _razorCodeDocument);
                        AddNodes(parameterName);
                    }
                }

                base.VisitMarkupTagHelperDirectiveAttribute(node);
            }

            public override void VisitMarkupMinimizedTagHelperDirectiveAttribute(MarkupMinimizedTagHelperDirectiveAttributeSyntax node)
            {

                if (node.TagHelperAttributeInfo.Bound)
                {
                    var transition = new SyntaxResult(node.Transition, SyntaxKind.Transition, _razorCodeDocument);
                    AddNodes(transition);

                    var directiveAttribute = new SyntaxResult(node.Name, SyntaxKind.MarkupMinimizedTagHelperDirectiveAttribute, _razorCodeDocument);
                    AddNodes(directiveAttribute);

                    if (node.Colon != null)
                    {
                        var colon = new SyntaxResult(node.Colon, SyntaxKind.Colon, _razorCodeDocument);
                        AddNodes(colon);
                    }

                    if (node.ParameterName != null)
                    {
                        var parameterName = new SyntaxResult(node.ParameterName, SyntaxKind.MarkupMinimizedTagHelperDirectiveAttribute, _razorCodeDocument);

                        AddNodes(parameterName);
                    }
                }

                base.VisitMarkupMinimizedTagHelperDirectiveAttribute(node);
            }

            private void AddNodes(SyntaxResult syntaxResult)
            {
                if (_range is null || syntaxResult.Range.OverlapsWith(_range))
                {
                    syntaxResult.Transmited = true;
                }
                else
                {
                    syntaxResult.Transmited = false;
                }

                _syntaxNodes.Add(syntaxResult);
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
}
