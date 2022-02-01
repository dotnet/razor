// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(Methods.TextDocumentImplementationName)]
    internal class GoToImplementationHandler : IRequestHandler<TextDocumentPositionParams, object>
    {
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly LSPDocumentManager _documentManager;
        private readonly LSPProjectionProvider _projectionProvider;
        private readonly LSPDocumentMappingProvider _documentMappingProvider;
        private readonly ILogger _logger;
        private readonly JsonSerializer _serializer;

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

            _serializer = new JsonSerializer();
            _serializer.AddVSInternalExtensionConverters();
        }

        public async Task<object?> HandleRequestAsync(TextDocumentPositionParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
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
            if (projectionResult is null)
            {
                return null;
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

            _logger.LogInformation($"Requesting {languageServerName} implementation for {projectionResult.Uri}.");

            var textBuffer = serverKind.GetTextBuffer(documentSnapshot);
            var response = await _requestInvoker.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, JToken>(
                textBuffer,
                Methods.TextDocumentImplementationName,
                languageServerName,
                textDocumentPositionParams,
                cancellationToken).ConfigureAwait(false);

            if (!ReinvocationResponseHelper.TryExtractResultOrLog(response, _logger, languageServerName, out var jToken))
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // From some language servers we get VSInternalReferenceItem results, and from some we get Location results.
            // We check for the _vs_id property, which is required in VSInternalReferenceItem, to know which is which.
            if (jToken.Type == JTokenType.Array)
            {
                var jArray = (JArray)jToken;
                if (jArray.Any())
                {
                    _logger.LogInformation($"Received {jArray.Count} results, remapping.");

                    cancellationToken.ThrowIfCancellationRequested();

                    if (jArray.First?["_vs_id"] is not null)
                    {
                        var referenceItems = jArray.ToObject<VSInternalReferenceItem[]>(_serializer);

                        var remappedLocations = await FindAllReferencesHandler.RemapReferenceItemsAsync(referenceItems, _documentMappingProvider, _documentManager, cancellationToken).ConfigureAwait(false);

                        _logger.LogInformation($"Returning {remappedLocations?.Length} internal reference items.");
                        return remappedLocations;
                    }
                    else
                    {
                        var locations = jArray.ToObject<Location[]>(_serializer);
                        if (locations is not null)
                        {
                            var remappedLocations = await _documentMappingProvider.RemapLocationsAsync(locations, cancellationToken).ConfigureAwait(false);

                            _logger.LogInformation($"Returning {remappedLocations?.Length} locations.");

                            return remappedLocations;
                        }
                    }
                }
            }

            _logger.LogInformation("Received no results.");
            return Array.Empty<VSInternalReferenceItem>();
        }
    }
}
