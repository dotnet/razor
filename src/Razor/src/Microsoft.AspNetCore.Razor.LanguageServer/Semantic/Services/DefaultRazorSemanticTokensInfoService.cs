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

        // Maps (docURI -> (resultId -> tokens)). We cache per-doc instead of storing all tokens
        // in one giant cache to improve colorization speeds when working with multiple files.
        private const int MaxCachesPerDoc = 6;
        private readonly MemoryCache<DocumentUri, MemoryCache<string, VersionedSemanticTokens>> _razorDocTokensCache = new();

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

        public override Task<SemanticTokens?> GetSemanticTokensAsync(
            TextDocumentIdentifier textDocumentIdentifier,
            CancellationToken cancellationToken)
        {
            return GetSemanticTokensAsync(textDocumentIdentifier, range: null, cancellationToken);
        }

        public override async Task<SemanticTokens?> GetSemanticTokensAsync(
            TextDocumentIdentifier textDocumentIdentifier,
            Range? range,
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

            var tokens = await GetSemanticTokensAsync(textDocumentIdentifier, documentSnapshot, documentVersion, range, cancellationToken);

            return tokens;
        }

        // Internal for testing
        internal async Task<SemanticTokens?> GetSemanticTokensAsync(
            TextDocumentIdentifier textDocumentIdentifier,
            DocumentSnapshot documentSnapshot,
            int documentVersion,
            Range? range,
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
            string? newResultId = null;
            var isFinalizedCSharp = false;

            try
            {
                (csharpSemanticRanges, isFinalizedCSharp) = await GetCSharpSemanticRangesAsync(
                    codeDocument, textDocumentIdentifier, range, documentVersion, cancellationToken);
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

            var semanticVersion = await GetDocumentSemanticVersionAsync(documentSnapshot);

            if (newResultId is null)
            {
                // If there's no C# in the Razor doc, we won't have a resultId returned to us.
                // Just use a GUID instead.
                newResultId = Guid.NewGuid().ToString();
            }

            var razorSemanticTokens = ConvertSemanticRangesToSemanticTokens(
                combinedSemanticRanges, codeDocument, newResultId, isFinalizedCSharp);
            UpdateRazorDocCache(textDocumentIdentifier.Uri, semanticVersion, newResultId, razorSemanticTokens);

            return new SemanticTokens { ResultId = razorSemanticTokens.ResultId, Data = razorSemanticTokens.Data.ToImmutableArray() };
        }

        public override async Task<SemanticTokensFullOrDelta?> GetSemanticTokensEditsAsync(
            TextDocumentIdentifier textDocumentIdentifier,
            string? previousResultId,
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

            return await GetSemanticTokensEditsAsync(
                documentSnapshot,
                documentVersion,
                textDocumentIdentifier,
                previousResultId,
                cancellationToken);
        }

        // Internal for testing
        internal async Task<SemanticTokensFullOrDelta?> GetSemanticTokensEditsAsync(
            DocumentSnapshot documentSnapshot,
            long documentVersion,
            TextDocumentIdentifier textDocumentIdentifier,
            string? previousResultId,
            CancellationToken cancellationToken)
        {
            VersionStamp? cachedSemanticVersion = null;
            IReadOnlyList<int>? previousResults = null;
            var csharpTokensFinalized = false;

            // Attempting to retrieve cached tokens for the Razor document.
            if (previousResultId != null &&
                _razorDocTokensCache.TryGetValue(textDocumentIdentifier.Uri, out var documentCache) &&
                documentCache.TryGetValue(previousResultId, out var cachedTokens))
            {
                previousResults = cachedTokens?.SemanticTokens;
                cachedSemanticVersion = cachedTokens?.SemanticVersion;

                if (cachedTokens is not null)
                {
                    csharpTokensFinalized = cachedTokens.IsFinalizedCSharp;
                }
            }

            var semanticVersion = await GetDocumentSemanticVersionAsync(documentSnapshot);
            cancellationToken.ThrowIfCancellationRequested();

            // We have to recompute tokens in two scenarios:
            //     1) SemanticVersion is different. Occurs if there's been any text edits to the
            //        Razor file or ProjectVersion has changed.
            //     2) C# returned non-finalized tokens to us the last time around. May occur if a
            //        partial compilation was used to compute tokens.
            if (semanticVersion == default || cachedSemanticVersion != semanticVersion || !csharpTokensFinalized)
            {
                var codeDocument = await GetRazorCodeDocumentAsync(documentSnapshot);
                if (codeDocument is null)
                {
                    throw new ArgumentNullException(nameof(codeDocument));
                }

                cancellationToken.ThrowIfCancellationRequested();

                var razorSemanticRanges = TagHelperSemanticRangeVisitor.VisitAllNodes(codeDocument);

                var (csharpSemanticRanges, isFinalizedCSharp) = await GetCSharpSemanticRangesAsync(
                    codeDocument,
                    textDocumentIdentifier,
                    range: null,
                    documentVersion,
                    cancellationToken,
                    previousResultId);

                var combinedSemanticRanges = CombineSemanticRanges(razorSemanticRanges, csharpSemanticRanges);

                // We return null when we have an incomplete view of the document.
                // Likely CSharp ahead of us in terms of document versions.
                // We return null (which to the LSP is a no-op) to prevent flashing of CSharp elements.
                if (combinedSemanticRanges is null)
                {
                    return null;
                }

                var newResultId = Guid.NewGuid().ToString();
                var newTokens = ConvertSemanticRangesToSemanticTokens(
                    combinedSemanticRanges, codeDocument, newResultId, isFinalizedCSharp);
                UpdateRazorDocCache(textDocumentIdentifier.Uri, semanticVersion, newResultId, newTokens);

                if (previousResults is null)
                {
                    return new SemanticTokens { ResultId = newTokens.ResultId, Data = newTokens.Data.ToImmutableArray() };
                }

                var razorSemanticEdits = SemanticTokensEditsDiffer.ComputeSemanticTokensEdits(newTokens, previousResults);
                return razorSemanticEdits;
            }
            else
            {
                var result = new SemanticTokensFullOrDelta(new SemanticTokensDelta
                {
                    Edits = Array.Empty<SemanticTokensEdit>(),
                    ResultId = previousResultId,
                });

                return result;
            }
        }

        private void UpdateRazorDocCache(
            DocumentUri documentUri,
            VersionStamp semanticVersion,
            string newResultId,
            SemanticTokensResponse newTokens)
        {
            // Update the tokens cache associated with the given Razor doc. If the doc has no
            // associated cache, we will create one.
            if (!_razorDocTokensCache.TryGetValue(documentUri, out var documentCache))
            {
                documentCache = new MemoryCache<string, VersionedSemanticTokens>(sizeLimit: MaxCachesPerDoc);
                _razorDocTokensCache.Set(documentUri, documentCache);
            }

            documentCache.Set(newResultId, new VersionedSemanticTokens(semanticVersion, newTokens.Data, newTokens.IsFinalizedCSharp));
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
        internal virtual async Task<VersionedSemanticRange> GetCSharpSemanticRangesAsync(
            RazorCodeDocument codeDocument,
            TextDocumentIdentifier textDocumentIdentifier,
            Range? range,
            long documentVersion,
            CancellationToken cancellationToken,
            string? previousResultId = null)
        {
            var razorRanges = new List<SemanticRange>();

            if (!TryGetMinimalCSharpRange(codeDocument, out var csharpRange))
            {
                return new VersionedSemanticRange(razorRanges, IsFinalizedCSharp: true);
            }

            var csharpResponse = await GetMatchingCSharpResponseAsync(textDocumentIdentifier, documentVersion, csharpRange, cancellationToken);

            // Indicates an issue with retrieving the C# response (e.g. no response or C# is out of sync with us).
            // Unrecoverable, return null to indicate no change. It will retry in a bit.
            if (csharpResponse is null)
            {
                return VersionedSemanticRange.Default;
            }

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
                    var razorRange = new SemanticRange(semanticRange.Kind, originalRange, tokenModifiers);
                    if (range is null || range.OverlapsWith(razorRange.Range))
                    {
                        razorRanges.Add(razorRange);
                    }
                }

                previousSemanticRange = semanticRange;
            }

            var result = razorRanges.ToImmutableList();
            return new VersionedSemanticRange(result, csharpResponse.IsFinalizedCSharp);
        }

        // Internal for testing only
        internal static bool TryGetMinimalCSharpRange(RazorCodeDocument codeDocument, [NotNullWhen(true)] out Range? range)
        {
            var csharpDoc = codeDocument.GetCSharpDocument();

            // If there aren't any source mappings, there's no C# code in the Razor doc.
            if (csharpDoc.SourceMappings.Count == 0)
            {
                range = null;
                return false;
            }

            // We only need to colorize the portions of the generated doc that correspond with a C# mapping.
            // To accomplish this while minimizing the amount of work we need to do, we'll only colorize the
            // range spanning from the first C# span to the last C# span.
            SourceSpan? minSpan = null;
            SourceSpan? maxSpan = null;

            foreach (var mapping in csharpDoc.SourceMappings)
            {
                var generatedSpan = mapping.GeneratedSpan;
                if (minSpan is null || generatedSpan.AbsoluteIndex < minSpan.Value.AbsoluteIndex)
                {
                    minSpan = generatedSpan;
                }

                if (maxSpan is null || generatedSpan.AbsoluteIndex + generatedSpan.Length > maxSpan.Value.AbsoluteIndex + maxSpan.Value.Length)
                {
                    maxSpan = generatedSpan;
                }
            }

            var csharpSourceText = codeDocument.GetCSharpSourceText();
            var start = csharpSourceText.Lines.GetLinePosition(minSpan!.Value.AbsoluteIndex);
            var startPosition = new Position(start.Line, start.Character);

            var end = csharpSourceText.Lines.GetLinePosition(maxSpan!.Value.AbsoluteIndex + maxSpan!.Value.Length);
            var endPosition = new Position(end.Line, end.Character);

            range = new Range(startPosition, endPosition);
            return true;
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

            // C# doesn't return resultIds so we can return null here.
            return new SemanticTokensResponse(ResultId: null, csharpResponse.Tokens ?? Array.Empty<int>(), csharpResponse.IsFinalized);
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

        private static SemanticTokensResponse ConvertSemanticRangesToSemanticTokens(
            IReadOnlyList<SemanticRange> semanticRanges,
            RazorCodeDocument razorCodeDocument,
            string? resultId,
            bool isFinalizedCSharp)
        {
            SemanticRange? previousResult = null;

            var data = new List<int>();
            foreach (var result in semanticRanges)
            {
                var newData = GetData(result, previousResult, razorCodeDocument);
                data.AddRange(newData);

                previousResult = result;
            }

            var tokensResult = new SemanticTokensResponse(resultId, data.ToArray(), isFinalizedCSharp);
            return tokensResult;
        }

        private static async Task<VersionStamp> GetDocumentSemanticVersionAsync(DocumentSnapshot documentSnapshot)
        {
            var documentVersionStamp = await documentSnapshot.GetTextVersionAsync();
            var semanticVersion = documentVersionStamp.GetNewerVersion(documentSnapshot.Project.Version);

            return semanticVersion;
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

        // Internal for testing
        internal record VersionedSemanticRange(IReadOnlyList<SemanticRange>? SemanticRanges, bool IsFinalizedCSharp)
        {
            public static VersionedSemanticRange Default => new(null, false);
        }

        private record VersionedSemanticTokens(VersionStamp? SemanticVersion, IReadOnlyList<int> SemanticTokens, bool IsFinalizedCSharp);

        // Internal for testing
        internal record SemanticTokensResponse(string? ResultId, int[] Data, bool IsFinalizedCSharp)
        {
            public static SemanticTokensResponse Default => new(null, Array.Empty<int>(), false);
        }
    }
}
