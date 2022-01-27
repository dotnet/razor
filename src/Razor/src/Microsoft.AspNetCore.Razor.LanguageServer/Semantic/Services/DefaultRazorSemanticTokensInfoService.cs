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
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class DefaultRazorSemanticTokensInfoService : RazorSemanticTokensInfoService
    {
        private readonly ClientNotifierServiceBase _languageServer;
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly DocumentVersionCache _documentVersionCache;
        private readonly ILogger _logger;

        // (DocumentUri, SemanticVersion) -> (Ranges, Tokens)
        private readonly MemoryCache<(DocumentUri Uri, VersionStamp SemanticVersion), MemoryCache<Range, SemanticTokens>> _cachedResponses = new(sizeLimit: 10);

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

            var cachedTokens = await GetCachedTokensAsync(
                textDocumentIdentifier.Uri, documentSnapshot, documentVersion, semanticVersion, range, _logger, cancellationToken).ConfigureAwait(false);
            if (cachedTokens is not null)
            {
                return cachedTokens;
            }

            var (tokens, isCSharpFinalized) = await GetSemanticTokensAsync(
                textDocumentIdentifier, documentSnapshot, documentVersion, range, cancellationToken);
            if (isCSharpFinalized && tokens is not null)
            {
                CacheTokens(textDocumentIdentifier.Uri, semanticVersion, range, tokens);
            }

            return tokens;
        }

        private static async Task<VersionStamp> GetDocumentSemanticVersionAsync(DocumentSnapshot documentSnapshot)
        {
            var documentVersionStamp = await documentSnapshot.GetTextVersionAsync();
            var semanticVersion = documentVersionStamp.GetNewerVersion(documentSnapshot.Project.Version);

            return semanticVersion;
        }

        private async Task<SemanticTokens?> GetCachedTokensAsync(
            TextDocumentIdentifier textDocumentIdentifier,
            DocumentSnapshot documentSnapshot,
            int documentVersion,
            VersionStamp semanticVersion,
            Range range,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (!_cachedResponses.TryGetValue((textDocumentIdentifier.Uri, semanticVersion), out var documentCache))
            {
                return null;
            }

            // 1) We'll first check if the cache contains an exact match for the range, in which case we can
            // return early.
            if (documentCache.TryGetValue(range, out var cachedTokens))
            {
                return cachedTokens;
            }

            // 2) If the cache doesn't contain an exact match for the range, we can still check if there's a
            // partial match. This allows us to reduce the range we have to compute tokens for. We aim to find
            // the cached range with the most overlap with the requested range.
            (Range Range, int NumOverlappingLines)? currentBestMatch = null;
            foreach (var rangeCandidate in documentCache.Keys)
            {
                if (!range.OverlapsWith(rangeCandidate))
                {
                    continue;
                }

                var overlap = range.Overlap(rangeCandidate);
                Assumes.NotNull(overlap);

                // It's possible that the cached range candidate encompasses a large span that completely overlaps
                // with the requested range. Nothing in the LSP spec explicitly prevents us from returning excess
                // tokens outside of the requested range, so if we hit this case we can return early.
                if (overlap.Equals(range) && documentCache.TryGetValue(rangeCandidate, out cachedTokens))
                {
                    return cachedTokens;
                }

                // The range candidate becomes the best range either if:
                //     1) It is the first candidate we are analyzing
                //     2) The # of overlapping lines exceeds the current best candidate, AND the current candidate
                //     is not in the middle of the requested range (this complicates later computations).
                var numOverlappingLines = overlap.End.Line - overlap.Start.Line;
                if (currentBestMatch is null ||
                    ((currentBestMatch.Value.NumOverlappingLines < numOverlappingLines) && (rangeCandidate.Start <= range.Start || rangeCandidate.End >= range.End)))
                {
                    currentBestMatch = (rangeCandidate, numOverlappingLines);
                }
            }

            // We couldn't find a best range match. Don't use any cache values and compute normally.
            if (currentBestMatch is null)
            {
                return null;
            }

            if (!documentCache.TryGetValue(currentBestMatch.Value.Range, out cachedTokens))
            {
                logger.LogError("Could not find the expected cached tokens.");
                return null;
            }

            // 3) We've found the best range match. There are now two paths to consider depending on whether
            // we need to populate the start or end of the range.
            var bestMatch = currentBestMatch.Value;

            // 3a) The latter part of the range needs to be computed.
            if (bestMatch.Range.Start <= range.Start)
            {
                var tokens = await ComputeRequiredRange(computeStart: false, cachedTokens);
                if (tokens is null)
                {
                    return null;
                }

                return tokens;
            }
            // 3b) The beginning of the range needs to be computed.
            else if (bestMatch.Range.End >= range.End)
            {
                var tokens = await ComputeRequiredRange(computeStart: true, cachedTokens);
                if (tokens is null)
                {
                    return null;
                }

                return tokens;
            }
            else
            {
                // We shouldn't ever reach this point, but if we do we'll log an error.
                logger.LogError("Caching error in DefaultRazorSemanticTokensInfoService.");
                return null;
            }

            async Task<SemanticTokens?> ComputeRequiredRange(bool computeStart, SemanticTokens cachedTokens)
            {
                // We want to reduce the requested range to the part we don't have cached.
                var modifiedRange = computeStart ? new Range(range.Start, bestMatch.Range.Start) : new Range(bestMatch.Range.End, range.End);
                var (partialTokens, isCSharpFinalized) = await GetSemanticTokensAsync(
                    textDocumentIdentifier, documentSnapshot, documentVersion, modifiedRange, cancellationToken);
                if (partialTokens is null)
                {
                    return null;
                }

                // Now we want to combine the ranges. It's possible for the two ranges to have
                // duplicate tokens, so we can't just combine them automatically. We need to
                // remove the overlapping tokens and in doing so may need to make minor adjustments
                // to positioning.

                var beginningTokens = computeStart ? partialTokens.Data.ToArray() : cachedTokens.Data.ToArray();
                var endingTokens = computeStart ? cachedTokens.Data.ToArray() : partialTokens.Data.ToArray();

                // Compute the absolute ending line/character of the beginning tokens.
                var beginningLine = 0;
                var beginningCharacter = 0;
                for (var i = 0; i < beginningTokens.Length; i += 5)
                {
                    if (beginningTokens[i] != 0)
                    {
                        beginningCharacter = 0;
                    }

                    beginningLine += beginningTokens[i];
                    beginningCharacter += beginningTokens[i + 1];
                }

                // Compute the absolute beginning line/character of the ending tokens.
                // Here we also determine here which tokens are duplicates.
                var endingLine = 0;
                var endingCharacter = 0;
                var tokensToSkip = 0;
                for (var i = 0; i < endingTokens.Length; i += 5)
                {
                    if (endingTokens[i] != 0)
                    {
                        endingCharacter = 0;
                    }

                    endingLine += endingTokens[i];
                    endingCharacter += endingTokens[i + 1];

                    if (endingLine > beginningLine)
                    {
                        break;
                    }
                    else if (endingLine < beginningLine || endingCharacter <= beginningCharacter)
                    {
                        tokensToSkip += 5;
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }

                if (tokensToSkip > 0)
                {
                    endingTokens = endingTokens.Skip(tokensToSkip).ToArray();
                }

                if (endingLine > beginningLine)
                {
                    endingTokens[0] = endingLine - beginningLine;
                }

                var combinedTokens = beginningTokens.Concat(endingTokens);
                var semanticTokens = new SemanticTokens { Data = combinedTokens.ToImmutableArray() };

                if (isCSharpFinalized)
                {
                    var combinedRange = computeStart ? new Range(range.Start, bestMatch.Range.End) : new Range(bestMatch.Range.Start, range.End);
                    CacheTokens(textDocumentIdentifier.Uri, semanticVersion, combinedRange, semanticTokens);
                }

                return semanticTokens;
            }
        }

        private void CacheTokens(
            DocumentUri uri,
            VersionStamp semanticVersion,
            Range range,
            SemanticTokens tokens)
        {
            if (!_cachedResponses.TryGetValue((uri, semanticVersion), out var documentCache))
            {
                documentCache = new MemoryCache<Range, SemanticTokens>(sizeLimit: 1000);
                _cachedResponses.Set((uri, semanticVersion), documentCache);
            }

            documentCache.Set(range, tokens);
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
                return SemanticRangeResponse.Default;
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
            for (var i = 0; i < csharpResponse.Data.Length; i += 5)
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
            var minIndex = -1;
            var maxIndex = -1;
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
                    if (minIndex == -1 || mapping.OriginalSpan.AbsoluteIndex < minIndex)
                    {
                        minIndex = mapping.OriginalSpan.AbsoluteIndex;
                        minGeneratedSpan = mapping.GeneratedSpan;
                    }

                    if (maxIndex == -1 || mapping.OriginalSpan.AbsoluteIndex + mapping.OriginalSpan.Length > maxIndex)
                    {
                        maxIndex = mapping.OriginalSpan.AbsoluteIndex + mapping.OriginalSpan.Length;
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
