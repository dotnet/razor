﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Editor.Razor.Settings;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Cohost;

[Shared]
[RazorLanguageServerEndpoint(Methods.TextDocumentSemanticTokensRangeName)]
[ExportRazorStatelessLspService(typeof(CohostSemanticTokensRangeEndpoint))]
[Export(typeof(ICapabilitiesProvider))]
[method: ImportingConstructor]
internal sealed class CohostSemanticTokensRangeEndpoint(
    IRazorSemanticTokensInfoService semanticTokensInfoService,
    RazorSemanticTokensLegendService razorSemanticTokensLegendService,
    IClientSettingsManager clientSettingsManager,
    IRazorLoggerFactory loggerFactory)
    : AbstractRazorCohostDocumentRequestHandler<SemanticTokensRangeParams, SemanticTokens?>, ICapabilitiesProvider
{
    private readonly IRazorSemanticTokensInfoService _semanticTokensInfoService = semanticTokensInfoService;
    private readonly RazorSemanticTokensLegendService _razorSemanticTokensLegendService = razorSemanticTokensLegendService;
    private readonly IClientSettingsManager _clientSettingsManager = clientSettingsManager;
    private readonly ILogger _logger = loggerFactory.CreateLogger<CohostSemanticTokensRangeEndpoint>();

    protected override bool MutatesSolutionState => false;
    protected override bool RequiresLSPSolution => true;

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(SemanticTokensRangeParams request)
        => request.TextDocument.ToRazorTextDocumentIdentifier();

    public void ApplyCapabilities(VSInternalServerCapabilities serverCapabilities, VSInternalClientCapabilities clientCapabilities)
    {
        serverCapabilities.EnableSemanticTokens(_razorSemanticTokensLegendService.Legend);
    }

    protected override Task<SemanticTokens?> HandleRequestAsync(SemanticTokensRangeParams request, RazorCohostRequestContext context, CancellationToken cancellationToken)
    {
        var documentContext = context.GetRequiredDocumentContext();

        _logger.LogDebug("[Cohost] Received semantic range request for {requestPath} and got document {documentPath}", request.TextDocument.Uri, documentContext.FilePath);
        var clientConnection = context.GetClientConnection();

        // TODO: This is currently using the "VS" client settings manager, since that's where we are running. In future
        //       we should create a hook into Roslyn's LSP options infra so we get the option values from the LSP client
        var colorBackground = _clientSettingsManager.GetClientSettings().AdvancedSettings.ColorBackground;

        return _semanticTokensInfoService.GetSemanticTokensAsync(clientConnection, request.TextDocument, request.Range, documentContext, colorBackground, cancellationToken);
    }
}
