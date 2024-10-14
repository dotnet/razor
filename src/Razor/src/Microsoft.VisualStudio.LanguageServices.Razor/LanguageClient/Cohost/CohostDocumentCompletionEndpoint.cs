// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.Completion.Delegation;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.Completion;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Razor.Settings;
using Response = Microsoft.CodeAnalysis.Razor.Remote.RemoteResponse<Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalCompletionList?>;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(Methods.TextDocumentCompletionName)]
[Export(typeof(IDynamicRegistrationProvider))]
[ExportCohostStatelessLspService(typeof(CohostDocumentCompletionEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal class CohostDocumentCompletionEndpoint(
    IRemoteServiceInvoker remoteServiceInvoker,
    IClientSettingsManager clientSettingsManager,
    IHtmlDocumentSynchronizer htmlDocumentSynchronizer,
    LSPRequestInvoker requestInvoker,
    ILoggerFactory loggerFactory)
    : AbstractRazorCohostDocumentRequestHandler<CompletionParams, VSInternalCompletionList?>, IDynamicRegistrationProvider
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;
    private readonly IClientSettingsManager _clientSettingsManager = clientSettingsManager;
    private readonly IHtmlDocumentSynchronizer _htmlDocumentSynchronizer = htmlDocumentSynchronizer;
    private readonly LSPRequestInvoker _requestInvoker = requestInvoker;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<CohostDocumentCompletionEndpoint>();

    private VSInternalClientCapabilities? _clientCapabilities;

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    public ImmutableArray<Registration> GetRegistrations(VSInternalClientCapabilities clientCapabilities, DocumentFilter[] filter, RazorCohostRequestContext requestContext)
    {
        _clientCapabilities = clientCapabilities;

        if (clientCapabilities.TextDocument?.Completion?.DynamicRegistration is true)
        {
            return [new Registration()
            {
                Method = Methods.TextDocumentCompletionName,
                RegisterOptions = new CompletionRegistrationOptions()
                {
                    ResolveProvider = true,
                    TriggerCharacters = [.. CompletionTriggerCharacters.AllTriggerCharacters],
                    DocumentSelector = filter,
                    AllCommitCharacters = [" ", ">", ";", "="]
                }
            }];
        }

        return [];
    }

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(CompletionParams request)
        => request.TextDocument?.ToRazorTextDocumentIdentifier();

    protected override Task<VSInternalCompletionList?> HandleRequestAsync(CompletionParams request, RazorCohostRequestContext context, CancellationToken cancellationToken)
        => HandleRequestAsync(request, context.TextDocument.AssumeNotNull(), cancellationToken);

    private async Task<VSInternalCompletionList?> HandleRequestAsync(CompletionParams request, TextDocument razorDocument, CancellationToken cancellationToken)
    {
        if (request.Context is null)
        {
            _logger.LogError("Completion request context is null");
            return null;
        }

        // Return immediately if this is auto-shown completion but auto-shown completion is disallowed in settings
        var clientSettings =  _clientSettingsManager.GetClientSettings();
        var autoShownCompletion = request.Context.TriggerKind != CompletionTriggerKind.Invoked;
        if (autoShownCompletion && !clientSettings.ClientCompletionSettings.AutoShowCompletion)
        {
            return null;
        }

        _logger.LogDebug($"Invoking completion for {razorDocument.FilePath}");

        var completionContext = ToVSInternalCompletionContext(request.Context);
        if (await _remoteServiceInvoker.TryInvokeAsync<IRemoteCompletionService, CompletionPositionInfo?>(
                razorDocument.Project.Solution,
                (service, solutionInfo, cancellationToken)
                    => service.GetPositionInfoAsync(
                            solutionInfo,
                            razorDocument.Id,
                            completionContext,
                            request.Position,
                            cancellationToken),
                cancellationToken).ConfigureAwait(false) is not { } completionPositionInfo)
        {
            // If we can't figure out position info for request position we can't return completions
            return null;
        }

        var documentPositionInfo = completionPositionInfo.DocumentPositionInfo;
        if (documentPositionInfo.LanguageKind != RazorLanguageKind.Razor)
        {
            completionContext = DelegatedCompletionHelper.RewriteContext(completionContext, documentPositionInfo.LanguageKind);
        }

        // First of all, see if we in HTML and get HTML completions before calling OOP to get Razor completions.
        // Razor completion provider needs a set of existing HTML item labels.

        VSInternalCompletionList? htmlCompletionList = null;
        using var _ = HashSetPool<string>.GetPooledObject(out var existingHtmlCompletions);
        if (CompletionTriggerCharacters.IsValidTrigger(CompletionTriggerCharacters.HtmlTriggerCharacters, completionContext))
        {
            // We can just blindly call HTML LSP because if we are in C#, generated HTML seen by HTML LSP may return
            // results we don't want to show. So we want to call HTML LSP only if we know we are in HTML content.
            if (documentPositionInfo.LanguageKind == RazorLanguageKind.Html)
            {
                htmlCompletionList = await GetHtmlCompletionListAsync(request, razorDocument, cancellationToken);
                if (htmlCompletionList is not null)
                {
                    existingHtmlCompletions.UnionWith(htmlCompletionList.Items.Select(i => i.Label));
                }
            }
        }

        var razorCompletionOptions = new RazorCompletionOptions(
            SnippetsSupported: false,
            AutoInsertAttributeQuotes: true, // TODO: Get real values
            CommitElementsWithSpace: true);

        _logger.LogDebug($"Calling OOP to get completion items at {request.Position} invoked by typing '{request.Context?.TriggerCharacter}'");

        var data = await _remoteServiceInvoker.TryInvokeAsync<IRemoteCompletionService, Response>(
            razorDocument.Project.Solution,
            (service, solutionInfo, cancellationToken)
                => service.GetCompletionAsync(
                        solutionInfo,
                        razorDocument.Id,
                        completionPositionInfo,
                        completionContext,
                        _clientCapabilities.AssumeNotNull(),
                        razorCompletionOptions,
                        existingHtmlCompletions,
                        cancellationToken),
            cancellationToken).ConfigureAwait(false);

        if (data.StopHandling)
        {
            return null;
        }

        if (data.Result is { } oopCompletionList)
        {
            return htmlCompletionList?.Items is not null && htmlCompletionList.Items.Length > 0
                // If we have HTML completions, that means OOP completion list is really just Razor completion list
                ? CompletionListMerger.Merge(oopCompletionList, htmlCompletionList)
                : oopCompletionList;
        }

        // Didn't get anything from OOP, so just return HTML completion list or null
        return htmlCompletionList;
    }

    private async Task<VSInternalCompletionList?> GetHtmlCompletionListAsync(CompletionParams request, TextDocument razorDocument, CancellationToken cancellationToken)
    {
        var htmlDocument = await _htmlDocumentSynchronizer.TryGetSynchronizedHtmlDocumentAsync(razorDocument, cancellationToken).ConfigureAwait(false);
        if (htmlDocument is null)
        {
            return null;
        }

        request.TextDocument = request.TextDocument.WithUri(htmlDocument.Uri);

        _logger.LogDebug($"Resolving auto-insertion edit for {htmlDocument.Uri}");

        var result = await _requestInvoker.ReinvokeRequestOnServerAsync<CompletionParams, VSInternalCompletionList?>(
            htmlDocument.Buffer,
            Methods.TextDocumentCompletionName,
            RazorLSPConstants.HtmlLanguageServerName,
            request,
            cancellationToken).ConfigureAwait(false);

        if (result?.Response is not { } htmlCompletionList)
        {
            _logger.LogDebug($"Didn't get completion list from Html.");
            return null;
        }

        return htmlCompletionList;
    }

    // TODO: This is likely not quite correct, need to investigate why this request is not getting VSInternalCompletionContext
    private static VSInternalCompletionContext ToVSInternalCompletionContext(CompletionContext context)
    {
        return new VSInternalCompletionContext
        {
            InvokeKind = context.TriggerCharacter != null
                ? VSInternalCompletionInvokeKind.Typing
                : VSInternalCompletionInvokeKind.Explicit,
            TriggerCharacter = context.TriggerCharacter,
            TriggerKind = context.TriggerKind
        };
    }
}
