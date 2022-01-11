// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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

        // Caches the last response per-document to potentially save on computation costs.
        private readonly MemoryCache<DocumentUri, SemanticTokensCacheResponse> _cachedResponses = new();

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

            // If we have a matching cached response, avoid computation and return early.
            if (_cachedResponses.TryGetValue(textDocumentIdentifier.Uri, out var response) &&
                response.IsCSharpFinalized &&
                response.SemanticVersion == semanticVersion &&
                response.Range == range)
            {
                return response.SemanticTokens;
            }

            var tokens = await GetSemanticTokensAsync(
                textDocumentIdentifier, documentSnapshot, documentVersion, semanticVersion, range, cancellationToken);
            return tokens;
        }

        private static async Task<VersionStamp> GetDocumentSemanticVersionAsync(DocumentSnapshot documentSnapshot)
        {
            var documentVersionStamp = await documentSnapshot.GetTextVersionAsync();
            var semanticVersion = documentVersionStamp.GetNewerVersion(documentSnapshot.Project.Version);

            return semanticVersion;
        }

        // Internal for testing
        internal async Task<SemanticTokens?> GetSemanticTokensAsync(
            TextDocumentIdentifier textDocumentIdentifier,
            DocumentSnapshot documentSnapshot,
            int documentVersion,
            VersionStamp semanticVersion,
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
                return null;
            }

            var data = ConvertSemanticRangesToSemanticTokensData(combinedSemanticRanges, codeDocument);
            var tokens = new SemanticTokens { Data = data };

            // Cache the result so we can potentially avoid recomputation next time around.
            var cacheResponse = new SemanticTokensCacheResponse(semanticVersion, range, tokens, isCSharpFinalized);
            _cachedResponses.Set(textDocumentIdentifier.Uri, cacheResponse);

            return tokens;
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
            if (!_documentMappingService.TryMapToProjectedDocumentRange(codeDocument, razorRange, out var csharpRange))
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
        internal record SemanticRangeResponse(SemanticRange[] SemanticRanges, bool IsCSharpFinalized)
        {
            public static SemanticRangeResponse Default => new(Array.Empty<SemanticRange>(), false);
        }

        private record SemanticTokensCacheResponse(VersionStamp SemanticVersion, Range Range, SemanticTokens SemanticTokens, bool IsCSharpFinalized);
    }
}
