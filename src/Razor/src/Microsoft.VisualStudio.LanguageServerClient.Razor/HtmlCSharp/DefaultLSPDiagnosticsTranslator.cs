// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;

#nullable enable

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [Export(typeof(LSPDiagnosticsTranslator))]
    internal class DefaultLSPDiagnosticsTranslator : LSPDiagnosticsTranslator
    {
        private readonly LSPDocumentManager _documentManager;
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public DefaultLSPDiagnosticsTranslator(
            LSPDocumentManager documentManager,
            LSPRequestInvoker requestInvoker,
            HTMLCSharpLanguageServerLogHubLoggerProvider loggerProvider)
        {
            _documentManager = documentManager;
            _requestInvoker = requestInvoker;
            _logger = loggerProvider.CreateLogger(nameof(DefaultLSPDiagnosticsTranslator));
        }

        public override async Task<RazorDiagnosticsResponse?> TranslateAsync(
            RazorLanguageKind languageKind,
            Uri razorDocumentUri,
            Diagnostic[] diagnostics,
            CancellationToken cancellationToken)
        {
            if (!_documentManager.TryGetDocument(razorDocumentUri, out var documentSnapshot))
            {
                return new RazorDiagnosticsResponse()
                {
                    Diagnostics = Array.Empty<Diagnostic>(),
                };
            }

            var diagnosticsParams = new RazorDiagnosticsParams()
            {
                Kind = languageKind,
                RazorDocumentUri = razorDocumentUri,
                Diagnostics = diagnostics
            };

            var response = await _requestInvoker.ReinvokeRequestOnServerAsync<RazorDiagnosticsParams, RazorDiagnosticsResponse>(
                documentSnapshot.Snapshot.TextBuffer,
                LanguageServerConstants.RazorTranslateDiagnosticsEndpoint,
                RazorLSPConstants.RazorLanguageServerName,
                diagnosticsParams,
                cancellationToken).ConfigureAwait(false);

            if (!ReinvocationResponseHelper.TryExtractResultOrLog(response, _logger, RazorLSPConstants.RazorLanguageServerName, out var result))
            {
                return null;
            }

            return result;
        }
    }
}
