﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Features;
using Microsoft.CodeAnalysis.Razor.Protocol.DocumentHighlight;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(Methods.TextDocumentDocumentHighlightName)]
[Export(typeof(IDynamicRegistrationProvider))]
[ExportRazorStatelessLspService(typeof(CohostDocumentHighlightEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostDocumentHighlightEndpoint(
    IRemoteServiceInvoker remoteServiceInvoker,
    IHtmlRequestInvoker requestInvoker)
    : AbstractRazorCohostDocumentRequestHandler<DocumentHighlightParams, DocumentHighlight[]?>, IDynamicRegistrationProvider
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;
    private readonly IHtmlRequestInvoker _requestInvoker = requestInvoker;

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    public ImmutableArray<Registration> GetRegistrations(VSInternalClientCapabilities clientCapabilities, RazorCohostRequestContext requestContext)
    {
        if (clientCapabilities.TextDocument?.DocumentHighlight?.DynamicRegistration == true)
        {
            return [new Registration
            {
                Method = Methods.TextDocumentDocumentHighlightName,
                RegisterOptions = new DocumentHighlightRegistrationOptions()
            }];
        }

        return [];
    }

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(DocumentHighlightParams request)
        => request.TextDocument.ToRazorTextDocumentIdentifier();

    protected override Task<DocumentHighlight[]?> HandleRequestAsync(DocumentHighlightParams request, RazorCohostRequestContext context, CancellationToken cancellationToken)
        => HandleRequestAsync(request, context.TextDocument.AssumeNotNull(), cancellationToken);

    private async Task<DocumentHighlight[]?> HandleRequestAsync(DocumentHighlightParams request, TextDocument razorDocument, CancellationToken cancellationToken)
    {
        var csharpResult = await _remoteServiceInvoker.TryInvokeAsync<IRemoteDocumentHighlightService, RemoteResponse<RemoteDocumentHighlight[]?>>(
            razorDocument.Project.Solution,
            (service, solutionInfo, cancellationToken) => service.GetHighlightsAsync(solutionInfo, razorDocument.Id, request.Position.ToLinePosition(), cancellationToken),
            cancellationToken).ConfigureAwait(false);

        // If we got a response back, then either Razor or C# wants to do something with this, so we're good to go
        if (csharpResult.Result is { } highlights)
        {
            return highlights.Select(RemoteDocumentHighlight.ToLspDocumentHighlight).ToArray();
        }

        if (csharpResult.StopHandling)
        {
            return null;
        }

        // If we didn't get anything from Razor or Roslyn, lets ask Html what they want to do
        return await _requestInvoker.MakeHtmlLspRequestAsync<DocumentHighlightParams, DocumentHighlight[]>(
            razorDocument,
            Methods.TextDocumentDocumentHighlightName,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CohostDocumentHighlightEndpoint instance)
    {
        public Task<DocumentHighlight[]?> HandleRequestAsync(DocumentHighlightParams request, TextDocument razorDocument, CancellationToken cancellationToken)
            => instance.HandleRequestAsync(request, razorDocument, cancellationToken);
    }
}
