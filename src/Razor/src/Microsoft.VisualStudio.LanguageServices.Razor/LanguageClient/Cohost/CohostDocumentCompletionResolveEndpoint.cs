// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.Completion.Delegation;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Razor.Settings;
using RoslynTextDocumentIdentifier = Roslyn.LanguageServer.Protocol.TextDocumentIdentifier;
using RoslynVSInternalCompletionItem = Roslyn.LanguageServer.Protocol.VSInternalCompletionItem;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(Methods.TextDocumentCompletionResolveName)]
[Export(typeof(IDynamicRegistrationProvider))]
[ExportCohostStatelessLspService(typeof(CohostDocumentCompletionResolveEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostDocumentCompletionResolveEndpoint(
    CompletionListCache completionListCache,
    IRemoteServiceInvoker remoteServiceInvoker,
    IClientSettingsManager clientSettingsManager,
    IHtmlDocumentSynchronizer htmlDocumentSynchronizer,
    LSPRequestInvoker requestInvoker,
    ILoggerFactory loggerFactory)
    : AbstractRazorCohostDocumentRequestHandler<RoslynVSInternalCompletionItem, RoslynVSInternalCompletionItem?>, IDynamicRegistrationProvider
{
    private readonly CompletionListCache _completionListCache = completionListCache;
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;
    private readonly IClientSettingsManager _clientSettingsManager = clientSettingsManager;
    private readonly IHtmlDocumentSynchronizer _htmlDocumentSynchronizer = htmlDocumentSynchronizer;
    private readonly LSPRequestInvoker _requestInvoker = requestInvoker;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<CohostDocumentCompletionEndpoint>();

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    public ImmutableArray<Registration> GetRegistrations(VSInternalClientCapabilities clientCapabilities, RazorCohostRequestContext requestContext)
    {
        if (clientCapabilities.TextDocument?.Completion?.DynamicRegistration is true)
        {
            return [new Registration()
            {
                Method = Methods.TextDocumentCompletionResolveName,
                RegisterOptions = new CompletionRegistrationOptions()
                {
                    ResolveProvider = true
                }
            }];
        }

        return [];
    }

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(RoslynVSInternalCompletionItem request)
    {
        if (JsonHelpers.ToVsLSP<VSInternalCompletionItem, RoslynVSInternalCompletionItem>(request) is { } completionItem &&
            RazorCompletionResolveData.Unwrap(completionItem) is { } data)
        {
            var roslynTextDocument = JsonHelpers.ToRoslynLSP<RoslynTextDocumentIdentifier, TextDocumentIdentifier>(data.TextDocument).AssumeNotNull();
            return Roslyn.LanguageServer.Protocol.RoslynLspExtensions.ToRazorTextDocumentIdentifier(roslynTextDocument);
        }

        return null;
    }

    protected override Task<RoslynVSInternalCompletionItem?> HandleRequestAsync(RoslynVSInternalCompletionItem request, RazorCohostRequestContext context, CancellationToken cancellationToken)
        => HandleRequestAsync(request, context.TextDocument.AssumeNotNull(), cancellationToken);

    private async Task<RoslynVSInternalCompletionItem?> HandleRequestAsync(
        RoslynVSInternalCompletionItem request,
        TextDocument razorDocument,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return request;
        }

        if (JsonHelpers.ToVsLSP<VSInternalCompletionItem, RoslynVSInternalCompletionItem>(request) is not { } completionItem)
        {
            return request;
        }

        var data = RazorCompletionResolveData.Unwrap(completionItem);
        completionItem.Data = data.OriginalData;

        if (_completionListCache.TryGetOriginalRequestData(completionItem, out var completionList, out var context) &&
            context is DelegatedCompletionResolutionContext delegatedContext)
        {
            Debug.Assert(delegatedContext.ProjectedKind == RazorLanguageKind.Html);

            completionItem.Data = DelegatedCompletionHelper.GetOriginalCompletionItemData(completionItem, completionList, delegatedContext.OriginalCompletionListData);
            var htmlResolveResult = await ResolveHtmlCompletionItemAsync(completionItem, razorDocument, cancellationToken).ConfigureAwait(false);

            return JsonHelpers.ToRoslynLSP<RoslynVSInternalCompletionItem, VSInternalCompletionItem>(htmlResolveResult).AssumeNotNull();
        }

        var roslynItem = JsonHelpers.ToRoslynLSP<RoslynVSInternalCompletionItem, VSInternalCompletionItem>(completionItem).AssumeNotNull();

        var clientSettings = _clientSettingsManager.GetClientSettings();
        var formattingOptions = new RazorFormattingOptions()
        {
            InsertSpaces = !clientSettings.ClientSpaceSettings.IndentWithTabs,
            TabSize = clientSettings.ClientSpaceSettings.IndentSize,
            CodeBlockBraceOnNextLine = clientSettings.AdvancedSettings.CodeBlockBraceOnNextLine
        };

        // Couldn't find an associated completion list, so its either Razor or C#. Either way, over to OOP
        var result = await _remoteServiceInvoker.TryInvokeAsync<IRemoteCompletionService, RoslynVSInternalCompletionItem>(
            razorDocument.Project.Solution,
            (service, solutionInfo, cancellationToken)
                => service.ResolveCompletionItemAsync(
                    solutionInfo,
                    razorDocument.Id,
                    roslynItem,
                    formattingOptions,
                    cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async Task<VSInternalCompletionItem> ResolveHtmlCompletionItemAsync(
        VSInternalCompletionItem request,
        TextDocument razorDocument,
        CancellationToken cancellationToken)
    {
        var htmlDocument = await _htmlDocumentSynchronizer.TryGetSynchronizedHtmlDocumentAsync(razorDocument, cancellationToken).ConfigureAwait(false);
        if (htmlDocument is null)
        {
            return request;
        }

        _logger.LogDebug($"Resolving completion item {request.Label} for {htmlDocument.Uri}");

        var result = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalCompletionItem, VSInternalCompletionItem>(
            htmlDocument.Buffer,
            Methods.TextDocumentCompletionResolveName,
            RazorLSPConstants.HtmlLanguageServerName,
            request,
            cancellationToken).ConfigureAwait(false);

        return result?.Response ?? request;
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CohostDocumentCompletionResolveEndpoint instance)
    {
        public RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(RoslynVSInternalCompletionItem request)
            => instance.GetRazorTextDocumentIdentifier(request);

        public Task<RoslynVSInternalCompletionItem?> HandleRequestAsync(
            RoslynVSInternalCompletionItem request,
            TextDocument razorDocument,
            CancellationToken cancellationToken)
                => instance.HandleRequestAsync(request, razorDocument, cancellationToken);
    }
}
