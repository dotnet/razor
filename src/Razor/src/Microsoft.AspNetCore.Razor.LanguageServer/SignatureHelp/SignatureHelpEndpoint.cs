// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using LS = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.SignatureHelp;

[LanguageServerEndpoint(Methods.TextDocumentSignatureHelpName)]
internal sealed class SignatureHelpEndpoint : AbstractRazorDelegatingEndpoint<SignatureHelpParams, LS.SignatureHelp?>, ICapabilitiesProvider
{
    private readonly IOptionsMonitor<RazorLSPOptions> _optionsMonitor;

    public SignatureHelpEndpoint(
        LanguageServerFeatureOptions languageServerFeatureOptions,
        IRazorDocumentMappingService documentMappingService,
        IClientConnection clientConnection,
        IOptionsMonitor<RazorLSPOptions> optionsMonitor,
        ILoggerFactory loggerProvider)
        : base(languageServerFeatureOptions, documentMappingService, clientConnection, loggerProvider.CreateLogger<SignatureHelpEndpoint>())
    {
        _optionsMonitor = optionsMonitor;
    }

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
        if (request.Context is not null && request.Context.TriggerKind != SignatureHelpTriggerKind.Invoked && !_optionsMonitor.CurrentValue.AutoListParams)
        {
            // Return nothing is "Parameter Information" option is disabled unless signature help is invoked explicitly via command as opposed to typing or content change
            return Task.FromResult((IDelegatedParams?)null);
        }

        var documentContext = requestContext.GetRequiredDocumentContext();
        return Task.FromResult<IDelegatedParams?>(new DelegatedPositionParams(
                documentContext.Identifier,
                positionInfo.Position,
                positionInfo.LanguageKind));
    }
}
