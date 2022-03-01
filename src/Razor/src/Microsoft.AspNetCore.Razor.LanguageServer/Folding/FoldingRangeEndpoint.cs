// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
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
        private readonly ILogger _logger;

        public FoldingRangeEndpoint(
            RazorDocumentMappingService documentMappingService,
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            ClientNotifierServiceBase languageServer,
            DocumentVersionCache documentVersionCache,
            ILoggerFactory loggerFactory)
        {
            _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
            _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher ?? throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            _documentResolver = documentResolver ?? throw new ArgumentNullException(nameof(documentResolver));
            _documentVersionCache = documentVersionCache ?? throw new ArgumentNullException(nameof(documentVersionCache));
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

            Container<FoldingRange>? container = null;
            var retries = 0;
            const int MaxRetries = 5;

            while (container is null && ++retries <= MaxRetries)
            {
                try
                {
                    container = await HandleCoreAsync(@params, cancellationToken);
                }
                catch(Exception e)
                {
                    _logger.LogTrace(e, $"Try {retries} to get FoldingRange");
                }
            }

            if (retries > MaxRetries)
            {
                _logger.LogInformation($"Exceeded max retries of {MaxRetries}");
            }

            return container;
        }

        private async Task<Container<FoldingRange>?> HandleCoreAsync(FoldingRangeRequestParam @params, CancellationToken cancellationToken)
        {
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
                DocumentHostVersion = version,
                TextDocument = @params.TextDocument
            };

            var delegatedRequest = await _languageServer.SendRequestAsync(LanguageServerConstants.RazorFoldingRangeEndpoint, requestParams).ConfigureAwait(false);
            var foldingResponse = await delegatedRequest.Returning<RazorFoldingRangeResponse?>(cancellationToken).ConfigureAwait(false);

            if (foldingResponse is null)
            {
                return null;
            }

            List<FoldingRange> mappedRanges = new();

            foreach (var foldingRange in foldingResponse.CSharpRanges)
            {
                var range = new Range(
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

                if (_documentMappingService.TryMapFromProjectedDocumentRange(
                    codeDocument,
                    range,
                    out var mappedRange))
                {
                    mappedRanges.Add(new FoldingRange()
                    {
                        StartLine = mappedRange.Start.Line,
                        StartCharacter = mappedRange.Start.Character,
                        EndCharacter = mappedRange.End.Character,
                        EndLine = mappedRange.End.Line
                    });
                }
            }

            mappedRanges.AddRange(foldingResponse.HtmlRanges);
            return new Container<FoldingRange>(mappedRanges);
        }

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
