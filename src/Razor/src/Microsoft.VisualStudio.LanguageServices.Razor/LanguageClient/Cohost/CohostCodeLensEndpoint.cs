// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Remote;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint("textDocument/codeLens")]
[ExportCohostStatelessLspService(typeof(CohostCodeLensEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostCodeLensEndpoint(
    IRemoteServiceInvoker remoteServiceInvoker,
    IIncompatibleProjectService incompatibleProjectService,
    ILoggerFactory loggerFactory)
    : AbstractCohostDocumentEndpoint<CodeLensParams, CodeLens[]?>(incompatibleProjectService)
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<CohostCodeLensEndpoint>();

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(CodeLensParams request)
        => request.TextDocument.ToRazorTextDocumentIdentifier();

    protected override async Task<CodeLens[]?> HandleRequestAsync(CodeLensParams request, TextDocument razorDocument, CancellationToken cancellationToken)
    {
        var result = await _remoteServiceInvoker.TryInvokeAsync<IRemoteCodeLensService, RemoteResponse<CodeLens[]?>>(
            razorDocument.Project.Solution,
            (service, solutionInfo, cancellationToken) => service.GetCodeLensAsync(solutionInfo, razorDocument.Id, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return result.Result;
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CohostCodeLensEndpoint instance)
    {
        public Task<CodeLens[]?> HandleRequestAsync(CodeLensParams request, TextDocument razorDocument, CancellationToken cancellationToken)
            => instance.HandleRequestAsync(request, razorDocument, cancellationToken);
    }
}