﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using LS = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.SignatureHelp;

[RazorLanguageServerEndpoint(Methods.TextDocumentSignatureHelpName)]
internal sealed class SignatureHelpEndpoint(
        LanguageServerFeatureOptions languageServerFeatureOptions,
        IRazorDocumentMappingService documentMappingService,
        IClientConnection clientConnection,
        RazorLSPOptionsMonitor optionsMonitor,
        ILoggerFactory loggerProvider)
    : AbstractRazorDelegatingEndpoint<SignatureHelpParams, LS.SignatureHelp?>(
        languageServerFeatureOptions,
        documentMappingService,
        clientConnection,
        loggerProvider.GetOrCreateLogger<SignatureHelpEndpoint>()),
    ICapabilitiesProvider
{
    protected override string CustomMessageTarget => CustomMessageNames.RazorSignatureHelpEndpointName;

    public void ApplyCapabilities(VSInternalServerCapabilities serverCapabilities, VSInternalClientCapabilities clientCapabilities)
    {
        serverCapabilities.SignatureHelpProvider = new SignatureHelpOptions()
        {
            TriggerCharacters = new[] { "(", ",", "<" },
            RetriggerCharacters = new[] { ">", ")" }
        };
    }

    protected override Task<IDelegatedParams?> CreateDelegatedParamsAsync(SignatureHelpParams request, RazorRequestContext requestContext, DocumentPositionInfo positionInfo, CancellationToken cancellationToken)
    {
        if (request.Context is not null &&
            request.Context.TriggerKind != SignatureHelpTriggerKind.Invoked &&
            !optionsMonitor.CurrentValue.AutoListParams)
        {
            // Return nothing if "Parameter Information" option is disabled unless signature help is invoked explicitly via command as opposed to typing or content change
            return Task.FromResult<IDelegatedParams?>(null);
        }

        var documentContext = requestContext.DocumentContext;
        if (documentContext is null)
        {
            return Task.FromResult<IDelegatedParams?>(null);
        }

        return Task.FromResult<IDelegatedParams?>(new DelegatedPositionParams(
                documentContext.Identifier,
                positionInfo.Position,
                positionInfo.LanguageKind));
    }
}
