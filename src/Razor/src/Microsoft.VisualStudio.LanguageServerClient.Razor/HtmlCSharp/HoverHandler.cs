﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(Methods.TextDocumentHoverName)]
    internal class HoverHandler : IRequestHandler<TextDocumentPositionParams, Hover>
    {
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly LSPDocumentManager _documentManager;
        private readonly LSPProjectionProvider _projectionProvider;
        private readonly LSPDocumentMappingProvider _documentMappingProvider;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public HoverHandler(
            LSPRequestInvoker requestInvoker,
            LSPDocumentManager documentManager,
            LSPProjectionProvider projectionProvider,
            LSPDocumentMappingProvider documentMappingProvider,
            HTMLCSharpLanguageServerLogHubLoggerProvider loggerProvider)
        {
            if (requestInvoker is null)
            {
                throw new ArgumentNullException(nameof(requestInvoker));
            }

            if (documentManager is null)
            {
                throw new ArgumentNullException(nameof(documentManager));
            }

            if (projectionProvider is null)
            {
                throw new ArgumentNullException(nameof(projectionProvider));
            }

            if (documentMappingProvider is null)
            {
                throw new ArgumentNullException(nameof(documentMappingProvider));
            }

            if (loggerProvider == null)
            {
                throw new ArgumentNullException(nameof(loggerProvider));
            }

            _requestInvoker = requestInvoker;
            _documentManager = documentManager;
            _projectionProvider = projectionProvider;
            _documentMappingProvider = documentMappingProvider;

            _logger = loggerProvider.CreateLogger(nameof(HoverHandler));
        }

        public async Task<Hover> HandleRequestAsync(TextDocumentPositionParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (clientCapabilities is null)
            {
                throw new ArgumentNullException(nameof(clientCapabilities));
            }

            _logger.LogInformation($"Starting request for {request.TextDocument.Uri}.");

            if (!_documentManager.TryGetDocument(request.TextDocument.Uri, out var documentSnapshot))
            {
                _logger.LogWarning($"Failed to find document {request.TextDocument.Uri}.");
                return null;
            }

            var projectionResult = await _projectionProvider.GetProjectionAsync(
                documentSnapshot,
                request.Position,
                cancellationToken).ConfigureAwait(false);
            if (projectionResult == null)
            {
                return null;
            }

            var languageServerName = projectionResult.LanguageKind.ToContainedLanguageServerName();

            cancellationToken.ThrowIfCancellationRequested();

            var textDocumentPositionParams = new TextDocumentPositionParams()
            {
                Position = projectionResult.Position,
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = projectionResult.Uri
                }
            };

            _logger.LogInformation($"Requesting hovers for {projectionResult.Uri}.");

            var response = await _requestInvoker.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, Hover>(
                Methods.TextDocumentHoverName,
                languageServerName,
                textDocumentPositionParams,
                cancellationToken).ConfigureAwait(false);
            var result = response.Result;

            if (result?.Range is null || result?.Contents is null)
            {
                _logger.LogInformation("Received no results.");
                return null;
            }

            _logger.LogInformation("Received result, remapping.");

            var mappingResult = await _documentMappingProvider.MapToDocumentRangesAsync(
                projectionResult.LanguageKind,
                request.TextDocument.Uri,
                new[] { result.Range },
                cancellationToken).ConfigureAwait(false);
            if (mappingResult is null || mappingResult.Ranges[0].IsUndefined())
            {
                // Couldn't remap the edits properly. Returning hover at initial request position.
                _logger.LogInformation("Mapping failed");
                return CreateHover(result, new Range
                {
                    Start = request.Position,
                    End = request.Position
                });
            }
            else if (mappingResult.HostDocumentVersion != documentSnapshot.Version)
            {
                _logger.LogInformation($"Discarding result, document has changed. {documentSnapshot.Version} -> {mappingResult.HostDocumentVersion}");
                return null;
            }

            _logger.LogInformation("Returning hover result.");
            return CreateHover(result, mappingResult.Ranges[0]);
        }

        private static VSInternalHover CreateHover(Hover originalHover, Range range)
        {
            return new VSInternalHover
            {
                Contents = originalHover.Contents,
                Range = range,
                RawContent = originalHover is VSInternalHover originalVSHover ? originalVSHover.RawContent : null
            };
        }
    }
}
