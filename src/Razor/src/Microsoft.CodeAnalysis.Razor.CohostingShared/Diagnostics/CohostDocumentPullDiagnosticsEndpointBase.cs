// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Telemetry;
using ExternalHandlers = Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost.Handlers;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

internal abstract class CohostDocumentPullDiagnosticsEndpointBase<TRequest, TResponse>(
    IIncompatibleProjectService incompatibleProjectService,
    IRemoteServiceInvoker remoteServiceInvoker,
    IHtmlRequestInvoker requestInvoker,
    IClientCapabilitiesService clientCapabilitiesService,
    ITelemetryReporter telemetryReporter,
    ILogger logger)
    : AbstractCohostDocumentEndpoint<TRequest, TResponse>(incompatibleProjectService)
    where TRequest : notnull
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;
    private readonly IHtmlRequestInvoker _requestInvoker = requestInvoker;
    private readonly IClientCapabilitiesService _clientCapabilitiesService = clientCapabilitiesService;
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;
    private readonly ILogger _logger = logger;

    // Tracks the first C# diagnostic computation for a document. The first successful completion
    // bootstraps Roslyn's IncrementalMemberEditAnalyzer, making all subsequent analyses fast and
    // incremental (~50ms vs ~1-3s). Only the first computation needs protection from cancellation.
    // This is a single-entry cache because Roslyn's IncrementalMemberEditAnalyzer itself only
    // caches the last document analyzed.
    private volatile BootstrapDiagnosticComputation? _activeBootstrap;
    private readonly object _bootstrapGate = new object();

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    protected abstract string LspMethodName { get; }
    protected abstract bool SupportsHtmlDiagnostics { get; }

    protected virtual LspDiagnostic[] ExtractHtmlDiagnostics(TResponse result)
    {
        throw new NotSupportedException("If SupportsHtmlDiagnostics is true, you must implement GetHtmlDiagnostics");
    }

    protected virtual TRequest CreateHtmlParams(Uri uri)
    {
        throw new NotSupportedException("If SupportsHtmlDiagnostics is true, you must implement CreateHtmlParams");
    }

    protected async Task<LspDiagnostic[]?> GetDiagnosticsAsync(TextDocument razorDocument, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid();
        using var _ = _telemetryReporter.TrackLspRequest(LspMethodName, LanguageServerConstants.RazorLanguageServerName, TelemetryThresholds.DiagnosticsRazorTelemetryThreshold, correlationId);

        // Diagnostics is a little different, because Roslyn is not designed to run diagnostics in OOP. Their system will transition to OOP
        // as it needs, but we have to start here in devenv. This is not as big a problem as it sounds, specifically for diagnostics, because
        // we only need to tell Roslyn the document we need diagnostics for. If we had to map positions or ranges etc. it would be worse
        // because we'd have to transition to our OOP to find out that info, then back here to get the diagnostics, then back to OOP to process.
        _logger.LogDebug($"Getting diagnostics for {razorDocument.FilePath}");

        var csharpTask = GetCSharpDiagnosticsAsync(razorDocument, correlationId, cancellationToken);
        var htmlTask = SupportsHtmlDiagnostics
            ? GetHtmlDiagnosticsAsync(razorDocument, correlationId, cancellationToken)
            : SpecializedTasks.EmptyArray<LspDiagnostic>();

        try
        {
            await Task.WhenAll(htmlTask, csharpTask).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e, $"Exception thrown in PullDiagnostic delegation");
            throw;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        var csharpDiagnostics = csharpTask.VerifyCompleted();
        var htmlDiagnostics = htmlTask.VerifyCompleted();

        _logger.LogDebug($"Calling OOP with the {csharpDiagnostics.Length} C# and {htmlDiagnostics.Length} Html diagnostics");
        var diagnostics = await _remoteServiceInvoker.TryInvokeAsync<IRemoteDiagnosticsService, ImmutableArray<LspDiagnostic>>(
            razorDocument.Project.Solution,
            (service, solutionInfo, cancellationToken) => service.GetDiagnosticsAsync(solutionInfo, razorDocument.Id, csharpDiagnostics, htmlDiagnostics, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        _logger.LogDebug($"Reporting {diagnostics.Length} diagnostics back to the client");
        return [.. diagnostics];
    }

    protected static Task<SourceGeneratedDocument?> TryGetGeneratedDocumentAsync(TextDocument razorDocument, CancellationToken cancellationToken)
    {
        return razorDocument.Project.TryGetSourceGeneratedDocumentForRazorDocumentAsync(razorDocument, cancellationToken);
    }

    private async Task<LspDiagnostic[]> GetCSharpDiagnosticsAsync(TextDocument razorDocument, Guid correletionId, CancellationToken cancellationToken)
    {
        var generatedDocument = await TryGetGeneratedDocumentAsync(razorDocument, cancellationToken).ConfigureAwait(false);
        if (generatedDocument is null || razorDocument.FilePath is not string filePath)
        {
            return [];
        }

        _logger.LogDebug($"Getting C# diagnostics for {generatedDocument.FilePath}");

        using var _ = _telemetryReporter.TrackLspRequest(LspMethodName, "Razor.ExternalAccess", TelemetryThresholds.DiagnosticsSubLSPTelemetryThreshold, correletionId);
        var supportsVisualStudioExtensions = _clientCapabilitiesService.ClientCapabilities.SupportsVisualStudioExtensions;

        // Check if this document already has a bootstrap computation. Once bootstrapped,
        // Roslyn's IncrementalMemberEditAnalyzer makes subsequent analyses fast (~50ms),
        // so they complete within the normal request lifetime and don't need protection.
        var bootstrap = _activeBootstrap;
        var isBootstrapRequest = false;
        if (bootstrap?.FilePath != filePath)
        {
            lock (_bootstrapGate)
            {
                bootstrap = _activeBootstrap; // re-read
                if (bootstrap?.FilePath != filePath)
                {
                    // First request for this document (or bootstrap was for a different doc).
                    // Use AsyncLazy with CancellationToken.None to survive cancellation for the bootstrap request.
                    var newLazy = AsyncLazy.Create(
                        asynchronousComputeFunction: static (state, _) => ExternalHandlers.Diagnostics.GetDocumentDiagnosticsAsync(
                            state.GeneratedDocument, state.SupportsVisualStudioExtensions, CancellationToken.None),
                        arg: (GeneratedDocument: generatedDocument, SupportsVisualStudioExtensions: supportsVisualStudioExtensions));

                    bootstrap = new BootstrapDiagnosticComputation(filePath, newLazy);
                    _activeBootstrap = bootstrap;
                    isBootstrapRequest = true;
                }
            }
        }

        try
        {
            // Pass cancellationToken so callers aren't blocked indefinitely. The AsyncLazy factory
            // uses CancellationToken.None, so the underlying computation continues even if this
            // caller is cancelled — ensuring the analyzer warmup side-effect still completes.
            var bootstrapResult = await bootstrap.Diagnostics.GetValueAsync(cancellationToken).ConfigureAwait(false);
            if (isBootstrapRequest)
            {
                return [.. bootstrapResult];
            }
        }
        catch (Exception ex)
        {
            if (isBootstrapRequest)
            {
                // On cancellation, the AsyncLazy factory continues running with CancellationToken.None
                // and will still warm up the analyzer. On a genuine fault, the computation is broken.
                // Clear the cache so the next request can create a fresh bootstrap.
                if (ex is not OperationCanceledException)
                {
                    lock (_bootstrapGate)
                    {
                        if (ReferenceEquals(_activeBootstrap, bootstrap))
                        {
                            _activeBootstrap = null;
                        }
                    }
                }

                // Our request failed, propagate the exception. Future requests will attempt to bootstrap again.
                throw;
            }
        }

        // Document is bootstrapped — use normal cancellation semantics with current document snapshot.
        return [.. await ExternalHandlers.Diagnostics.GetDocumentDiagnosticsAsync(
            generatedDocument, supportsVisualStudioExtensions, cancellationToken).ConfigureAwait(false)];
    }

    private async Task<LspDiagnostic[]> GetHtmlDiagnosticsAsync(TextDocument razorDocument, Guid correletionId, CancellationToken cancellationToken)
    {
        var diagnosticsParams = CreateHtmlParams(razorDocument.CreateUri());

        var result = await _requestInvoker.MakeHtmlLspRequestAsync<TRequest, TResponse>(
            razorDocument,
            LspMethodName,
            diagnosticsParams,
            TelemetryThresholds.DiagnosticsSubLSPTelemetryThreshold,
            correletionId,
            cancellationToken).ConfigureAwait(false);

        if (result is null)
        {
            return [];
        }

        return ExtractHtmlDiagnostics(result);
    }

    private sealed class BootstrapDiagnosticComputation(string filePath, AsyncLazy<ImmutableArray<LspDiagnostic>> diagnostics)
    {
        public string FilePath { get; } = filePath;
        public AsyncLazy<ImmutableArray<LspDiagnostic>> Diagnostics { get; } = diagnostics;
    }
}
