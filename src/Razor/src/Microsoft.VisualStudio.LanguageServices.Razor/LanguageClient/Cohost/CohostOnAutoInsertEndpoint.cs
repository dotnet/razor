// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.AutoInsert;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol.AutoInsert;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Razor.LanguageClient.Cohost;
using Microsoft.VisualStudio.Razor.LanguageClient.Extensions;

using RazorLSPConstants = Microsoft.VisualStudio.Razor.LanguageClient.RazorLSPConstants;

namespace Microsoft.VisualStudio.LanguageServices.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(VSInternalMethods.OnAutoInsertName)]
[Export(typeof(IDynamicRegistrationProvider))]
[ExportCohostStatelessLspService(typeof(CohostOnAutoInsertEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal class CohostOnAutoInsertEndpoint(
    IRemoteServiceInvoker remoteServiceInvoker,
    IHtmlDocumentSynchronizer htmlDocumentSynchronizer,
    LSPRequestInvoker requestInvoker,
    IRazorDocumentMappingService razorDocumentMappingService,
    ILoggerFactory loggerFactory)
    : AbstractRazorCohostDocumentRequestHandler<VSInternalDocumentOnAutoInsertParams, VSInternalDocumentOnAutoInsertResponseItem?>, IDynamicRegistrationProvider
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;
    private readonly IHtmlDocumentSynchronizer _htmlDocumentSynchronizer = htmlDocumentSynchronizer;
    private readonly LSPRequestInvoker _requestInvoker = requestInvoker;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<CohostOnAutoInsertEndpoint>();
    private readonly IRazorDocumentMappingService _razorDocumentMappingService = razorDocumentMappingService;

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    public Registration? GetRegistration(VSInternalClientCapabilities clientCapabilities, DocumentFilter[] filter, RazorCohostRequestContext requestContext)
    {
        if (clientCapabilities.SupportsVisualStudioExtensions)
        {
            // TODO: Add overload that doesn't use solution
            //_remoteServiceProvider.TryInvokeAsync()
            var providerTriggerChars = new string[] { ">" };

            var triggerCharacters = providerTriggerChars
                .Concat(AutoInsertService.HtmlAllowedAutoInsertTriggerCharacters)
                .Concat(AutoInsertService.CSharpAllowedAutoInsertTriggerCharacters);

            return new Registration
            {
                Method = VSInternalMethods.OnAutoInsertName,
                RegisterOptions = new VSInternalDocumentOnAutoInsertOptions()
                    .EnableOnAutoInsert(triggerCharacters)
            };
        }

        return null;
    }

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(VSInternalDocumentOnAutoInsertParams request)
        => request.TextDocument.ToRazorTextDocumentIdentifier();

    protected override async Task<VSInternalDocumentOnAutoInsertResponseItem?> HandleRequestAsync(VSInternalDocumentOnAutoInsertParams request, RazorCohostRequestContext context, CancellationToken cancellationToken)
    {
        var razorDocument = context.TextDocument.AssumeNotNull();

        _logger.LogDebug($"Resolving auto-insertion for {razorDocument.FilePath}");

        _logger.LogDebug($"Calling OOP to resolve insertion at {request.Position} invoked by typing '{request.Character}'");
        var data = await _remoteServiceInvoker.TryInvokeAsync<IRemoteAutoInsertService, RemoteInsertTextEdit?>(
            razorDocument.Project.Solution,
            (service, solutionInfo, cancellationToken)
                => service.TryResolveInsertionAsync(
                        solutionInfo,
                        razorDocument.Id,
                        request.Position,
                        request.Character,
                        autoCloseTags: true, // TODO: get value from client options
                        cancellationToken),
            cancellationToken).ConfigureAwait(false);

        if (data is { } remoteInsertTextEdit)
        {
            _logger.LogDebug($"Got insert text edit from OOP {remoteInsertTextEdit}");
            return RemoteInsertTextEdit.ToLspInsertTextEdit(remoteInsertTextEdit);
        }

        // If we are here, Razor didn't return anything, so try HTML

        return await TryResolveHtmlInsertionAsync(razorDocument, request, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<VSInternalDocumentOnAutoInsertResponseItem?> TryResolveHtmlInsertionAsync(
        TextDocument razorDocument,
        VSInternalDocumentOnAutoInsertParams request,
        CancellationToken cancellationToken)
    {
        // We support auto-insert in HTML only on "="
        if (request.Character != "=")
        {
            return null;
        }

        var htmlDocument = await _htmlDocumentSynchronizer.TryGetSynchronizedHtmlDocumentAsync(razorDocument, cancellationToken).ConfigureAwait(false);
        if (htmlDocument is null)
        {
            return null;
        }

        var autoInsertParams = new VSInternalDocumentOnAutoInsertParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = htmlDocument.Uri },
            Character = request.Character,
            Position = request.Position
        };

        _logger.LogDebug($"Resolving auto-insertion edit for {htmlDocument.Uri}");

        var result = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalDocumentOnAutoInsertParams, VSInternalDocumentOnAutoInsertResponseItem?>(
            htmlDocument.Buffer,
            VSInternalMethods.OnAutoInsertName,
            RazorLSPConstants.HtmlLanguageServerName,
            autoInsertParams,
            cancellationToken).ConfigureAwait(false);

        if (result?.Response is null)
        {
            _logger.LogDebug($"Didn't get insert edit back from Html.");
            return null;
        }

        return result.Response;
    }
}
