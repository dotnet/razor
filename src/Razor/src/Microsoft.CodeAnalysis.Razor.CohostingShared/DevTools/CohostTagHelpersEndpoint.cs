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
[CohostEndpoint("razor/tagHelpers")]
[ExportRazorStatelessLspService(typeof(CohostTagHelpersEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostTagHelpersEndpoint(
    IIncompatibleProjectService incompatibleProjectService,
    IRemoteServiceInvoker remoteServiceInvoker)
    : AbstractCohostDocumentEndpoint<TagHelpersRequest, string?>(incompatibleProjectService)
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;

    protected override bool MutatesSolutionState => false;
    protected override bool RequiresLSPSolution => true;

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(TagHelpersRequest request)
        => request.TextDocument.ToRazorTextDocumentIdentifier();

    protected override async Task<string?> HandleRequestAsync(TagHelpersRequest request, TextDocument razorDocument, CancellationToken cancellationToken)
    {
        return await _remoteServiceInvoker.TryInvokeAsync<IRemoteDevToolsService, string>(
            razorDocument.Project.Solution,
            (service, solutionInfo, cancellationToken) => service.GetTagHelpersJsonAsync(solutionInfo, razorDocument.Id, request.Kind, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CohostTagHelpersEndpoint instance)
    {
        public Task<string?> HandleRequestAsync(TagHelpersRequest request, TextDocument razorDocument, CancellationToken cancellationToken)
            => instance.HandleRequestAsync(request, razorDocument, cancellationToken);
    }
}