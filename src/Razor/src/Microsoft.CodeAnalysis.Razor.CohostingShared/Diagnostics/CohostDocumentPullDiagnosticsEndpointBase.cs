// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Telemetry;
using ExternalHandlers = Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost.Handlers;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

internal abstract class CohostDocumentPullDiagnosticsEndpointBase(
    IRemoteServiceInvoker remoteServiceInvoker,
    IHtmlRequestInvoker requestInvoker,
    ITelemetryReporter telemetryReporter,
    ILoggerFactory loggerFactory)
    : AbstractRazorCohostDocumentRequestHandler<VSInternalDocumentDiagnosticsParams, VSInternalDiagnosticReport[]?>
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;
    private readonly IHtmlRequestInvoker _requestInvoker = requestInvoker;
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<CohostDocumentPullDiagnosticsEndpointBase>();

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(VSInternalDocumentDiagnosticsParams request)
        => request.TextDocument?.ToRazorTextDocumentIdentifier();

    protected async Task<VSInternalDiagnosticReport[]?> HandleRequestAsync(TextDocument razorDocument, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        using var _ = _telemetryReporter.TrackLspRequest(Methods.TextDocumentCompletionName, LanguageServerConstants.RazorLanguageServerName, TelemetryThresholds.DiagnosticsRazorTelemetryThreshold, correlationId);

        // Diagnostics is a little different, because Roslyn is not designed to run diagnostics in OOP. Their system will transition to OOP
        // as it needs, but we have to start here in devenv. This is not as big a problem as it sounds, specifically for diagnostics, because
        // we only need to tell Roslyn the document we need diagnostics for. If we had to map positions or ranges etc. it would be worse
        // because we'd have to transition to our OOP to find out that info, then back here to get the diagnostics, then back to OOP to process.
        _logger.LogDebug($"Getting diagnostics for {razorDocument.FilePath}");

        var csharpTask = GetCSharpDiagnosticsAsync(razorDocument, correlationId, cancellationToken);
        var htmlTask = GetHtmlDiagnosticsAsync(razorDocument, correlationId, cancellationToken);

        try
        {
            await Task.WhenAll(htmlTask, csharpTask).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            if (e is not OperationCanceledException)
            {
                _logger.LogError(e, $"Exception thrown in PullDiagnostic delegation");
                throw;
            }
        }

        var csharpDiagnostics = await csharpTask.ConfigureAwait(false);
        var htmlDiagnostics = await htmlTask.ConfigureAwait(false);

        _logger.LogDebug($"Calling OOP with the {csharpDiagnostics.Length} C# and {htmlDiagnostics.Length} Html diagnostics");
        var diagnostics = await _remoteServiceInvoker.TryInvokeAsync<IRemoteDiagnosticsService, ImmutableArray<LspDiagnostic>>(
            razorDocument.Project.Solution,
            (service, solutionInfo, cancellationToken) => service.GetDiagnosticsAsync(solutionInfo, razorDocument.Id, csharpDiagnostics, htmlDiagnostics, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        if (diagnostics.IsDefaultOrEmpty)
        {
            return null;
        }

        _logger.LogDebug($"Reporting {diagnostics.Length} diagnostics back to the client");
        return
        [
            new()
            {
                Diagnostics = diagnostics.ToArray(),
                ResultId = Guid.NewGuid().ToString()
            }
        ];
    }

    private async Task<LspDiagnostic[]> GetCSharpDiagnosticsAsync(TextDocument razorDocument, Guid correletionId, CancellationToken cancellationToken)
    {
        if (!razorDocument.TryComputeHintNameFromRazorDocument(out var hintName) ||
            await razorDocument.Project.TryGetSourceGeneratedDocumentFromHintNameAsync(hintName, cancellationToken).ConfigureAwait(false) is not { } generatedDocument)
        {
            return [];
        }

        _logger.LogDebug($"Getting C# diagnostics for {generatedDocument.FilePath}");

        using var _ = _telemetryReporter.TrackLspRequest(VSInternalMethods.DocumentPullDiagnosticName, "Razor.ExternalAccess", TelemetryThresholds.DiagnosticsSubLSPTelemetryThreshold, correletionId);
        var diagnostics = await ExternalHandlers.Diagnostics.GetDocumentDiagnosticsAsync(generatedDocument, supportsVisualStudioExtensions: true, cancellationToken).ConfigureAwait(false);
        return diagnostics.ToArray();
    }

    private async Task<LspDiagnostic[]> GetHtmlDiagnosticsAsync(TextDocument razorDocument, Guid correletionId, CancellationToken cancellationToken)
    {
        var diagnosticsParams = new VSInternalDocumentDiagnosticsParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = razorDocument.CreateUri() }
        };

        var result = await _requestInvoker.MakeHtmlLspRequestAsync<VSInternalDocumentDiagnosticsParams, VSInternalDiagnosticReport[]>(
            razorDocument,
#if VSCODE
            Methods.TextDocumentDiagnosticName,
#else
            VSInternalMethods.DocumentPullDiagnosticName,
#endif
            diagnosticsParams,
            TelemetryThresholds.DiagnosticsSubLSPTelemetryThreshold,
            correletionId,
            cancellationToken).ConfigureAwait(false);

        if (result is null)
        {
            return [];
        }

        using var allDiagnostics = new PooledArrayBuilder<LspDiagnostic>();
        foreach (var report in result)
        {
            if (report.Diagnostics is not null)
            {
                allDiagnostics.AddRange(report.Diagnostics);
            }
        }

        return allDiagnostics.ToArray();
    }
}
