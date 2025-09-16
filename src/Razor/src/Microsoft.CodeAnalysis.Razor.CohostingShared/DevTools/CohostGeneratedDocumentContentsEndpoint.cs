// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Protocol.DevTools;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint("razor/generatedDocumentContents")]
[ExportRazorStatelessLspService(typeof(CohostGeneratedDocumentContentsEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostGeneratedDocumentContentsEndpoint(
    IIncompatibleProjectService incompatibleProjectService,
    IRemoteServiceInvoker remoteServiceInvoker)
    : AbstractCohostDocumentEndpoint<DocumentContentsRequest, DocumentContentsResponse?>(incompatibleProjectService)
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;

    protected override bool MutatesSolutionState => false;
    protected override bool RequiresLSPSolution => true;

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(DocumentContentsRequest request)
        => request.TextDocument.ToRazorTextDocumentIdentifier();

    protected override async Task<DocumentContentsResponse?> HandleRequestAsync(DocumentContentsRequest request, TextDocument razorDocument, CancellationToken cancellationToken)
    {
        string? contents = null;
        
        switch (request.Kind)
        {
            case GeneratedDocumentKind.CSharp:
                contents = await _remoteServiceInvoker.TryInvokeAsync<IRemoteDevToolsService, string?>(
                    razorDocument.Project.Solution,
                    (service, solutionInfo, cancellationToken) => service.GetCSharpDocumentTextAsync(solutionInfo, razorDocument.Id, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
                break;
                
            case GeneratedDocumentKind.Html:
                contents = await _remoteServiceInvoker.TryInvokeAsync<IRemoteDevToolsService, string?>(
                    razorDocument.Project.Solution,
                    (service, solutionInfo, cancellationToken) => service.GetHtmlDocumentTextAsync(solutionInfo, razorDocument.Id, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
                break;
                
            case GeneratedDocumentKind.Formatting:
                contents = await _remoteServiceInvoker.TryInvokeAsync<IRemoteDevToolsService, string?>(
                    razorDocument.Project.Solution,
                    (service, solutionInfo, cancellationToken) => service.GetFormattingDocumentTextAsync(solutionInfo, razorDocument.Id, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
                break;
                
            default:
                throw new ArgumentException($"Unsupported document kind: {request.Kind}", nameof(request));
        }

        if (contents == null)
            return null;

        return new DocumentContentsResponse
        {
            Contents = contents,
            FilePath = razorDocument.FilePath ?? string.Empty
        };
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CohostGeneratedDocumentContentsEndpoint instance)
    {
        public Task<DocumentContentsResponse?> HandleRequestAsync(DocumentContentsRequest request, TextDocument razorDocument, CancellationToken cancellationToken)
            => instance.HandleRequestAsync(request, razorDocument, cancellationToken);
    }
}