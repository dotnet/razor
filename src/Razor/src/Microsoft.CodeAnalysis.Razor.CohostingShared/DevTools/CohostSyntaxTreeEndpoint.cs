// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
[CohostEndpoint("razor/syntaxTree")]
[ExportRazorStatelessLspService(typeof(CohostSyntaxTreeEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostSyntaxTreeEndpoint(
    IIncompatibleProjectService incompatibleProjectService,
    IRemoteServiceInvoker remoteServiceInvoker)
    : AbstractCohostDocumentEndpoint<SyntaxTreeRequest, SyntaxVisualizerTree?>(incompatibleProjectService)
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;

    protected override bool MutatesSolutionState => false;
    protected override bool RequiresLSPSolution => true;

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(SyntaxTreeRequest request)
        => request.TextDocument.ToRazorTextDocumentIdentifier();

    protected override async Task<SyntaxVisualizerTree?> HandleRequestAsync(SyntaxTreeRequest request, TextDocument razorDocument, CancellationToken cancellationToken)
    {
        return await _remoteServiceInvoker.TryInvokeAsync<IRemoteDevToolsService, SyntaxVisualizerTree?>(
            razorDocument.Project.Solution,
            (service, solutionInfo, cancellationToken) => service.GetRazorSyntaxTreeAsync(solutionInfo, razorDocument.Id, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CohostSyntaxTreeEndpoint instance)
    {
        public Task<SyntaxVisualizerTree?> HandleRequestAsync(SyntaxTreeRequest request, TextDocument razorDocument, CancellationToken cancellationToken)
            => instance.HandleRequestAsync(request, razorDocument, cancellationToken);
    }
}