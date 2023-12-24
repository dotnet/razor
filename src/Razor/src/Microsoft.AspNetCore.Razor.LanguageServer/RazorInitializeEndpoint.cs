// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

[LanguageServerEndpoint(Methods.InitializeName)]
internal class RazorInitializeEndpoint : IRazorDocumentlessRequestHandler<InitializeParams, InitializeResult>
{
    public bool MutatesSolutionState { get; } = true;

    public Task<InitializeResult> HandleRequestAsync(InitializeParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        var featureOptions = requestContext.GetRequiredService<LanguageServerFeatureOptions>();

        // If the user hasn't turned on the Cohost server, then don't disable the Razor server either
        if (featureOptions.UseRazorCohostServer && featureOptions.DisableRazorLanguageServer)
        {
            return Task.FromResult<InitializeResult>(
                new()
                {
                    Capabilities = new()
                    {
                        TextDocumentSync = new TextDocumentSyncOptions { OpenClose = false, Change = TextDocumentSyncKind.None, Save = false, WillSave = false, WillSaveWaitUntil = false }
                    }
                });
        }

        var capabilitiesManager = requestContext.GetRequiredService<IInitializeManager<InitializeParams, InitializeResult>>();

        capabilitiesManager.SetInitializeParams(request);
        var serverCapabilities = capabilitiesManager.GetInitializeResult();

        return Task.FromResult(serverCapabilities);
    }
}
