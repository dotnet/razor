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
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;
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

        // First of all, see if we in HTML and get HTML completions before calling OOP to get Razor completions.
        // Razor completion provider needs a set of existing HTML item labels.
        using var _ = HashSetPool<string>.GetPooledObject(out var existingHtmlCompletions);
        if (CompletionTriggerCharacters.IsValidTrigger(CompletionTriggerCharacters.HtmlTriggerCharacters, request.Context))
        {
            var positionInfo = await _remoteServiceInvoker.TryInvokeAsync<IRemoteCompletionService, DocumentPositionInfo?>(
                razorDocument.Project.Solution,
                (service, solutionInfo, cancellationToken)
                    => service.GetPositionInfoAsync(
                            solutionInfo,
                            razorDocument.Id,
                            request.Position,
                            cancellationToken),
                cancellationToken).ConfigureAwait(false);

            if (positionInfo is not { } positionInfoValue)
            {
                // If we can't figure out position info for request position we can't return completions
                return null;
            }

            // We can just blindly call HTML LSP because if we are in C#, generated HTML seen by HTML LSP may return
            // results we don't want to show. So we want to call HTML LSP only if we know we are in HTML content.
            if (positionInfoValue.LanguageKind == RazorLanguageKind.Html
                && await GetHtmlCompletionListAsync(request, razorDocument, cancellationToken) is { } htmlCompletionList)
            {
                existingHtmlCompletions.UnionWith(htmlCompletionList.Items.Select(i => i.Label));
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
                        request.Position,
                        request.Context.AssumeNotNull(),
                        _clientCapabilities.AssumeNotNull(),
                        razorCompletionOptions,
                        existingHtmlCompletions,
                        cancellationToken),
            cancellationToken).ConfigureAwait(false);

        if (data.Result is { } completionList)
        {
            return completionList;
        }

        return null;
    }

    private async Task<VSInternalCompletionList?> GetHtmlCompletionListAsync(CompletionParams request, TextDocument razorDocument, CancellationToken cancellationToken)
    {
        if (request.Context?.TriggerCharacter != "<")
        {
            return null;
        }

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

        if (result?.Response is null)
        {
            _logger.LogDebug($"Didn't get completion list from Html.");
            return null;
        }

        return result.Response;
    }
}
