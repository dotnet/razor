// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Response = Microsoft.CodeAnalysis.Razor.Remote.RemoteResponse<Roslyn.LanguageServer.Protocol.VSInternalCompletionList?>;
using RoslynCompletionList = Roslyn.LanguageServer.Protocol.VSInternalCompletionList;

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
    IHtmlDocumentSynchronizer htmlDocumentSynchronizer,
    LSPRequestInvoker requestInvoker,
    IFilePathService filePathService,
    ILoggerFactory loggerFactory)
    : AbstractRazorCohostDocumentRequestHandler<CompletionParams, VSInternalCompletionList?>, IDynamicRegistrationProvider
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;
    private readonly IHtmlDocumentSynchronizer _htmlDocumentSynchronizer = htmlDocumentSynchronizer;
    private readonly LSPRequestInvoker _requestInvoker = requestInvoker;
    private readonly IFilePathService _filePathService = filePathService;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<CohostDocumentCompletionEndpoint>();

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    public ImmutableArray<Registration> GetRegistrations(VSInternalClientCapabilities clientCapabilities, DocumentFilter[] filter, RazorCohostRequestContext requestContext)
    {
        if (clientCapabilities.TextDocument?.Completion?.DynamicRegistration is true)
        {
            return [new Registration()
            {
                Method = Methods.TextDocumentCompletionName,
                RegisterOptions = new CompletionRegistrationOptions()
                {
                    ResolveProvider = true,
                    TriggerCharacters = [" ", "<"], // TODO: Implement TriggerCharacterProvider
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
        _logger.LogDebug($"Invoking completion for {razorDocument.FilePath}");

        _logger.LogDebug($"Calling OOP to get completion items at {request.Position} invoked by typing '{request.Context?.TriggerCharacter}'");

        var data = await _remoteServiceInvoker.TryInvokeAsync<IRemoteCompletionService, Response>(
            razorDocument.Project.Solution,
            (service, solutionInfo, cancellationToken)
                => service.GetCompletionAsync(
                        solutionInfo,
                        razorDocument.Id,
                        request.Position.ToLinePosition(),
                        request.Context?.TriggerCharacter,
                        cancellationToken),
            cancellationToken).ConfigureAwait(false);

        if (data.Result is { } completionList)
        {
            return ToLspCompletionList(completionList);
        }

        if (data.StopHandling)
        {
            return null;
        }

        return await GetHtmlCompletionListAsync(request, razorDocument, cancellationToken);
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

    private static VSInternalCompletionList? ToLspCompletionList(RoslynCompletionList? roslynCompletionList)
    {
        if (roslynCompletionList is null)
        {
            return null;
        }

        // TODO: proper conversion
        var result = new VSInternalCompletionList()
        {
            IsIncomplete = roslynCompletionList.IsIncomplete
        };

        return result;
    }
}
