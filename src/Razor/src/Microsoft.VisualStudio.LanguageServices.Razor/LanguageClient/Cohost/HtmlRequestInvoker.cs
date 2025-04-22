// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Telemetry;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

[Export(typeof(IHtmlRequestInvoker))]
[method: ImportingConstructor]
internal sealed class HtmlRequestInvoker(
    LSPRequestInvoker requestInvoker,
    IHtmlDocumentSynchronizer htmlDocumentSynchronizer,
    ITelemetryReporter telemetryReporter,
    ILoggerFactory loggerFactory) : IHtmlRequestInvoker
{
    private readonly LSPRequestInvoker _requestInvoker = requestInvoker;
    private readonly IHtmlDocumentSynchronizer _htmlDocumentSynchronizer = htmlDocumentSynchronizer;
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<HtmlRequestInvoker>();

    public async Task<TResponse?> MakeHtmlLspRequestAsync<TRequest, TResponse>(TextDocument razorDocument, string method, TRequest request, TimeSpan threshold, Guid correlationId, CancellationToken cancellationToken) where TRequest : notnull
    {
        var htmlDocument = await _htmlDocumentSynchronizer.TryGetSynchronizedHtmlDocumentAsync(razorDocument, cancellationToken).ConfigureAwait(false);
        if (htmlDocument is null)
        {
            return default;
        }

        Uri? originalUri = null;
        try
        {
            if (request is ITextDocumentParams textDocumentRequest)
            {
                originalUri = textDocumentRequest.TextDocument.Uri;
                textDocumentRequest.TextDocument.Uri = htmlDocument.Uri;
            }

            _logger.LogDebug($"Making LSP request for {method} from {htmlDocument.Uri}{(request is ITextDocumentPositionParams positionParams ? $" at {positionParams.Position}" : "")}.");

            // Passing Guid.Empty to this method will mean no tracking
            using var _ = _telemetryReporter.TrackLspRequest(Methods.TextDocumentCodeActionName, RazorLSPConstants.HtmlLanguageServerName, threshold, correlationId);

            var result = await _requestInvoker.ReinvokeRequestOnServerAsync<TRequest, TResponse?>(
                htmlDocument.Buffer,
                method,
                RazorLSPConstants.HtmlLanguageServerName,
                request,
                cancellationToken).ConfigureAwait(false);

            if (result is null)
            {
                return default;
            }

            return result.Response;
        }
        finally
        {
            if (request is ITextDocumentParams textDocumentRequest)
            {
                textDocumentRequest.TextDocument.Uri = originalUri.AssumeNotNull();
            }
        }
    }
}
