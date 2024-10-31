// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.CodeAnalysis.Razor.CodeActions;
using Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;
using Microsoft.CodeAnalysis.Razor.Workspaces.Telemetry;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

[RazorLanguageServerEndpoint(LspEndpointName)]
internal sealed class CodeActionEndpoint(
    ICodeActionsService codeActionsService,
    ITelemetryReporter telemetryReporter)
    : IRazorRequestHandler<VSCodeActionParams, SumType<Command, CodeAction>[]?>, ICapabilitiesProvider
{
    private const string LspEndpointName = Methods.TextDocumentCodeActionName;

    private readonly ICodeActionsService _codeActionsService = codeActionsService;
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;

    internal bool _supportsCodeActionResolve = false;

    public bool MutatesSolutionState { get; } = false;

    public void ApplyCapabilities(VSInternalServerCapabilities serverCapabilities, VSInternalClientCapabilities clientCapabilities)
    {
        _supportsCodeActionResolve = clientCapabilities.TextDocument?.CodeAction?.ResolveSupport is not null;

        serverCapabilities.EnableCodeActions();
    }

    public TextDocumentIdentifier GetTextDocumentIdentifier(VSCodeActionParams request)
    {
        return request.TextDocument;
    }

    public async Task<SumType<Command, CodeAction>[]?> HandleRequestAsync(VSCodeActionParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        var documentContext = requestContext.DocumentContext;
        if (documentContext is null)
        {
            return null;
        }

        var correlationId = Guid.NewGuid();
        using var __ = _telemetryReporter.TrackLspRequest(LspEndpointName, LanguageServerConstants.RazorLanguageServerName, TelemetryThresholds.CodeActionRazorTelemetryThreshold, correlationId);
        cancellationToken.ThrowIfCancellationRequested();

        return await _codeActionsService.GetCodeActionsAsync(request, documentContext, _supportsCodeActionResolve, correlationId, cancellationToken).ConfigureAwait(false);
    }
}
