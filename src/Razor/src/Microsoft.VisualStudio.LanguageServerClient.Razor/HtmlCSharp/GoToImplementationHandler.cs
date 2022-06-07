// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(Methods.TextDocumentImplementationName)]
    internal class GoToImplementationHandler : IRequestHandler<TextDocumentPositionParams, SumType<Location[]?, VSInternalReferenceItem[]?>>
    {
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly LSPDocumentManager _documentManager;
        private readonly LSPProjectionProvider _projectionProvider;
        private readonly LSPDocumentMappingProvider _documentMappingProvider;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public GoToImplementationHandler(
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

            if (loggerProvider is null)
            {
                throw new ArgumentNullException(nameof(loggerProvider));
            }

            _requestInvoker = requestInvoker;
            _documentManager = documentManager;
            _projectionProvider = projectionProvider;
            _documentMappingProvider = documentMappingProvider;

            _logger = loggerProvider.CreateLogger(nameof(GoToImplementationHandler));
        }

        public async Task<SumType<Location[]?, VSInternalReferenceItem[]?>> HandleRequestAsync(TextDocumentPositionParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (clientCapabilities is null)
            {
                throw new ArgumentNullException(nameof(clientCapabilities));
            }

            _logger.LogInformation("Starting request for {request.TextDocument.Uri}.", request.TextDocument.Uri);

            if (!_documentManager.TryGetDocument(request.TextDocument.Uri, out var documentSnapshot))
            {
                _logger.LogWarning("Failed to find document {request.TextDocument.Uri}.", request.TextDocument.Uri);
                return new();
            }

            var projectionResult = await _projectionProvider.GetProjectionAsync(
                documentSnapshot,
                request.Position,
                cancellationToken).ConfigureAwait(false);
            if (projectionResult is null)
            {
                return new();
            }

            cancellationToken.ThrowIfCancellationRequested();

            var textDocumentPositionParams = new TextDocumentPositionParams()
            {
                Position = projectionResult.Position,
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = projectionResult.Uri
                }
            };

            var serverKind = projectionResult.LanguageKind.ToLanguageServerKind();
            var languageServerName = serverKind.ToLanguageServerName();

            _logger.LogInformation("Requesting {languageServerName} implementation for {projectionResult.Uri}.", languageServerName, projectionResult.Uri);

            var textBuffer = serverKind.GetTextBuffer(documentSnapshot);
            var response = await _requestInvoker.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, SumType<Location[]?, VSInternalReferenceItem[]?>>(
                textBuffer,
                Methods.TextDocumentImplementationName,
                languageServerName,
                textDocumentPositionParams,
                cancellationToken).ConfigureAwait(false);

            if (!ReinvocationResponseHelper.TryExtractResultOrLog(response, _logger, languageServerName, out var result))
            {
                return new();
            }

            cancellationToken.ThrowIfCancellationRequested();

            // From some language servers we get VSInternalReferenceItem results, and from some we get Location results.
            // We check for the _vs_id property, which is required in VSInternalReferenceItem, to know which is which.
            if (result.Value is VSInternalReferenceItem[] referenceItems)
            {
                var remappedLocations = await FindAllReferencesHandler.RemapReferenceItemsAsync(referenceItems, _documentMappingProvider, _documentManager, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Returning {remappedLocations?.Length} internal reference items.", remappedLocations?.Length);
                return remappedLocations;
            }
            else if (result.Value is Location[] locations)
            {
                var remappedLocations = await _documentMappingProvider.RemapLocationsAsync(locations, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Returning {remappedLocations?.Length} locations.", remappedLocations?.Length);

                return remappedLocations;
            }

            _logger.LogInformation("Received no results.");
            return Array.Empty<VSInternalReferenceItem>();
        }
    }
}
