// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.SemanticTokens;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Razor.Remote;

[Export(typeof(IRemoteClientProvider))]
[method: ImportingConstructor]
internal sealed class RemoteClientProvider(
    IWorkspaceProvider workspaceProvider,
    LanguageServerFeatureOptions languageServerFeatureOptions,
    IClientCapabilitiesService clientCapabilitiesService,
    ISemanticTokensLegendService semanticTokensLegendService,
    ITelemetryReporter telemetryReporter,
    ILoggerFactory loggerFactory)
    : IRemoteClientProvider
{
    private readonly IWorkspaceProvider _workspaceProvider = workspaceProvider;
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions = languageServerFeatureOptions;
    private readonly IClientCapabilitiesService _clientCapabilitiesService = clientCapabilitiesService;
    private readonly ISemanticTokensLegendService _semanticTokensLegendService = semanticTokensLegendService;
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<RemoteClientProvider>();

    private bool _isInitialized;
    private bool _isLSPInitialized;

    public async ValueTask<TResult?> TryInvokeAsync<TService, TResult>(Solution solution, Func<TService, RazorPinnedSolutionInfoWrapper, CancellationToken, ValueTask<TResult>> invocation, CancellationToken cancellationToken)
        where TService : class
    {
        var client = await TryGetClientAsync(cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            _logger.LogError($"Couldn't get remote client for {typeof(TService).Name} service");
            _telemetryReporter.ReportEvent("OOPClientFailure", Severity.Normal, new Property("service", typeof(TService).FullName));
            return default;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return default;
        }

        try
        {
            var result = await client.TryInvokeAsync(solution, invocation, cancellationToken).ConfigureAwait(false);

            return result.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error calling remote method for {typeof(TService).Name} service, invocation: ${invocation.ToString()}");
            _telemetryReporter.ReportFault(ex, "Exception calling remote method for {service}", typeof(TService).FullName);
            return default;
        }
    }

    private async Task<RazorRemoteHostClient?> TryGetClientAsync(CancellationToken cancellationToken)
    {
        var workspace = _workspaceProvider.GetWorkspace();

        var remoteClient = await RazorRemoteHostClient.TryGetClientAsync(
            workspace.Services,
            RazorServices.Descriptors,
            RazorRemoteServiceCallbackDispatcherRegistry.Empty,
            cancellationToken).ConfigureAwait(false);

        if (remoteClient is null)
        {
            return null;
        }

        await InitializeRemoteClientAsync(remoteClient, cancellationToken).ConfigureAwait(false);

        return remoteClient;
    }

    private async Task InitializeRemoteClientAsync(RazorRemoteHostClient remoteClient, CancellationToken cancellationToken)
    {
        if (!_isInitialized)
        {
            var initParams = new RemoteClientInitializationOptions
            {
                UseRazorCohostServer = _languageServerFeatureOptions.UseRazorCohostServer,
                UsePreciseSemanticTokenRanges = _languageServerFeatureOptions.UsePreciseSemanticTokenRanges,
                CSharpVirtualDocumentSuffix = _languageServerFeatureOptions.CSharpVirtualDocumentSuffix,
                HtmlVirtualDocumentSuffix = _languageServerFeatureOptions.HtmlVirtualDocumentSuffix,
                IncludeProjectKeyInGeneratedFilePath = _languageServerFeatureOptions.IncludeProjectKeyInGeneratedFilePath,
            };

            await remoteClient.TryInvokeAsync<IRemoteClientInitializationService>(
                (s, ct) => s.InitializeAsync(initParams, ct),
                cancellationToken).ConfigureAwait(false);

            _isInitialized = true;
        }

        if (!_isLSPInitialized && _clientCapabilitiesService.CanGetClientCapabilities)
        {
            var initParams = new RemoteClientLSPInitializationOptions
            {
                TokenTypes = _semanticTokensLegendService.TokenTypes.All,
                TokenModifiers = _semanticTokensLegendService.TokenModifiers.All,
            };

            await remoteClient.TryInvokeAsync<IRemoteClientInitializationService>(
                (s, ct) => s.InitializeLSPAsync(initParams, ct),
                cancellationToken).ConfigureAwait(false);

            _isLSPInitialized = true;
        }
    }
}
