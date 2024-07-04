// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Razor.Workspaces.Protocol.SignatureHelp;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Razor.LanguageClient;
using Microsoft.VisualStudio.Razor.LanguageClient.Cohost;
using Microsoft.VisualStudio.Razor.LanguageClient.Extensions;

namespace Microsoft.VisualStudio.LanguageServices.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(Methods.TextDocumentSignatureHelpName)]
[Export(typeof(IDynamicRegistrationProvider))]
[ExportCohostStatelessLspService(typeof(CohostSignatureHelpEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal class CohostSignatureHelpEndpoint(
    IRemoteServiceProvider remoteServiceProvider,
    IHtmlDocumentSynchronizer htmlDocumentSynchronizer,
    LSPRequestInvoker requestInvoker,
    ILoggerFactory loggerFactory)
    : AbstractRazorCohostDocumentRequestHandler<SignatureHelpParams, SignatureHelp?>, IDynamicRegistrationProvider
{
    private readonly IRemoteServiceProvider _remoteServiceProvider = remoteServiceProvider;
    private readonly IHtmlDocumentSynchronizer _htmlDocumentSynchronizer = htmlDocumentSynchronizer;
    private readonly LSPRequestInvoker _requestInvoker = requestInvoker;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<CohostFoldingRangeEndpoint>();

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    public Registration? GetRegistration(VSInternalClientCapabilities clientCapabilities, DocumentFilter[] filter, RazorCohostRequestContext requestContext)
    {
        if (clientCapabilities.TextDocument?.SignatureHelp?.DynamicRegistration == true)
        {
            return new Registration()
            {
                Method = Methods.TextDocumentSignatureHelpName,
                RegisterOptions = new SignatureHelpRegistrationOptions()
                {
                    DocumentSelector = filter
                }.EnableSignatureHelp()
            };
        }

        return null;
    }

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(SignatureHelpParams request)
        => request.TextDocument.ToRazorTextDocumentIdentifier();

    protected async override Task<SignatureHelp?> HandleRequestAsync(SignatureHelpParams request, RazorCohostRequestContext context, CancellationToken cancellationToken)
    {
        var razorDocument = context.TextDocument.AssumeNotNull();

        var data = await _remoteServiceProvider.TryInvokeAsync<IRemoteSignatureHelpService, RemoteSignatureHelp?>(
            razorDocument.Project.Solution,
            (service, solutionInfo, cancellationToken) => service.GetSignatureHelpAsync(solutionInfo, razorDocument.Id, request.Position.ToLinePosition(), request.Context.TriggerKind, request.Context.TriggerCharacter, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        // If we got a response back, then either Razor or C# wants to do something with this, so we're good to go
        if (data is { } signatureHelp)
        {
            return signatureHelp.ToSignatureHelp();
        }

        // If we didn't get anything from Razor or Roslyn, lets ask Html what they want to do
        var htmlDocument = await _htmlDocumentSynchronizer.TryGetSynchronizedHtmlDocumentAsync(razorDocument, cancellationToken).ConfigureAwait(false);
        if (htmlDocument is null)
        {
            return null;
        }

        request.TextDocument = request.TextDocument.WithUri(htmlDocument.Uri);

        var result = await _requestInvoker.ReinvokeRequestOnServerAsync<SignatureHelpParams, SignatureHelp?>(
            htmlDocument.Buffer,
            Methods.TextDocumentSignatureHelpName,
            RazorLSPConstants.HtmlLanguageServerName,
            request,
            cancellationToken).ConfigureAwait(false);

        return result?.Response;
    }
}
