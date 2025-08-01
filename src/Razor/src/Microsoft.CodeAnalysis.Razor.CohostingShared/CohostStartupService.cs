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

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

[Export(typeof(AbstractRazorCohostLifecycleService))]
[method: ImportingConstructor]
internal sealed class CohostStartupService(
    [ImportMany] IEnumerable<Lazy<IRazorCohostStartupService>> lazyStartupServices,
    IRemoteServiceInvoker remoteServiceInvoker,
    ILoggerFactory loggerFactory) : AbstractRazorCohostLifecycleService
{
    private readonly ImmutableArray<Lazy<IRazorCohostStartupService>> _lazyStartupServices = [.. lazyStartupServices];
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;

    public override Task LspServerIntializedAsync(CancellationToken cancellationToken)
    {
        return _remoteServiceInvoker.InitializeAsync().AsTask();
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
    }
}
