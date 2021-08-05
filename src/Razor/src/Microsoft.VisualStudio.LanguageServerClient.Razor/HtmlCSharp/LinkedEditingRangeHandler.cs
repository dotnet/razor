// Copyright (c) .NET Foundation. All rights reserved.
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
    [ExportLspMethod(Methods.TextDocumentLinkedEditingRangeName)]
    internal class LinkedEditingRangeHandler : IRequestHandler<LinkedEditingRangeParams, LinkedEditingRanges>
    {
        private readonly LSPDocumentManager _documentManager;
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly LSPProjectionProvider _projectionProvider;
        private readonly LSPDocumentMappingProvider _documentMappingProvider;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public LinkedEditingRangeHandler(
            LSPDocumentManager documentManager,
            LSPRequestInvoker requestInvoker,
            LSPProjectionProvider projectionProvider,
            LSPDocumentMappingProvider documentMappingProvider,
            HTMLCSharpLanguageServerLogHubLoggerProvider loggerProvider)
        {
            if (documentManager is null)
            {
                throw new ArgumentNullException(nameof(documentManager));
            }

            if (requestInvoker is null)
            {
                throw new ArgumentNullException(nameof(requestInvoker));
            }

            if (projectionProvider is null)
            {
                throw new ArgumentNullException(nameof(projectionProvider));
            }

            if (documentMappingProvider is null)
            {
                throw new ArgumentNullException(nameof(documentMappingProvider));
            }

            if (loggerProvider is null)
            {
                throw new ArgumentNullException(nameof(loggerProvider));
            }

            _documentManager = documentManager;
            _requestInvoker = requestInvoker;
            _projectionProvider = projectionProvider;
            _documentMappingProvider = documentMappingProvider;

            _logger = loggerProvider.CreateLogger(nameof(LinkedEditingRangeHandler));
        }

        public async Task<LinkedEditingRanges> HandleRequestAsync(LinkedEditingRangeParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

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
            if (projectionResult is null)
            {
                return null;
            }
            else if (projectionResult.LanguageKind != RazorLanguageKind.Html)
            {
                _logger.LogInformation($"Unsupported language kind {projectionResult.LanguageKind:G}.");
                return null;
            }

            var linkedEditingRangeParams = new LinkedEditingRangeParams()
            {
                Position = projectionResult.Position,
                TextDocument = new TextDocumentIdentifier() { Uri = projectionResult.Uri }
            };

            _logger.LogInformation($"Requesting LinkedEditingRange for {projectionResult.Uri}.");

            var languageServerName = projectionResult.LanguageKind.ToContainedLanguageServerName();
            var linkedEditingRangeResponse = await _requestInvoker.ReinvokeRequestOnServerAsync<LinkedEditingRangeParams, LinkedEditingRanges>(
                Methods.TextDocumentLinkedEditingRangeName,
                languageServerName,
                linkedEditingRangeParams,
                cancellationToken).ConfigureAwait(false);

            var linkedEditingRangeResult = linkedEditingRangeResponse.Result;
            if (linkedEditingRangeResult is null)
            {
                _logger.LogInformation("Received no results.");
                return null;
            }

            _logger.LogInformation($"Received response, remapping.");

            var mappingResult = await _documentMappingProvider.MapToDocumentRangesAsync(
                projectionResult.LanguageKind,
                request.TextDocument.Uri,
                linkedEditingRangeResult.Ranges,
                cancellationToken).ConfigureAwait(false);

            if (mappingResult is null ||
                (_documentManager.TryGetDocument(request.TextDocument.Uri, out var mappedDocumentSnapshot) &&
                mappingResult.HostDocumentVersion != mappedDocumentSnapshot.Version))
            {
                // Couldn't remap the range or the document changed in the meantime. Discard this result.
                _logger.LogInformation($"Mapping failed. Versions: {documentSnapshot.Version} -> {mappingResult?.HostDocumentVersion}.");
                return null;
            }

            linkedEditingRangeResult.Ranges = mappingResult.Ranges;
            _logger.LogInformation("Returned remapped result.");
            return linkedEditingRangeResult;
        }
    }
}
