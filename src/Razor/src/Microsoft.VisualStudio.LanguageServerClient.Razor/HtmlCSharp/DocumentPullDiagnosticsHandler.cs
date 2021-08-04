﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(VSInternalMethods.DocumentPullDiagnosticName)]
    internal class DocumentPullDiagnosticsHandler :
        IRequestHandler<VSInternalDocumentDiagnosticsParams, VSInternalDiagnosticReport[]>
    {
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly LSPDocumentManager _documentManager;
        private readonly LSPDocumentSynchronizer _documentSynchronizer;
        private readonly LSPDiagnosticsTranslator _diagnosticsProvider;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public DocumentPullDiagnosticsHandler(
            LSPRequestInvoker requestInvoker,
            LSPDocumentManager documentManager,
            LSPDocumentSynchronizer documentSynchronizer,
            LSPDiagnosticsTranslator diagnosticsProvider,
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

            if (documentSynchronizer is null)
            {
                throw new ArgumentNullException(nameof(documentSynchronizer));
            }

            if (diagnosticsProvider is null)
            {
                throw new ArgumentNullException(nameof(diagnosticsProvider));
            }

            if (loggerProvider == null)
            {
                throw new ArgumentNullException(nameof(loggerProvider));
            }

            _requestInvoker = requestInvoker;
            _documentManager = documentManager;
            _documentSynchronizer = documentSynchronizer;
            _diagnosticsProvider = diagnosticsProvider;

            _logger = loggerProvider.CreateLogger(nameof(DocumentPullDiagnosticsHandler));
        }

        // Internal for testing
        public async Task<VSInternalDiagnosticReport[]> HandleRequestAsync(VSInternalDocumentDiagnosticsParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
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

            if (!documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpDoc))
            {
                _logger.LogWarning($"Failed to find virtual C# document for {request.TextDocument.Uri}.");
                return null;
            }

            var synchronized = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync(
                documentSnapshot.Version,
                csharpDoc,
                cancellationToken).ConfigureAwait(false);
            if (!synchronized)
            {
                _logger.LogInformation($"Failed to synchronize document {csharpDoc.Uri}.");

                // Could not synchronize, report nothing changed
                return new VSInternalDiagnosticReport[]
                {
                    new VSInternalDiagnosticReport()
                    {
                        ResultId = request.PreviousResultId,
                        Diagnostics = null
                    }
                };
            }

            var referenceParams = new VSInternalDocumentDiagnosticsParams()
            {
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = csharpDoc.Uri
                },
                PreviousResultId = request.PreviousResultId
            };

            _logger.LogInformation($"Requesting diagnostics for {csharpDoc.Uri} with previous result Id of {request.PreviousResultId}.");

            // End goal is to transition this from ReinvokeRequestOnMultipleServersAsync -> ReinvokeRequestOnServerAsync
            // We can't do this right now as we don't have the ability to specify the language client name we'd like to make the call out to
            // https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1246135
            var resultsFromAllLanguageServers = await _requestInvoker.ReinvokeRequestOnMultipleServersAsync<VSInternalDocumentDiagnosticsParams, VSInternalDiagnosticReport[]>(
                VSInternalMethods.DocumentPullDiagnosticName,
                RazorLSPConstants.CSharpContentTypeName,
                referenceParams,
                cancellationToken).ConfigureAwait(false);

            var results = resultsFromAllLanguageServers.SelectMany(l => l.Result).ToArray();

            _logger.LogInformation($"Received {results?.Length} diagnostic reports.");

            var processedResults = await RemapDocumentDiagnosticsAsync(
                results,
                request.TextDocument.Uri,
                cancellationToken).ConfigureAwait(false);

            // | ---------------------------------------------------------------------------------- |
            // |                       LSP Platform Expected Response Semantics                     |
            // | ---------------------------------------------------------------------------------- |
            // | DiagnosticReport.Diagnostics     | DiagnosticReport.ResultId | Meaning             |
            // | -------------------------------- | ------------------------- | ------------------- |
            // | `null`                           | `null`                    | document gone       |
            // | `null`                           | valid                     | nothing changed     |
            // | valid (non-null including empty) | valid                     | diagnostics changed |
            // | ---------------------------------------------------------------------------------- |
            return processedResults;
        }

        private async Task<VSInternalDiagnosticReport[]> RemapDocumentDiagnosticsAsync(
            VSInternalDiagnosticReport[] unmappedDiagnosticReports,
            Uri razorDocumentUri,
            CancellationToken cancellationToken)
        {
            if (unmappedDiagnosticReports?.Any() != true)
            {
                return unmappedDiagnosticReports;
            }

            var mappedDiagnosticReports = new List<VSInternalDiagnosticReport>(unmappedDiagnosticReports.Length);

            foreach (var diagnosticReport in unmappedDiagnosticReports)
            {
                // Check if there are any diagnostics in this report
                if (diagnosticReport?.Diagnostics?.Any() != true)
                {
                    _logger.LogInformation("Diagnostic report contained no diagnostics.");
                    mappedDiagnosticReports.Add(diagnosticReport);
                    continue;
                }

                _logger.LogInformation($"Requesting processing of {diagnosticReport.Diagnostics.Length} diagnostics.");

                var processedDiagnostics = await _diagnosticsProvider.TranslateAsync(
                    RazorLanguageKind.CSharp,
                    razorDocumentUri,
                    diagnosticReport.Diagnostics,
                    cancellationToken
                ).ConfigureAwait(false);

                if (!_documentManager.TryGetDocument(razorDocumentUri, out var documentSnapshot) ||
                    documentSnapshot.Version != processedDiagnostics.HostDocumentVersion)
                {
                    _logger.LogInformation($"Document version mismatch, discarding {diagnosticReport.Diagnostics.Length} diagnostics.");

                    // We choose to discard diagnostics in this case & report nothing changed.
                    diagnosticReport.Diagnostics = null;
                    mappedDiagnosticReports.Add(diagnosticReport);
                    continue;
                }

                _logger.LogInformation($"Returning {processedDiagnostics.Diagnostics.Length} diagnostics.");
                diagnosticReport.Diagnostics = processedDiagnostics.Diagnostics;

                mappedDiagnosticReports.Add(diagnosticReport);
            }

            return mappedDiagnosticReports.ToArray();
        }
    }
}
