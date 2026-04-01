// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Features;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Remote;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[RazorMethod(RazorLSPConstants.AddNestedFileName)]
[ExportRazorStatelessLspService(typeof(CohostAddNestedFileEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostAddNestedFileEndpoint(
    IRemoteServiceInvoker remoteServiceInvoker,
    ILoggerFactory loggerFactory)
    : AbstractRazorCohostRequestHandler<AddNestedFileParams, VoidResult>
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<CohostAddNestedFileEndpoint>();

    protected override bool MutatesSolutionState => true;

    protected override bool RequiresLSPSolution => true;

    protected override async Task<VoidResult> HandleRequestAsync(
        AddNestedFileParams request,
        RazorCohostRequestContext context,
        CancellationToken cancellationToken)
    {
        var solution = context.Solution;
        if (solution is null)
        {
            _logger.LogWarning($"No solution available for addNestedFile request.");
            return new();
        }

        var workspaceEdit = await _remoteServiceInvoker.TryInvokeAsync<IRemoteAddNestedFileService, WorkspaceEdit?>(
            solution,
            (service, solutionInfo, ct) => service.AddNestedFileAsync(
                solutionInfo,
                request.RazorFileUri,
                request.FileKind,
                ct),
            cancellationToken).ConfigureAwait(false);

        if (workspaceEdit is null)
        {
            _logger.LogWarning($"Remote service returned no edit for addNestedFile.");
            return new();
        }

        var razorCohostClientLanguageServerManager = context.GetRequiredService<IRazorClientLanguageServerManager>();
        var response = await razorCohostClientLanguageServerManager.SendRequestAsync<ApplyWorkspaceEditParams, ApplyWorkspaceEditResponse>(
            Methods.WorkspaceApplyEditName,
            new ApplyWorkspaceEditParams { Edit = workspaceEdit },
            cancellationToken).ConfigureAwait(false);

        if (!response.Applied)
        {
            _logger.LogWarning($"Failed to apply workspace edit for addNestedFile: {response.FailureReason}");
        }

        return new();
    }
}
