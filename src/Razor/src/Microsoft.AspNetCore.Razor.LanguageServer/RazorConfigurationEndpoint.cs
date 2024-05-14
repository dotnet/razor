﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class RazorConfigurationEndpoint(RazorLSPOptionsMonitor optionsMonitor, ILoggerFactory loggerFactory)
    : IDidChangeConfigurationEndpoint, IOnInitialized
{
    private readonly RazorLSPOptionsMonitor _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<RazorConfigurationEndpoint>();

    public bool MutatesSolutionState => true;

    public async Task HandleNotificationAsync(DidChangeConfigurationParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Settings changed. Updating the server.");

        await _optionsMonitor.UpdateAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task OnInitializedAsync(ILspServices services, CancellationToken cancellationToken)
    {
        var capabilitiesService = services.GetRequiredService<IClientCapabilitiesService>();
        var clientCapabilities = capabilitiesService.ClientCapabilities;

        if (clientCapabilities.Workspace?.Configuration == true)
        {
            await _optionsMonitor.UpdateAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
