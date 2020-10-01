// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
#pragma warning disable CS0618
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Services;
using Microsoft.CodeAnalysis.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class DefaultRazorSemanticTokensInfoService : RazorSemanticTokensInfoService
    {
        // This cache is not created for performance, but rather to restrict memory growth.
        // We need to keep track of the last couple of requests for use in previousResultId,
        // but if we let the grow unbounded it could quickly allocate a lot of memory.
        // Solution: an in-memory cache
        private static readonly MemoryCache<string, IReadOnlyList<int>> _semanticTokensCache =
            new MemoryCache<string, IReadOnlyList<int>>();

        private readonly IClientLanguageServer _languageServer;
        private readonly RazorDocumentMappingService _documentMappingService;

        public DefaultRazorSemanticTokensInfoService(
            IClientLanguageServer languageServer,
            RazorDocumentMappingService documentMappingService)
        {
            _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
            _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
        }

        public override Task<SemanticTokens> GetSemanticTokensAsync(RazorCodeDocument codeDocument, TextDocumentIdentifier textDocumentIdentifier, CancellationToken cancellationToken)
        {
            return GetSemanticTokensAsync(codeDocument, textDocumentIdentifier, range: null, cancellationToken);
        }

        public override async Task<SemanticTokens> GetSemanticTokensAsync(
            RazorCodeDocument codeDocument,
            TextDocumentIdentifier textDocumentIdentifier,
            Range range,
            CancellationToken cancellationToken)
        {
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            var razorSemanticRanges = TagHelperSemanticRangeVisitor.VisitAllNodes(codeDocument, range);
            var csharpSemanticRanges = await GetCSharpSemanticRangesAsync(codeDocument, textDocumentIdentifier, range, cancellationToken);
            var combinedSemanticRanges = CombineSemanticRanges(razorSemanticRanges, csharpSemanticRanges);

            var razorSemanticTokens = ConvertSemanticRangesToSemanticTokens(combinedSemanticRanges, codeDocument);

            return razorSemanticTokens;
        }

        public override async Task<SemanticTokensFullOrDelta> GetSemanticTokensEditsAsync(
            RazorCodeDocument codeDocument,
            TextDocumentIdentifier textDocumentIdentifier,
            string previousResultId,
            CancellationToken cancellationToken)
        {
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            var razorSemanticRanges = TagHelperSemanticRangeVisitor.VisitAllNodes(codeDocument);
            var csharpSemanticRanges = await GetCSharpSemanticRangesAsync(codeDocument, textDocumentIdentifier, range: null, cancellationToken);

            var combinedSemanticRanges = CombineSemanticRanges(razorSemanticRanges, csharpSemanticRanges);

            IReadOnlyList<int> previousResults = null;

            if (previousResultId != null)
            {
                _semanticTokensCache.TryGetValue(previousResultId, out previousResults);
            }
            var newTokens = ConvertSemanticRangesToSemanticTokens(combinedSemanticRanges, codeDocument);

            if (previousResults is null)
            {
                return newTokens;
            }

            var razorSemanticEdits = SemanticTokensEditsDiffer.ComputeSemanticTokensEdits(newTokens, previousResults);

            return razorSemanticEdits;
        }

        private IReadOnlyList<SemanticRange> CombineSemanticRanges(params IReadOnlyList<SemanticRange>[] rangesArray)
        {
            var newList = new List<SemanticRange>();
            foreach (var list in rangesArray)
            {
                if (list != null)
                {
                    newList.AddRange(list);
                }
            }

            // Because SemanticToken data is generated relative to the previous token it must be in order.
            // We have a guarentee of order within any given language server, but the interweaving of them can be quite complex.
            // Rather than attempting to reason about transition zones we can simply order our ranges since we know there can be no overlapping range.
            newList.Sort();

            return newList;
        }

        private async Task<IReadOnlyList<SemanticRange>> GetCSharpSemanticRangesAsync(
            RazorCodeDocument codeDocument,
            TextDocumentIdentifier textDocumentIdentifier,
            Range range,
            CancellationToken cancellationToken)
        {
            var parameter = new SemanticTokensParams
            {
                TextDocument = textDocumentIdentifier,
            };

            var request = _languageServer.SendRequest(LanguageServerConstants.RazorProvideSemanticTokensEndpoint, parameter);
            var csharpResponses = await request.Returning<SemanticTokens>(cancellationToken);
            if (csharpResponses is null)
            {
                return null;
            }

            SemanticRange previousSemanticRange = null;
            var razorRanges = new List<SemanticRange>();
            for (var i = 0; i < csharpResponses.Data.Length; i += 5)
            {
                var lineDelta = csharpResponses.Data[i];
                var charDelta = csharpResponses.Data[i + 1];
                var length = csharpResponses.Data[i + 2];
                var tokenType = csharpResponses.Data[i + 3];
                var tokenModifiers = csharpResponses.Data[i + 4];

                var semanticRange = DataToSemanticRange(lineDelta, charDelta, length, tokenType, tokenModifiers, previousSemanticRange);
                if (_documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, semanticRange.Range, out var originalRange))
                {
                    var razorRange = new SemanticRange(semanticRange.Kind, originalRange, tokenModifiers);
                    if (range == null || range.OverlapsWith(razorRange.Range))
                    {
                        razorRanges.Add(razorRange);
                    }
                }
                previousSemanticRange = semanticRange;
            }

            return razorRanges.ToImmutableList();
        }

        private SemanticRange DataToSemanticRange(int lineDelta, int charDelta, int length, int tokenType, int tokenModifiers, SemanticRange previousSemanticRange = null)
        {
            if (previousSemanticRange is null)
            {
                previousSemanticRange = new SemanticRange(0, new Range(new Position(0, 0), new Position(0, 0)), modifier: 0);
            }

            var startLine = previousSemanticRange.Range.End.Line + lineDelta;
            var previousEndChar = lineDelta == 0 ? previousSemanticRange.Range.Start.Character : 0;
            var startCharacter = previousEndChar + charDelta;
            var start = new Position(startLine, startCharacter);

            var endLine = startLine;
            var endCharacter = startCharacter + length;
            var end = new Position(endLine, endCharacter);

            var range = new Range(start, end);
            var semanticRange = new SemanticRange(tokenType, range, tokenModifiers);

            return semanticRange;
        }

        private static SemanticTokens ConvertSemanticRangesToSemanticTokens(
            IReadOnlyList<SemanticRange> semanticRanges,
            RazorCodeDocument razorCodeDocument)
        {
            if (semanticRanges is null)
            {
                return null;
            }

            SemanticRange previousResult = null;

            var data = new List<int>();
            foreach (var result in semanticRanges)
            {
                var newData = GetData(result, previousResult, razorCodeDocument);
                data.AddRange(newData);

                previousResult = result;
            }

            var resultId = Guid.NewGuid();

            var tokensResult = new SemanticTokens
            {
                Data = data.ToImmutableArray(),
                ResultId = resultId.ToString()
            };

            _semanticTokensCache.Set(resultId.ToString(), data);

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
        private static IEnumerable<int> GetData(
            SemanticRange currentRange,
            SemanticRange previousRange,
            RazorCodeDocument razorCodeDocument)
        {
            // var previousRange = previousRange?.Range;
            // var currentRange = currentRange.Range;

            // deltaLine
            var previousLineIndex = previousRange?.Range == null ? 0 : previousRange.Range.Start.Line;
            yield return currentRange.Range.Start.Line - previousLineIndex;

            // deltaStart
            if (previousRange != null && previousRange?.Range.Start.Line == currentRange.Range.Start.Line)
            {
                yield return currentRange.Range.Start.Character - previousRange.Range.Start.Character;
            }
            else
            {
                yield return currentRange.Range.Start.Character;
            }

            // length
            var textSpan = currentRange.Range.AsTextSpan(razorCodeDocument.GetSourceText());
            var length = textSpan.Length;
            Debug.Assert(length > 0);
            yield return length;

            // tokenType
            yield return currentRange.Kind;

            // tokenModifiers
            // We don't currently have any need for tokenModifiers
            yield return currentRange.Modifier;
        }
    }
}
