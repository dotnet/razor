// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic;

[LanguageServerEndpoint(Methods.TextDocumentSemanticTokensRangeName)]
internal sealed class SemanticTokensRangeEndpoint : IRazorRequestHandler<SemanticTokensRangeParams, SemanticTokens?>, ICapabilitiesProvider
{
    private readonly IRazorSemanticTokensInfoService _semanticTokensInfoService;
    private readonly IClientConnection _clientConnection;

    public SemanticTokensRangeEndpoint(IRazorSemanticTokensInfoService semanticTokensInfoService, IClientConnection clientConnection)
    {
        _semanticTokensInfoService = semanticTokensInfoService;
        _clientConnection = clientConnection;
    }

    public bool MutatesSolutionState { get; } = false;

    public void ApplyCapabilities(VSInternalServerCapabilities serverCapabilities, VSInternalClientCapabilities clientCapabilities)
    {
        _semanticTokensInfoService.ApplyCapabilities(serverCapabilities, clientCapabilities);
    }

    public TextDocumentIdentifier GetTextDocumentIdentifier(SemanticTokensRangeParams request)
    {
        return request.TextDocument;
    }

    public async Task<SemanticTokens?> HandleRequestAsync(SemanticTokensRangeParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        var documentContext = requestContext.GetRequiredDocumentContext();

        var semanticTokens = await _semanticTokensInfoService.GetSemanticTokensAsync(_clientConnection, request.TextDocument, request.Range, documentContext, cancellationToken).ConfigureAwait(false);

        return semanticTokens;
    }
}
