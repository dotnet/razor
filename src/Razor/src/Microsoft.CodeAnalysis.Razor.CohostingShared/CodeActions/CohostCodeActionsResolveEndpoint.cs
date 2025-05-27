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
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Features;
using Microsoft.CodeAnalysis.Razor.CodeActions;
using Microsoft.CodeAnalysis.Razor.CodeActions.Models;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces.Settings;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
#if !VSCODE
// Visual Studio requires us to register for every method name, VS Code correctly realises that if you
// register for code actions, and say you have resolve support, then registering for resolve is unnecessary.
// In fact it's an error.
[Export(typeof(IDynamicRegistrationProvider))]
#endif
[Shared]
[CohostEndpoint(Methods.CodeActionResolveName)]
[ExportRazorStatelessLspService(typeof(CohostCodeActionsResolveEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostCodeActionsResolveEndpoint(
    IRemoteServiceInvoker remoteServiceInvoker,
    IClientCapabilitiesService clientCapabilitiesService,
    IClientSettingsManager clientSettingsManager,
    IHtmlRequestInvoker requestInvoker)
    : AbstractRazorCohostDocumentRequestHandler<CodeAction, CodeAction?>, IDynamicRegistrationProvider
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;
    private readonly IClientCapabilitiesService _clientCapabilitiesService = clientCapabilitiesService;
    private readonly IClientSettingsManager _clientSettingsManager = clientSettingsManager;
    private readonly IHtmlRequestInvoker _requestInvoker = requestInvoker;

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

            var generatedDocument = await razorDocument.Project.TryGetCSharpDocumentFromGeneratedDocumentUriAsync(uri, cancellationToken).ConfigureAwait(false);
            if (generatedDocument is null)
            {
                return codeAction;
            }

            var resourceOptions = _clientCapabilitiesService.ClientCapabilities.Workspace?.WorkspaceEdit?.ResourceOperations ?? [];

            return await CodeActions.ResolveCodeActionAsync(generatedDocument, codeAction, resourceOptions, cancellationToken).ConfigureAwait(false);
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
            var result = await _requestInvoker.MakeHtmlLspRequestAsync<CodeAction, CodeAction>(
                razorDocument,
                Methods.CodeActionResolveName,
                codeAction,
                cancellationToken).ConfigureAwait(false);

            if (result is null)
            {
                return codeAction;
            }

            return result;
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
