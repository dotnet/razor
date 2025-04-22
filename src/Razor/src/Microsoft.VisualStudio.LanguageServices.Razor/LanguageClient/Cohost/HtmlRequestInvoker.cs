// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Telemetry;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

[Export(typeof(IHtmlRequestInvoker))]
[method: ImportingConstructor]
internal sealed class HtmlRequestInvoker(
    LSPRequestInvoker requestInvoker,
    LSPDocumentManager documentManager,
    IHtmlDocumentSynchronizer htmlDocumentSynchronizer,
    ITelemetryReporter telemetryReporter,
    ILoggerFactory loggerFactory) : IHtmlRequestInvoker
{
    private readonly LSPRequestInvoker _requestInvoker = requestInvoker;
    private readonly TrackingLSPDocumentManager _documentManager = documentManager as TrackingLSPDocumentManager ?? throw new InvalidOperationException("Expected TrackingLSPDocumentManager");
    private readonly IHtmlDocumentSynchronizer _htmlDocumentSynchronizer = htmlDocumentSynchronizer;
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<HtmlRequestInvoker>();

    public async Task<TResponse?> MakeHtmlLspRequestAsync<TRequest, TResponse>(TextDocument razorDocument, string method, TRequest request, TimeSpan threshold, Guid correlationId, CancellationToken cancellationToken) where TRequest : notnull
    {
        var syncResult = await _htmlDocumentSynchronizer.TrySynchronizeAsync(razorDocument, cancellationToken).ConfigureAwait(false);
        if (!syncResult)
        {
            _logger.LogDebug($"Couldn't synchronize for {razorDocument.FilePath}");
            return default;
        }

        if (!_documentManager.TryGetDocument(razorDocument.CreateUri(), out var snapshot))
        {
            _logger.LogError($"Couldn't find document in LSPDocumentManager for {razorDocument.FilePath}");
            return default;
        }

        if (!snapshot.TryGetVirtualDocument<HtmlVirtualDocumentSnapshot>(out var htmlDocument))
        {
            _logger.LogError($"Couldn't find virtual document snapshot for {snapshot.Uri}");
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
                htmlDocument.Snapshot.TextBuffer,
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
