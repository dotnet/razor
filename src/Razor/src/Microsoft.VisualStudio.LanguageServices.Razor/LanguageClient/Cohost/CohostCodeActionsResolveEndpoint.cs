﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost.Handlers;
using Microsoft.CodeAnalysis.Razor.CodeActions;
using Microsoft.CodeAnalysis.Razor.CodeActions.Models;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Razor.Settings;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(Methods.CodeActionResolveName)]
[Export(typeof(IDynamicRegistrationProvider))]
[ExportCohostStatelessLspService(typeof(CohostCodeActionsResolveEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostCodeActionsResolveEndpoint(
    IRemoteServiceInvoker remoteServiceInvoker,
    IClientCapabilitiesService clientCapabilitiesService,
    IClientSettingsManager clientSettingsManager,
    IHtmlDocumentSynchronizer htmlDocumentSynchronizer,
    LSPRequestInvoker requestInvoker)
    : AbstractRazorCohostDocumentRequestHandler<CodeAction, CodeAction?>, IDynamicRegistrationProvider
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;
    private readonly IClientCapabilitiesService _clientCapabilitiesService = clientCapabilitiesService;
    private readonly IClientSettingsManager _clientSettingsManager = clientSettingsManager;
    private readonly IHtmlDocumentSynchronizer _htmlDocumentSynchronizer = htmlDocumentSynchronizer;
    private readonly LSPRequestInvoker _requestInvoker = requestInvoker;

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    public ImmutableArray<Registration> GetRegistrations(VSInternalClientCapabilities clientCapabilities, RazorCohostRequestContext requestContext)
    {
        if (clientCapabilities.TextDocument?.CodeAction?.DynamicRegistration == true)
        {
            return [new Registration
            {
                Method = Methods.CodeActionResolveName,
                RegisterOptions = new CodeActionRegistrationOptions().EnableCodeActions()
            }];
        }

        return [];
    }

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(CodeAction request)
    {
        var resolveParams = CodeActionResolveService.GetRazorCodeActionResolutionParams(request);
        return resolveParams.TextDocument.ToRazorTextDocumentIdentifier();
    }

    protected override Task<CodeAction?> HandleRequestAsync(CodeAction request, RazorCohostRequestContext context, CancellationToken cancellationToken)
        => HandleRequestAsync(context.TextDocument.AssumeNotNull(), request, cancellationToken);

    private async Task<CodeAction?> HandleRequestAsync(TextDocument razorDocument, CodeAction request, CancellationToken cancellationToken)
    {
        var resolveParams = CodeActionResolveService.GetRazorCodeActionResolutionParams(request);

        var resolvedDelegatedCodeAction = resolveParams.Language switch
        {
            RazorLanguageKind.Html => await ResolvedHtmlCodeActionAsync(razorDocument, request, resolveParams, cancellationToken).ConfigureAwait(false),
            RazorLanguageKind.CSharp => await ResolveCSharpCodeActionAsync(razorDocument, request, resolveParams, cancellationToken).ConfigureAwait(false),
            _ => null
        };

        var clientSettings = _clientSettingsManager.GetClientSettings();
        var formattingOptions = new RazorFormattingOptions()
        {
            InsertSpaces = !clientSettings.ClientSpaceSettings.IndentWithTabs,
            TabSize = clientSettings.ClientSpaceSettings.IndentSize,
            CodeBlockBraceOnNextLine = clientSettings.AdvancedSettings.CodeBlockBraceOnNextLine
        };

        return await _remoteServiceInvoker.TryInvokeAsync<IRemoteCodeActionsService, CodeAction>(
            razorDocument.Project.Solution,
            (service, solutionInfo, cancellationToken) => service.ResolveCodeActionAsync(solutionInfo, razorDocument.Id, request, resolvedDelegatedCodeAction, formattingOptions, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<CodeAction> ResolveCSharpCodeActionAsync(TextDocument razorDocument, CodeAction codeAction, RazorCodeActionResolutionParams resolveParams, CancellationToken cancellationToken)
    {
        var originalData = codeAction.Data;
        try
        {
            codeAction.Data = resolveParams.Data;

            var uri = resolveParams.DelegatedDocumentUri.AssumeNotNull();

            if (!razorDocument.Project.TryGetCSharpDocument(uri, out var generatedDocument))
            {
                return codeAction;
            }

            var resourceOptions = _clientCapabilitiesService.ClientCapabilities.Workspace?.WorkspaceEdit?.ResourceOperations ?? [];
            var roslynCodeAction = JsonHelpers.ToRoslynLSP<Roslyn.LanguageServer.Protocol.VSInternalCodeAction, CodeAction>(codeAction).AssumeNotNull();
            var roslynResourceOptions = JsonHelpers.ToRoslynLSP<Roslyn.LanguageServer.Protocol.ResourceOperationKind[], ResourceOperationKind[]>(resourceOptions).AssumeNotNull();

            var resolvedCodeAction = await CodeActions.ResolveCodeActionAsync(generatedDocument, roslynCodeAction, roslynResourceOptions, cancellationToken).ConfigureAwait(false);

            return JsonHelpers.ToVsLSP<RazorVSInternalCodeAction, Roslyn.LanguageServer.Protocol.CodeAction>(resolvedCodeAction).AssumeNotNull();
        }
        finally
        {
            codeAction.Data = originalData;
        }
    }

    private async Task<CodeAction> ResolvedHtmlCodeActionAsync(TextDocument razorDocument, CodeAction codeAction, RazorCodeActionResolutionParams resolveParams, CancellationToken cancellationToken)
    {
        var originalData = codeAction.Data;
        codeAction.Data = resolveParams.Data;

        try
        {
            var htmlDocument = await _htmlDocumentSynchronizer.TryGetSynchronizedHtmlDocumentAsync(razorDocument, cancellationToken).ConfigureAwait(false);
            if (htmlDocument is null)
            {
                return codeAction;
            }

            var result = await _requestInvoker.ReinvokeRequestOnServerAsync<CodeAction, CodeAction>(
                htmlDocument.Buffer,
                Methods.CodeActionResolveName,
                RazorLSPConstants.HtmlLanguageServerName,
                codeAction,
                cancellationToken).ConfigureAwait(false);

            if (result?.Response is null)
            {
                return codeAction;
            }

            return result.Response;
        }
        finally
        {
            codeAction.Data = originalData;
        }
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CohostCodeActionsResolveEndpoint instance)
    {
        public Task<CodeAction?> HandleRequestAsync(TextDocument razorDocument, CodeAction request, CancellationToken cancellationToken)
            => instance.HandleRequestAsync(razorDocument, request, cancellationToken);
    }
}
