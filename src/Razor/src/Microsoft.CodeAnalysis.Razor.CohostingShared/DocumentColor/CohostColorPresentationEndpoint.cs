// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Features;
using Microsoft.CodeAnalysis.Razor.Logging;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(Methods.TextDocumentColorPresentationName)]
[ExportRazorStatelessLspService(typeof(CohostColorPresentationEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostColorPresentationEndpoint(
    IHtmlRequestInvoker requestInvoker,
    ILoggerFactory loggerFactory)
    : AbstractRazorCohostDocumentRequestHandler<ColorPresentationParams, ColorPresentation[]?>
{
    private readonly IHtmlRequestInvoker _requestInvoker = requestInvoker;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<CohostColorPresentationEndpoint>();

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(ColorPresentationParams request)
        => request.TextDocument.ToRazorTextDocumentIdentifier();

    protected override Task<ColorPresentation[]?> HandleRequestAsync(ColorPresentationParams request, RazorCohostRequestContext context, CancellationToken cancellationToken)
        => HandleRequestAsync(request, context.TextDocument.AssumeNotNull(), cancellationToken);

    private async Task<ColorPresentation[]?> HandleRequestAsync(ColorPresentationParams request, TextDocument razorDocument, CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Document color request for {request.TextDocument.Uri}");

        return await _requestInvoker.MakeHtmlLspRequestAsync<ColorPresentationParams, ColorPresentation[]>(
            razorDocument,
            Methods.TextDocumentColorPresentationName,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CohostColorPresentationEndpoint instance)
    {
        public Task<ColorPresentation[]?> HandleRequestAsync(ColorPresentationParams request, TextDocument razorDocument, CancellationToken cancellationToken)
            => instance.HandleRequestAsync(request, razorDocument, cancellationToken);
    }
}
