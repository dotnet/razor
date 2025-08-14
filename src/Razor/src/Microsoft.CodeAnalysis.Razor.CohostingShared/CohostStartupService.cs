// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

[Export(typeof(AbstractRazorCohostLifecycleService))]
[method: ImportingConstructor]
internal sealed class CohostStartupService(
    [ImportMany] IEnumerable<Lazy<IRazorCohostStartupService>> lazyStartupServices,
    IRemoteServiceInvoker remoteServiceInvoker,
    LanguageServerFeatureOptions featureOptions,
    ILoggerFactory loggerFactory) : AbstractRazorCohostLifecycleService
{
    private readonly ImmutableArray<Lazy<IRazorCohostStartupService>> _lazyStartupServices = [.. lazyStartupServices];
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;
    private readonly LanguageServerFeatureOptions _featureOptions = featureOptions;

    public override Task LspServerIntializedAsync(CancellationToken cancellationToken)
    {
        // If cohosting is on, we have to intialize it early so we can un-suppress the source generator. This can be removed
        // when the suppression system is removed once cohosting is fully enabled.
        // Without this operations that might affect Razor files won't work until a Razor file is opened in the editor.
        if (_featureOptions.UseRazorCohostServer)
        {
            return _remoteServiceInvoker.InitializeAsync().AsTask();
        }

        return Task.CompletedTask;
    }

    public override async Task RazorActivatedAsync(ClientCapabilities clientCapabilities, RazorCohostRequestContext requestContext, CancellationToken cancellationToken)
    {
        // Normally loggers are fields, but this service gets created early so it's better to avoid the work in case Razor
        // never gets activated.
        var logger = loggerFactory.GetOrCreateLogger<CohostStartupService>();

        var capabilities = clientCapabilities.ToVSInternalClientCapabilities();
        var providers = _lazyStartupServices.SelectAndOrderByAsArray(p => p.Value, p => p.Order);

        foreach (var provider in providers)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation($"Razor extension startup cancelled.");
                return;
            }

            try
            {
                await provider.StartupAsync(capabilities, requestContext, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, $"Error initializing Razor startup service '{provider.GetType().Name}'");
            }
        }

        logger.LogInformation($"Razor extension startup finished.");
    }

    public override void Dispose()
    {
        _remoteServiceInvoker.UninitializeLspAsync().Forget();
    }
}
