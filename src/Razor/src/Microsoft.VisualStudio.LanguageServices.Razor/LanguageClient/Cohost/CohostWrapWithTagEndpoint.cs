// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Razor.LanguageClient.WrapWithTag;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(LanguageServerConstants.RazorWrapWithTagEndpoint)]
[ExportCohostStatelessLspService(typeof(CohostWrapWithTagEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostWrapWithTagEndpoint(
    IRemoteServiceInvoker remoteServiceInvoker,
    IHtmlRequestInvoker requestInvoker)
    : AbstractRazorCohostDocumentRequestHandler<VSInternalWrapWithTagParams, VSInternalWrapWithTagResponse?>
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;
    private readonly IHtmlRequestInvoker _requestInvoker = requestInvoker;

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(VSInternalWrapWithTagParams request)
        => request.TextDocument.ToRazorTextDocumentIdentifier();

    protected override Task<VSInternalWrapWithTagResponse?> HandleRequestAsync(VSInternalWrapWithTagParams request, RazorCohostRequestContext context, CancellationToken cancellationToken)
        => HandleRequestAsync(request, context.TextDocument.AssumeNotNull(), cancellationToken);

    private async Task<VSInternalWrapWithTagResponse?> HandleRequestAsync(VSInternalWrapWithTagParams request, TextDocument razorDocument, CancellationToken cancellationToken)
    {
        // First, check if the position is valid for wrap with tag operation through the remote service
        var range = request.Range.ToLinePositionSpan();
        var result = await _remoteServiceInvoker.TryInvokeAsync<IRemoteWrapWithTagService, RemoteResponse<LinePositionSpan>>(
            razorDocument.Project.Solution,
            (service, solutionInfo, cancellationToken) => service.GetValidWrappingRangeAsync(solutionInfo, razorDocument.Id, range, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        // If the remote service says it's not a valid location or we should stop handling, return null
        if (result.StopHandling)
        {
            return null;
        }

        request.Range = result.Result.ToRange();

        // The location is valid, so delegate to the HTML server
        var htmlResponse = await _requestInvoker.MakeHtmlLspRequestAsync<VSInternalWrapWithTagParams, VSInternalWrapWithTagResponse>(
            razorDocument,
            LanguageServerConstants.RazorWrapWithTagEndpoint,
            request,
            cancellationToken).ConfigureAwait(false);

        if (htmlResponse?.TextEdits is not null)
        {
            // Fix the HTML text edits to handle any tilde characters in the generated document
            var fixedEdits = await _remoteServiceInvoker.TryInvokeAsync<IRemoteWrapWithTagService, RemoteResponse<TextEdit[]?>>(
                razorDocument.Project.Solution,
                (service, solutionInfo, cancellationToken) => service.FixHtmlTextEditsAsync(solutionInfo, razorDocument.Id, htmlResponse.TextEdits, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            if (fixedEdits.Result is not null)
            {
                htmlResponse.TextEdits = fixedEdits.Result;
            }
        }

        return htmlResponse;
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CohostWrapWithTagEndpoint instance)
    {
        public Task<VSInternalWrapWithTagResponse?> HandleRequestAsync(VSInternalWrapWithTagParams request, TextDocument razorDocument, CancellationToken cancellationToken)
            => instance.HandleRequestAsync(request, razorDocument, cancellationToken);
    }
}
