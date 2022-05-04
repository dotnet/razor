// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    internal abstract class PresentationHandlerBase<TParams> where TParams : notnull
    {
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly LSPDocumentManager _documentManager;
        private readonly LSPProjectionProvider _projectionProvider;
        private readonly LSPDocumentMappingProvider _documentMappingProvider;
        private readonly ILogger _logger;

        protected abstract string MethodName { get; }

        protected PresentationHandlerBase(
            LSPRequestInvoker requestInvoker,
            LSPDocumentManager documentManager,
            LSPProjectionProvider projectionProvider,
            LSPDocumentMappingProvider documentMappingProvider,
            HTMLCSharpLanguageServerLogHubLoggerProvider loggerProvider)
        {
            _requestInvoker = requestInvoker;
            _documentManager = documentManager;
            _projectionProvider = projectionProvider;
            _documentMappingProvider = documentMappingProvider;

            _logger = loggerProvider.CreateLogger(nameof(PresentationHandlerBase<TParams>));
        }

        protected abstract TextDocumentIdentifier GetTextDocumentIdentifier(TParams request);

        protected abstract Range GetRange(TParams request);

        public Task<WorkspaceEdit?> HandleRequestAsync(TParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            var textDocument = GetTextDocumentIdentifier(request);
            var range = GetRange(request);

            return HandleRequestAsync(request, textDocument, range, cancellationToken);
        }

        private async Task<WorkspaceEdit?> HandleRequestAsync(TParams request, TextDocumentIdentifier textDocumentIdentifier, Range range, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Starting request for {textDocumentIdentifier.Uri}.");

            if (!_documentManager.TryGetDocument(textDocumentIdentifier.Uri, out var documentSnapshot))
            {
                return null;
            }

            if (!documentSnapshot.TryGetVirtualDocument<HtmlVirtualDocumentSnapshot>(out var htmlDoc))
            {
                _logger.LogWarning($"Failed to find virtual HTML document for {textDocumentIdentifier.Uri}.");
                return null;
            }

            var projectionResult = await _projectionProvider.GetProjectionAsync(documentSnapshot, range.Start, cancellationToken).ConfigureAwait(false);
            if (projectionResult is null)
            {
                return null;
            }

            if (projectionResult.LanguageKind is not (RazorLanguageKind.Html or RazorLanguageKind.CSharp))
            {
                _logger.LogInformation($"Unsupported language {projectionResult.LanguageKind:G}.");
                return null;
            }

            var languageServerName = projectionResult.LanguageKind == RazorLanguageKind.CSharp ? RazorLSPConstants.RazorCSharpLanguageServerName : RazorLSPConstants.HtmlLanguageServerName;

            var textBuffer = htmlDoc.Snapshot.TextBuffer;
            var result = await _requestInvoker.ReinvokeRequestOnServerAsync<TParams, WorkspaceEdit?>(
                textBuffer,
                MethodName,
                languageServerName,
                request,
                cancellationToken).ConfigureAwait(false);

            if (result?.Response is null)
            {
                _logger.LogInformation($"Received no result from language server.");

                return null;
            }

            _logger.LogInformation($"Received a result from the language server, remapping.");

            return await _documentMappingProvider.RemapWorkspaceEditAsync(result.Response, cancellationToken);
        }
    }
}
