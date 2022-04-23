// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Folding
{
    internal class FoldingRangeEndpoint : IFoldingRangeHandler
    {
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly ClientNotifierServiceBase _languageServer;
        private readonly DocumentVersionCache _documentVersionCache;
        private readonly IEnumerable<RazorFoldingRangeProvider> _foldingRangeProviders;
        private readonly ILogger _logger;

        public FoldingRangeEndpoint(
            RazorDocumentMappingService documentMappingService!!,
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher!!,
            DocumentResolver documentResolver!!,
            ClientNotifierServiceBase languageServer!!,
            DocumentVersionCache documentVersionCache!!,
            IEnumerable<RazorFoldingRangeProvider> foldingRangeProviders!!,
            ILoggerFactory loggerFactory)
        {
            _documentMappingService = documentMappingService;
            _languageServer = languageServer;
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _documentResolver = documentResolver;
            _documentVersionCache = documentVersionCache;
            _foldingRangeProviders = foldingRangeProviders;
            _logger = loggerFactory.CreateLogger<FoldingRangeEndpoint>();
        }

        public FoldingRangeRegistrationOptions GetRegistrationOptions(FoldingRangeCapability capability, ClientCapabilities clientCapabilities)
            => new()
            {
                DocumentSelector = RazorDefaults.Selector,
            };

        public async Task<Container<FoldingRange>?> Handle(FoldingRangeRequestParam @params, CancellationToken cancellationToken)
        {
            using var _ = _logger.BeginScope("FoldingRangeEndpoint.Handle");

            var documentAndVersion = await TryGetDocumentSnapshotAndVersionAsync(
                @params.TextDocument.Uri.GetAbsoluteOrUNCPath(),
                cancellationToken).ConfigureAwait(false);

            if (documentAndVersion is null)
            {
                return null;
            }

            var (document, version) = documentAndVersion;
            if (document is null || cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var codeDocument = await document.GetGeneratedOutputAsync().ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            var requestParams = new RazorFoldingRangeRequestParam
            {
                HostDocumentVersion = version,
                TextDocument = @params.TextDocument
            };

            Container<FoldingRange>? container = null;
            var retries = 0;
            const int MaxRetries = 5;

            while (container is null && ++retries <= MaxRetries)
            {
                try
                {
                    container = await HandleCoreAsync(requestParams, document, codeDocument, cancellationToken);
                }
                catch (Exception e) when (retries < MaxRetries)
                {
                    _logger.LogWarning(e, $"Try {retries} to get FoldingRange");
                }
            }

            return container;
        }

        private async Task<Container<FoldingRange>?> HandleCoreAsync(RazorFoldingRangeRequestParam requestParams, DocumentSnapshot documentSnapshot, RazorCodeDocument codeDocument, CancellationToken cancellationToken)
        {
            var delegatedRequest = await _languageServer.SendRequestAsync(LanguageServerConstants.RazorFoldingRangeEndpoint, requestParams).ConfigureAwait(false);
            var foldingResponse = await delegatedRequest.Returning<RazorFoldingRangeResponse?>(cancellationToken).ConfigureAwait(false);

            if (foldingResponse is null)
            {
                return null;
            }

            List<FoldingRange> mappedRanges = new();

            foreach (var foldingRange in foldingResponse.CSharpRanges)
            {
                var range = GetRange(foldingRange);

                if (_documentMappingService.TryMapFromProjectedDocumentRange(
                    codeDocument,
                    range,
                    out var mappedRange))
                {
                    mappedRanges.Add(GetFoldingRange(mappedRange));
                }
            }

            mappedRanges.AddRange(foldingResponse.HtmlRanges);

            foreach (var provider in _foldingRangeProviders)
            {
                var ranges = await provider.GetFoldingRangesAsync(codeDocument, documentSnapshot, cancellationToken);
                mappedRanges.AddRange(ranges);
            }

            var finalRanges = FinalizeFoldingRanges(mappedRanges, codeDocument);
            return new Container<FoldingRange>(finalRanges);
        }

        private IEnumerable<FoldingRange> FinalizeFoldingRanges(List<FoldingRange> mappedRanges, RazorCodeDocument codeDocument)
        {
            // Don't allow ranges to be reported if they aren't spanning at least one line
            var validRanges = mappedRanges.Where(r => r.StartLine < r.EndLine);

            // Reduce ranges that have the same start line to be a single instance with the largest
            // range available, since only one button can be shown to collapse per line
            var reducedRanges = validRanges
                .GroupBy(r => r.StartLine)
                .Select(ranges => ranges.OrderByDescending(r => r.EndLine).First());

            // Fix the starting range so the "..." is shown at the end
            return reducedRanges.Select(r => FixFoldingRangeStart(r, codeDocument));
        }

        /// <summary>
        /// Fixes the start of a range so that the offset of the first line is the last character on that line. This makes
        /// it so collapsing will still show the text instead of just "..."
        /// </summary>
        private FoldingRange FixFoldingRangeStart(FoldingRange range, RazorCodeDocument codeDocument)
        {
            Debug.Assert(range.StartLine < range.EndLine);

            var sourceText = codeDocument.GetSourceText();
            var startLine = range.StartLine;
            var lineSpan = sourceText.Lines[startLine].Span;

            // Search from the end of the line to the beginning for the first non whitespace character. We want that
            // to be the offset for the range
            var offset = sourceText.GetLastNonWhitespaceOffset(lineSpan, out _);

            if (offset.HasValue)
            {
                // +1 to the offset value because the helper goes to the character position
                // that we want to be after. Make sure we don't exceed the line end
                var newCharacter = Math.Min(offset.Value + 1, lineSpan.Length);
                return range with { StartCharacter = newCharacter };
            }

            return range;
        }

        private static Range GetRange(FoldingRange foldingRange)
            => new Range(
                start: new Position()
                {
                    Character = foldingRange.StartCharacter.GetValueOrDefault(),
                    Line = foldingRange.StartLine
                },
                end: new Position()
                {
                    Character = foldingRange.EndCharacter.GetValueOrDefault(),
                    Line = foldingRange.EndLine
                });

        private static FoldingRange GetFoldingRange(Range range)
           => new FoldingRange()
           {
               StartLine = range.Start.Line,
               StartCharacter = range.Start.Character,
               EndCharacter = range.End.Character,
               EndLine = range.End.Line
           };

        private record DocumentSnapshotAndVersion(DocumentSnapshot Snapshot, int Version);

        private Task<DocumentSnapshotAndVersion?> TryGetDocumentSnapshotAndVersionAsync(string uri, CancellationToken cancellationToken)
        {
            return _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                if (_documentResolver.TryResolveDocument(uri, out var documentSnapshot))
                {
                    if (_documentVersionCache.TryGetDocumentVersion(documentSnapshot, out var version))
                    {
                        return new DocumentSnapshotAndVersion(documentSnapshot, version.Value);
                    }
                }

                return null;
            }, cancellationToken);
        }
    }
}
