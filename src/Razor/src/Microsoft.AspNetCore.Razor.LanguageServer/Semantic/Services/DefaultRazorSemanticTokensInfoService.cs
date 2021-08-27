// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

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

        // Maps (docURI -> (resultId -> tokens)). We cache per-doc instead of storing all tokens
        // in one giant cache to improve colorization speeds when working with multiple files.
        private const int MaxCachesPerDoc = 6;
        private readonly MemoryCache<DocumentUri, MemoryCache<string, VersionedSemanticTokens>> _razorDocTokensCache = new();
        private readonly MemoryCache<DocumentUri, MemoryCache<string, IReadOnlyList<int>>> _csharpGeneratedDocTokensCache = new();

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

            var (documentSnapshot, documentVersion) = await TryGetDocumentInfoAsync(documentPath, cancellationToken);
            if (documentSnapshot is null || documentVersion is null)
            {
                return null;
            }

            var tokens = await GetSemanticTokensAsync(textDocumentIdentifier, documentSnapshot, documentVersion.Value, range, cancellationToken);

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
            var isPartialCSharp = false;

            try
            {
                (csharpSemanticRanges, newResultId, isPartialCSharp) = await GetCSharpSemanticRangesAsync(
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
                combinedSemanticRanges, codeDocument, newResultId, isPartialCSharp);
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

            var (documentSnapshot, documentVersion) = await TryGetDocumentInfoAsync(documentPath, cancellationToken);
            if (documentSnapshot is null || documentVersion is null)
            {
                return null;
            }

            return await GetSemanticTokensEditsAsync(
                documentSnapshot,
                documentVersion.Value,
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
            var csharpTokensIncomplete = false;

            // Attempting to retrieve cached tokens for the Razor document.
            if (previousResultId != null &&
                _razorDocTokensCache.TryGetValue(textDocumentIdentifier.Uri, out var documentCache) &&
                documentCache.TryGetValue(previousResultId, out var cachedTokens))
            {
                previousResults = cachedTokens?.SemanticTokens;
                cachedSemanticVersion = cachedTokens?.SemanticVersion;

                if (cachedTokens is not null)
                {
                    csharpTokensIncomplete = cachedTokens.IsPartialCSharp;
                }
            }

            var semanticVersion = await GetDocumentSemanticVersionAsync(documentSnapshot);
            cancellationToken.ThrowIfCancellationRequested();

            // We have to recompute tokens in two scenarios:
            //     1) SemanticVersion is different. Occurs if there's been any text edits to the
            //        Razor file or ProjectVersion has changed.
            //     2) C# returned incomplete tokens to us the last time around. May occur if a
            //        partial compilation was used to compute tokens.
            if (semanticVersion == default || cachedSemanticVersion != semanticVersion || csharpTokensIncomplete)
            {
                var codeDocument = await GetRazorCodeDocumentAsync(documentSnapshot);
                if (codeDocument is null)
                {
                    throw new ArgumentNullException(nameof(codeDocument));
                }
                cancellationToken.ThrowIfCancellationRequested();

                var razorSemanticRanges = TagHelperSemanticRangeVisitor.VisitAllNodes(codeDocument);

                var (csharpSemanticRanges, newResultId, isPartialCSharp) = await GetCSharpSemanticRangesAsync(
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

                if (newResultId is null)
                {
                    // If there's no C# in the Razor doc, we won't have a resultId returned to us.
                    // Just use a GUID instead.
                    newResultId = Guid.NewGuid().ToString();
                }

                var newTokens = ConvertSemanticRangesToSemanticTokens(
                    combinedSemanticRanges, codeDocument, newResultId, isPartialCSharp);
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

            documentCache.Set(newResultId, new VersionedSemanticTokens(semanticVersion, newTokens.Data, newTokens.IsPartial));
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
            IReadOnlyList<int>? previousCSharpTokens = null;

            // Attempting to retrieve cached tokens for the C# generated document.
            if (previousResultId != null &&
                _csharpGeneratedDocTokensCache.TryGetValue(textDocumentIdentifier.Uri, out var documentCache) &&
                documentCache.TryGetValue(previousResultId, out var cachedTokens))
            {
                previousCSharpTokens = cachedTokens;
            }

            var csharpResponse = previousResultId == null || previousCSharpTokens == null
                ? await GetMatchingCSharpResponseAsync(
                    textDocumentIdentifier, documentVersion, cancellationToken)
                : await GetMatchingCSharpEditsResponseAsync(
                    textDocumentIdentifier, documentVersion, previousResultId, previousCSharpTokens, cancellationToken);

            // Indicates an issue with retrieving the C# response (e.g. no response or C# is out of sync with us).
            // Unrecoverable, return null to indicate no change. It will retry in a bit.
            if (csharpResponse is null)
            {
                return VersionedSemanticRange.Default;
            }

            var razorRanges = new List<SemanticRange>();

            // Indicates no C# code in Razor doc.
            if (csharpResponse.ResultId is null)
            {
                return new VersionedSemanticRange(razorRanges, null, IsPartialCSharp: csharpResponse.IsPartial);
            }

            // Keep track of the tokens for the C# generated document so we can reference them
            // the next time we're called for edits. We only need to insert into the cache if
            // we receive new responses.
            if (!_csharpGeneratedDocTokensCache.TryGetValue(
                textDocumentIdentifier.Uri, out documentCache))
            {
                documentCache = new MemoryCache<string, IReadOnlyList<int>>(sizeLimit: MaxCachesPerDoc);
                _csharpGeneratedDocTokensCache.Set(textDocumentIdentifier.Uri, documentCache);
            }

            if (!documentCache.TryGetValue(csharpResponse.ResultId, out _))
            {
                documentCache.Set(csharpResponse.ResultId, csharpResponse.Data);
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
            return new VersionedSemanticRange(result, csharpResponse.ResultId, csharpResponse.IsPartial);
        }

        private async Task<SemanticTokensResponse?> GetMatchingCSharpResponseAsync(
            TextDocumentIdentifier textDocumentIdentifier,
            long documentVersion,
            CancellationToken cancellationToken)
        {
            var parameter = new ProvideSemanticTokensParams
            {
                TextDocument = textDocumentIdentifier,
                RequiredHostDocumentVersion = documentVersion,
            };
            var request = await _languageServer.SendRequestAsync(LanguageServerConstants.RazorProvideSemanticTokensEndpoint, parameter);
            var csharpResponse = await request.Returning<ProvideSemanticTokensResponse>(cancellationToken);

            if (csharpResponse is null)
            {
                // C# isn't ready yet, don't make Razor wait for it
                return new SemanticTokensResponse
                {
                    ResultId = null,
                };
            }
            else if (csharpResponse.HostDocumentSyncVersion != null && csharpResponse.HostDocumentSyncVersion != documentVersion)
            {
                // No C# response or C# is out of sync with us. Unrecoverable, return null to indicate no change. It will retry in a bit.
                return null;
            }

            return new SemanticTokensResponse
            {
                ResultId = csharpResponse.ResultId,
                Data = csharpResponse.Tokens ?? Array.Empty<int>(),
                IsPartial = csharpResponse.IsPartial
            };
        }

        private async Task<SemanticTokensResponse?> GetMatchingCSharpEditsResponseAsync(
            TextDocumentIdentifier textDocumentIdentifier,
            long documentVersion,
            string previousResultId,
            IReadOnlyList<int> previousCSharpTokens,
            CancellationToken cancellationToken)
        {
            var parameter = new ProvideSemanticTokensDeltaParams
            {
                TextDocument = textDocumentIdentifier,
                PreviousResultId = previousResultId,
                RequiredHostDocumentVersion = documentVersion,
            };
            var request = await _languageServer.SendRequestAsync(LanguageServerConstants.RazorProvideSemanticTokensEditsEndpoint, parameter);
            var csharpResponse = await request.Returning<ProvideSemanticTokensEditsResponse>(cancellationToken);

            if (csharpResponse is null ||
                (csharpResponse.HostDocumentSyncVersion != null && csharpResponse.HostDocumentSyncVersion != documentVersion))
            {
                // No C# response or C# is out of sync with us. Unrecoverable, return null to indicate no change. It will retry in a bit.
                return null;
            }

            if (csharpResponse.Edits is null)
            {
                Assumes.NotNull(csharpResponse.Tokens);

                // C#'s edits handler returned to us a full token set, so we don't need to do any additional work.
                // This may indicate that C# had trouble with cache retrieval or that the previous tokens were
                // 0-length, since C# doesn't cache 0-length token sets.
                return new SemanticTokensResponse
                {
                    ResultId = csharpResponse.ResultId,
                    Data = csharpResponse.Tokens.ToArray(),
                    IsPartial = csharpResponse.IsPartial
                };
            }

            Assumes.NotNull(csharpResponse.Edits);

            if (!csharpResponse.Edits.Any())
            {
                // If there aren't any edits, return the previous tokens with updated resultId.
                return new SemanticTokensResponse
                {
                    ResultId = csharpResponse.ResultId,
                    Data = previousCSharpTokens.ToArray(),
                    IsPartial = csharpResponse.IsPartial
                };
            }

            var updatedTokens = ApplyEditsToPreviousCSharpDoc(previousCSharpTokens, csharpResponse.Edits);

            return new SemanticTokensResponse
            {
                ResultId = csharpResponse.ResultId,
                Data = updatedTokens.ToArray(),
                IsPartial = csharpResponse.IsPartial
            };
        }

        // Internal for testing
        internal static int[] ApplyEditsToPreviousCSharpDoc(
            IReadOnlyList<int> previousCSharpTokens,
            Models.RazorSemanticTokensEdit[] edits)
        {
            var updatedTokens = previousCSharpTokens.ToList();
            var previousStartIndex = int.MaxValue;

            // C# returns edits ordered from lowest -> highest start index. We need to
            // apply these edits in reverse order since the returned C# start indices
            // are absolute indices.
            for (var i = edits.Length - 1; i >= 0; i--)
            {
                var edit = edits[i];
                Debug.Assert(previousStartIndex > edit.Start);
                previousStartIndex = edit.Start;

                updatedTokens.RemoveRange(edit.Start, edit.DeleteCount);
                if (edit.Data != null)
                {
                    updatedTokens.InsertRange(edit.Start, edit.Data);
                }
            }

            return updatedTokens.ToArray();
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
            bool isPartialCSharp)
        {
            SemanticRange? previousResult = null;

            var data = new List<int>();
            foreach (var result in semanticRanges)
            {
                var newData = GetData(result, previousResult, razorCodeDocument);
                data.AddRange(newData);

                previousResult = result;
            }

            var tokensResult = new SemanticTokensResponse
            {
                Data = data.ToArray(),
                ResultId = resultId,
                IsPartial = isPartialCSharp,
            };

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

        private async Task<(DocumentSnapshot Snapshot, int? Version)> TryGetDocumentInfoAsync(string absolutePath, CancellationToken cancellationToken)
        {
            var documentInfo = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                _documentResolver.TryResolveDocument(absolutePath, out var documentSnapshot);
                _documentVersionCache.TryGetDocumentVersion(documentSnapshot, out var version);

                return (documentSnapshot, version);
            }, cancellationToken);

            return documentInfo;
        }

        internal record VersionedSemanticRange(IReadOnlyList<SemanticRange>? SemanticRanges, string? ResultId, bool IsPartialCSharp)
        {
            public static VersionedSemanticRange Default => new(null, null, false);
        }

        private record VersionedSemanticTokens(VersionStamp? SemanticVersion, IReadOnlyList<int> SemanticTokens, bool IsPartialCSharp);
    }
}
