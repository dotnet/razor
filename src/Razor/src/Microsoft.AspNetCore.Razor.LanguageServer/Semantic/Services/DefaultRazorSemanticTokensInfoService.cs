// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class DefaultRazorSemanticTokensInfoService : RazorSemanticTokensInfoService
    {
        private const int TokenSize = 5;

        private readonly ClientNotifierServiceBase _languageServer;
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly DocumentVersionCache _documentVersionCache;
        private readonly ILogger _logger;

        private readonly SemanticTokensCache _tokensCache = new();

        public DefaultRazorSemanticTokensInfoService(
            ClientNotifierServiceBase languageServer,
            RazorDocumentMappingService documentMappingService,
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            DocumentVersionCache documentVersionCache,
            ILoggerFactory loggerFactory)
        {
            _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
            _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher ?? throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            _documentResolver = documentResolver ?? throw new ArgumentNullException(nameof(documentResolver));
            _documentVersionCache = documentVersionCache ?? throw new ArgumentNullException(nameof(documentVersionCache));

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<DefaultRazorSemanticTokensInfoService>();
        }

        public override async Task<SemanticTokens?> GetSemanticTokensAsync(
            TextDocumentIdentifier textDocumentIdentifier,
            Range range,
            CancellationToken cancellationToken)
        {
            var documentPath = textDocumentIdentifier.Uri.GetAbsolutePath();
            if (documentPath is null)
            {
                return null;
            }

            var documentInfo = await TryGetDocumentInfoAsync(documentPath, cancellationToken).ConfigureAwait(false);
            if (documentInfo is null)
            {
                return null;
            }

            var (documentSnapshot, documentVersion) = documentInfo.Value;

            var semanticVersion = await GetDocumentSemanticVersionAsync(documentSnapshot).ConfigureAwait(false);

            // Today the LSP client doesn't send us ranges that start or end midway through a line, but we'll be
            // defensive here since our tokens cache is only meant to be used with complete lines.
            if (range.Start.Character != 0 || range.End.Character != 0)
            {
                _logger.LogWarning($"Requested range starts or ends midway through a line: {range}");
                var (tokens, _) = await GetSemanticTokensAsync(
                    textDocumentIdentifier, documentSnapshot, documentVersion, range, cancellationToken);

                return tokens;
            }

            // See if we can use our cache to at least partially avoid recomputation.
            if (!_tokensCache.TryGetCachedTokens(textDocumentIdentifier.Uri, semanticVersion, range, out var cachedResult))
            {
                // No cache results found, so we'll recompute tokens for the entire range and then hopefully cache the
                // results to use next time around.
                var (tokens, isCSharpFinalized) = await GetSemanticTokensAsync(
                    textDocumentIdentifier, documentSnapshot, documentVersion, range, cancellationToken);

                // Only cache tokens if C# has finalized their tokens (meaning they were computed using
                // a full semantic model rather than a partial one)
                if (isCSharpFinalized && tokens is not null)
                {
                    _tokensCache.CacheTokens(textDocumentIdentifier.Uri, semanticVersion, range, tokens.Data.ToArray());
                }

                return tokens;
            }

            var cachedRange = cachedResult.Value.Range;
            var cachedTokens = cachedResult.Value.Tokens;

            // The cache returned to us an exact match for the range we're looking for, so we can return early.
            if (cachedRange.Equals(range))
            {
                return new SemanticTokens { Data = cachedTokens.ToImmutableArray() };
            }

            // The cache returned to us only a partial match for the range. We'll need to compute the rest by
            // sending range requests to the Razor/C#/HTML servers.
            var filledInRange = await FillInRangeGapsAsync(
                textDocumentIdentifier, documentSnapshot, documentVersion, semanticVersion,
                range, cachedRange, cachedTokens, cancellationToken).ConfigureAwait(false);

            return filledInRange;
        }

        /// <summary>
        /// Given a desired range and a partial set of cached semantic tokens for that range, computes and fills
        /// in the the tokens for the missing parts of the range.
        /// </summary>
        private async Task<SemanticTokens?> FillInRangeGapsAsync(
            TextDocumentIdentifier textDocumentIdentifier,
            DocumentSnapshot documentSnapshot,
            int documentVersion,
            VersionStamp semanticVersion,
            Range requestedRange,
            Range cachedRange,
            List<int> cachedTokens,
            CancellationToken cancellationToken)
        {
            var finalTokens = new List<int>();

            // There are 3 scenarios:
            //    1) The cached result is at the start of the range
            //    2) The cached result is in the middle of the range
            //    3) The cached result is at the end of the range
            // For 1) and 3), we need to send one request to the Razor/C# language servers for the missing range's tokens.
            // For 2), we need to send two requests (one for the missing start range, and one for the missing end range).

            // Try to concurrently process requests if there are multiple
            var (startTokens, endTokens) = await ComputeAndCacheTokensInParallelAsync(
                textDocumentIdentifier, documentSnapshot, documentVersion, semanticVersion,
                requestedRange, cachedRange, cancellationToken).ConfigureAwait(false);

            var absoluteLine = 0;

            // a) Before processing the cached portion of the range, we might have to process tokens for the
            // start of the range -> start of the cached range (if applicable).
            if (cachedRange.Start.Line != requestedRange.Start.Line)
            {
                if (startTokens is null)
                {
                    return null;
                }

                absoluteLine = AddStartingTokens(requestedRange, cachedRange, finalTokens, startTokens);
            }

            // b) Add the cached range tokens to the results.
            absoluteLine = AddCachedTokens(cachedTokens, finalTokens, absoluteLine);

            // c) Process the ending range tokens (if applicable).
            if (requestedRange.End.Line != cachedRange.End.Line)
            {
                if (endTokens is null)
                {
                    return null;
                }

                AddEndingTokens(requestedRange, cachedRange, finalTokens, absoluteLine, endTokens);
            }

            // We should now have all the tokens for the requested range (a combination of computed results and cached results).
            return new SemanticTokens { Data = finalTokens.ToImmutableArray() };

            async Task<(SemanticTokens? StartTokens, SemanticTokens? EndTokens)> ComputeAndCacheTokensInParallelAsync(
                TextDocumentIdentifier textDocumentIdentifier,
                DocumentSnapshot documentSnapshot,
                int documentVersion,
                VersionStamp semanticVersion,
                Range requestedRange,
                Range cachedRange,
                CancellationToken cancellationToken)
            {
                var startTokensTask = Task.FromResult<SemanticTokens?>(null);
                if (cachedRange.Start.Line != requestedRange.Start.Line)
                {
                    var partialRange = new Range { Start = requestedRange.Start, End = cachedRange.Start };
                    startTokensTask = ComputeAndCacheTokensAsync(
                        textDocumentIdentifier, documentSnapshot, documentVersion, semanticVersion,
                        partialRange, cancellationToken);
                }

                var endTokensTask = Task.FromResult<SemanticTokens?>(null);
                if (requestedRange.End.Line != cachedRange.End.Line)
                {
                    var partialRange = new Range { Start = cachedRange.End, End = requestedRange.End };
                    endTokensTask = ComputeAndCacheTokensAsync(
                        textDocumentIdentifier, documentSnapshot, documentVersion, semanticVersion,
                        partialRange, cancellationToken);
                }

                await Task.WhenAll(startTokensTask, endTokensTask).ConfigureAwait(false);

                var startTokens = await startTokensTask;
                var endTokens = await endTokensTask;

                return (startTokens, endTokens);
            }

            async Task<SemanticTokens?> ComputeAndCacheTokensAsync(
                TextDocumentIdentifier textDocumentIdentifier,
                DocumentSnapshot documentSnapshot,
                int documentVersion,
                VersionStamp semanticVersion,
                Range partialRange,
                CancellationToken cancellationToken)
            {
                var (tokens, isCSharpFinalized) = await GetSemanticTokensAsync(
                    textDocumentIdentifier, documentSnapshot, documentVersion, partialRange, cancellationToken).ConfigureAwait(false);

                // Only cache tokens if C# has finalized their tokens (meaning they were computed using
                // a full semantic model rather than a partial one)
                if (isCSharpFinalized && tokens is not null)
                {
                    _tokensCache.CacheTokens(textDocumentIdentifier.Uri, semanticVersion, partialRange, tokens.Data.ToArray());
                }

                return tokens;
            }

            static int AddStartingTokens(
                Range requestedRange,
                Range cachedRange,
                List<int> finalTokens,
                SemanticTokens startingTokens)
            {
                // Add the partially computed range to the results. We need to also keep track of the current
                // absolute line index to use later when combining results.
                var absoluteLine = 0;
                var isFirstTokenInRange = true;
                for (var tokenIndex = 0; tokenIndex < startingTokens.Data.Length; tokenIndex++)
                {
                    // The first int of each token represents the line offset. We use this info to keep track of the
                    // absolute line for use later on when computing relative token positions.
                    if (tokenIndex % TokenSize == 0)
                    {
                        absoluteLine += startingTokens.Data[tokenIndex];

                        // Skip tokens that are out of the range
                        if (absoluteLine < requestedRange.Start.Line || absoluteLine >= cachedRange.Start.Line)
                        {
                            continue;
                        }

                        // First token of the starting set should be relative to the start of the document
                        if (isFirstTokenInRange)
                        {
                            finalTokens.Add(absoluteLine);
                            isFirstTokenInRange = false;
                            continue;
                        }
                    }

                    finalTokens.Add(startingTokens.Data[tokenIndex]);
                }

                return absoluteLine;
            }

            static int AddCachedTokens(List<int> cachedTokens, List<int> finalTokens, int absoluteLine)
            {
                // First token of the cached token set should be relative to either the start of the document
                // (if the current line is the first tokens line) or the previous line containing tokens
                finalTokens.Add(cachedTokens[0] - absoluteLine);
                absoluteLine = cachedTokens[0];

                for (var cachedTokenIndex = 1; cachedTokenIndex < cachedTokens.Count; cachedTokenIndex++)
                {
                    finalTokens.Add(cachedTokens[cachedTokenIndex]);

                    // Keep track of the absolute line # to use later when computing line offsets
                    if (cachedTokenIndex % TokenSize == 0)
                    {
                        absoluteLine += cachedTokens[cachedTokenIndex];
                    }
                }

                return absoluteLine;
            }

            static void AddEndingTokens(
                Range requestedRange,
                Range cachedRange,
                List<int> finalTokens,
                int cachedTokensEndAbsoluteLine,
                SemanticTokens endingTokens)
            {
                var isFirstTokenInRange = true;
                var absoluteLine = 0;
                for (var tokenIndex = 0; tokenIndex < endingTokens.Data.Length; tokenIndex++)
                {
                    // The first int of each token represents the line offset. We use this info to keep track of the
                    // absolute line for use later on when computing relative token positions.
                    if (tokenIndex % 5 == 0)
                    {
                        absoluteLine = tokenIndex == 0 ? endingTokens.Data[0] : absoluteLine + endingTokens.Data[tokenIndex];

                        // Skip tokens that are out of the range
                        if (absoluteLine < cachedRange.End.Line || absoluteLine >= requestedRange.End.Line)
                        {
                            continue;
                        }

                        // First token of the ending set should be relative to the end line of the cached tokens set
                        if (isFirstTokenInRange)
                        {
                            finalTokens.Add(absoluteLine - cachedTokensEndAbsoluteLine);
                            isFirstTokenInRange = false;
                            continue;
                        }
                    }

                    finalTokens.Add(endingTokens.Data[tokenIndex]);
                }
            }
        }

        private static async Task<VersionStamp> GetDocumentSemanticVersionAsync(DocumentSnapshot documentSnapshot)
        {
            var documentVersionStamp = await documentSnapshot.GetTextVersionAsync();
            var semanticVersion = documentVersionStamp.GetNewerVersion(documentSnapshot.Project.Version);

            return semanticVersion;
        }

        // Internal for testing
        internal async Task<(SemanticTokens? Tokens, bool IsCSharpFinalized)> GetSemanticTokensAsync(
            TextDocumentIdentifier textDocumentIdentifier,
            DocumentSnapshot documentSnapshot,
            int documentVersion,
            Range range,
            CancellationToken cancellationToken)
        {
            var codeDocument = await GetRazorCodeDocumentAsync(documentSnapshot);
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var razorSemanticRanges = TagHelperSemanticRangeVisitor.VisitAllNodes(codeDocument, range);
            IReadOnlyList<SemanticRange>? csharpSemanticRanges = null;
            var isCSharpFinalized = false;

            try
            {
                (csharpSemanticRanges, isCSharpFinalized) = await GetCSharpSemanticRangesAsync(
                    codeDocument, textDocumentIdentifier, range, documentVersion, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error thrown while retrieving CSharp semantic range");
            }

            var combinedSemanticRanges = CombineSemanticRanges(razorSemanticRanges, csharpSemanticRanges);

            // We return null when we have an incomplete view of the document.
            // Likely CSharp ahead of us in terms of document versions.
            // We return null (which to the LSP is a no-op) to prevent flashing of CSharp elements.
            if (combinedSemanticRanges is null)
            {
                return (null, isCSharpFinalized);
            }

            var data = ConvertSemanticRangesToSemanticTokensData(combinedSemanticRanges, codeDocument);
            var tokens = new SemanticTokens { Data = data };

            return (tokens, isCSharpFinalized);
        }

        private static async Task<RazorCodeDocument?> GetRazorCodeDocumentAsync(DocumentSnapshot documentSnapshot)
        {
            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync();
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            return codeDocument;
        }

        private static IReadOnlyList<SemanticRange>? CombineSemanticRanges(params IReadOnlyList<SemanticRange>?[] rangesArray)
        {
            if (rangesArray.Any(a => a is null))
            {
                // If we have an incomplete view of the situation we should return null so we avoid flashing.
                return null;
            }

            var newList = new List<SemanticRange>();
            foreach (var list in rangesArray)
            {
                if (list != null)
                {
                    newList.AddRange(list);
                }
            }

            // Because SemanticToken data is generated relative to the previous token it must be in order.
            // We have a guarantee of order within any given language server, but the interweaving of them can be quite complex.
            // Rather than attempting to reason about transition zones we can simply order our ranges since we know there can be no overlapping range.
            newList.Sort();

            return newList;
        }

        // Internal and virtual for testing only
        internal virtual async Task<SemanticRangeResponse> GetCSharpSemanticRangesAsync(
            RazorCodeDocument codeDocument,
            TextDocumentIdentifier textDocumentIdentifier,
            Range razorRange,
            long documentVersion,
            CancellationToken cancellationToken,
            string? previousResultId = null)
        {
            // We'll try to call into the mapping service to map to the projected range for us. If that doesn't work,
            // we'll try to find the minimal range ourselves.
            if (!_documentMappingService.TryMapToProjectedDocumentRange(codeDocument, razorRange, out var csharpRange) &&
                !TryGetMinimalCSharpRange(codeDocument, razorRange, out csharpRange))
            {
                // There's no C# in the range.
                return new SemanticRangeResponse(SemanticRanges: Array.Empty<SemanticRange>(), IsCSharpFinalized: true);
            }

            var csharpResponse = await GetMatchingCSharpResponseAsync(textDocumentIdentifier, documentVersion, csharpRange, cancellationToken);

            // Indicates an issue with retrieving the C# response (e.g. no response or C# is out of sync with us).
            // Unrecoverable, return default to indicate no change. It will retry in a bit.
            if (csharpResponse is null)
            {
                return SemanticRangeResponse.Default;
            }

            var razorRanges = new List<SemanticRange>();

            SemanticRange? previousSemanticRange = null;
            for (var i = 0; i < csharpResponse.Data.Length; i += TokenSize)
            {
                var lineDelta = csharpResponse.Data[i];
                var charDelta = csharpResponse.Data[i + 1];
                var length = csharpResponse.Data[i + 2];
                var tokenType = csharpResponse.Data[i + 3];
                var tokenModifiers = csharpResponse.Data[i + 4];

                var semanticRange = DataToSemanticRange(
                    lineDelta, charDelta, length, tokenType, tokenModifiers, previousSemanticRange);
                if (_documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, semanticRange.Range, out var originalRange))
                {
                    var razorSemanticRange = new SemanticRange(semanticRange.Kind, originalRange, tokenModifiers);
                    if (razorRange is null || razorRange.OverlapsWith(razorSemanticRange.Range))
                    {
                        razorRanges.Add(razorSemanticRange);
                    }
                }

                previousSemanticRange = semanticRange;
            }

            var result = razorRanges.ToArray();
            var semanticRanges = new SemanticRangeResponse(result, IsCSharpFinalized: csharpResponse.IsCSharpFinalized);
            return semanticRanges;
        }

        private static bool TryGetMinimalCSharpRange(RazorCodeDocument codeDocument, Range razorRange, [NotNullWhen(true)] out Range? csharpRange)
        {
            SourceSpan? minGeneratedSpan = null;
            SourceSpan? maxGeneratedSpan = null;

            var sourceText = codeDocument.GetSourceText();
            var textSpan = razorRange.AsTextSpan(sourceText);
            var csharpDoc = codeDocument.GetCSharpDocument();

            // We want to find the min and max C# source mapping that corresponds with our Razor range.
            foreach (var mapping in csharpDoc.SourceMappings)
            {
                var mappedTextSpan = mapping.OriginalSpan.AsTextSpan();

                if (textSpan.OverlapsWith(mappedTextSpan))
                {
                    if (minGeneratedSpan is null || mapping.GeneratedSpan.AbsoluteIndex < minGeneratedSpan.Value.AbsoluteIndex)
                    {
                        minGeneratedSpan = mapping.GeneratedSpan;
                    }

                    var mappingEndIndex = mapping.GeneratedSpan.AbsoluteIndex + mapping.GeneratedSpan.Length;
                    if (maxGeneratedSpan is null || mappingEndIndex > maxGeneratedSpan.Value.AbsoluteIndex + maxGeneratedSpan.Value.Length)
                    {
                        maxGeneratedSpan = mapping.GeneratedSpan;
                    }
                }
            }

            // Create a new projected range based on our calculated min/max source spans.
            if (minGeneratedSpan is not null && maxGeneratedSpan is not null)
            {
                var csharpSourceText = codeDocument.GetCSharpSourceText();
                var startRange = minGeneratedSpan.Value.AsTextSpan().AsRange(csharpSourceText);
                var endRange = maxGeneratedSpan.Value.AsTextSpan().AsRange(csharpSourceText);

                csharpRange = new Range { Start = startRange.Start, End = endRange.End };

                return true;
            }

            csharpRange = null;
            return false;
        }

        private async Task<SemanticTokensResponse?> GetMatchingCSharpResponseAsync(
            TextDocumentIdentifier textDocumentIdentifier,
            long documentVersion,
            Range csharpRange,
            CancellationToken cancellationToken)
        {
            var parameter = new ProvideSemanticTokensRangeParams(textDocumentIdentifier, documentVersion, csharpRange);
            var request = await _languageServer.SendRequestAsync(LanguageServerConstants.RazorProvideSemanticTokensRangeEndpoint, parameter);
            var csharpResponse = await request.Returning<ProvideSemanticTokensResponse>(cancellationToken);

            if (csharpResponse is null)
            {
                // C# isn't ready yet, don't make Razor wait for it
                return SemanticTokensResponse.Default;
            }
            else if (csharpResponse.HostDocumentSyncVersion != null && csharpResponse.HostDocumentSyncVersion != documentVersion)
            {
                // No C# response or C# is out of sync with us. Unrecoverable, return null to indicate no change. It will retry in a bit.
                return null;
            }

            var response = new SemanticTokensResponse(csharpResponse.Tokens ?? Array.Empty<int>(), csharpResponse.IsFinalized);
            return response;
        }

        private static SemanticRange DataToSemanticRange(
            int lineDelta,
            int charDelta,
            int length,
            int tokenType,
            int tokenModifiers,
            SemanticRange? previousSemanticRange = null)
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

        private static ImmutableArray<int> ConvertSemanticRangesToSemanticTokensData(
            IReadOnlyList<SemanticRange> semanticRanges,
            RazorCodeDocument razorCodeDocument)
        {
            SemanticRange? previousResult = null;

            var data = new List<int>();
            foreach (var result in semanticRanges)
            {
                var newData = GetData(result, previousResult, razorCodeDocument);
                data.AddRange(newData);

                previousResult = result;
            }

            return data.ToImmutableArray();
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
            SemanticRange? previousRange,
            RazorCodeDocument razorCodeDocument)
        {
            // var previousRange = previousRange?.Range;
            // var currentRange = currentRange.Range;

            // deltaLine
            var previousLineIndex = previousRange?.Range is null ? 0 : previousRange.Range.Start.Line;
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

        private Task<(DocumentSnapshot Snapshot, int Version)?> TryGetDocumentInfoAsync(string absolutePath, CancellationToken cancellationToken)
        {
            return _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync<(DocumentSnapshot Snapshot, int Version)?>(() =>
            {
                if (_documentResolver.TryResolveDocument(absolutePath, out var documentSnapshot))
                {
                    if (_documentVersionCache.TryGetDocumentVersion(documentSnapshot, out var version))
                    {
                        return (documentSnapshot, version.Value);
                    }
                }

                return null;
            }, cancellationToken);
        }

        private record SemanticTokensResponse(int[] Data, bool IsCSharpFinalized)
        {
            public static SemanticTokensResponse Default => new(Array.Empty<int>(), false);
        }

        // Internal for testing
        internal record SemanticRangeResponse(SemanticRange[]? SemanticRanges, bool IsCSharpFinalized)
        {
            public static SemanticRangeResponse Default => new(null, false);
        }

        private record SemanticTokensCacheResponse(VersionStamp SemanticVersion, Range Range, SemanticTokens SemanticTokens);
    }
}
