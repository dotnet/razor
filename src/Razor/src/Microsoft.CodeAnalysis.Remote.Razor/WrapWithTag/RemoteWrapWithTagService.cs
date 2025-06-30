// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Razor.Workspaces.Utilities;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Response = Microsoft.CodeAnalysis.Razor.Remote.RemoteResponse<bool>;
using TextEditResponse = Microsoft.CodeAnalysis.Razor.Remote.RemoteResponse<Microsoft.CodeAnalysis.Text.TextEdit[]?>;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed partial class RemoteWrapWithTagService(in ServiceArgs args) : RazorDocumentServiceBase(in args), IRemoteWrapWithTagService
{
    internal sealed class Factory : FactoryBase<IRemoteWrapWithTagService>
    {
        protected override IRemoteWrapWithTagService CreateService(in ServiceArgs args)
            => new RemoteWrapWithTagService(in args);
    }

    public ValueTask<Response> IsValidWrapWithTagLocationAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId razorDocumentId,
        LinePositionSpan range,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            razorDocumentId,
            context => IsValidWrapWithTagLocationAsync(context, range, cancellationToken),
            cancellationToken);

    public ValueTask<TextEditResponse> FixHtmlTextEditsAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId razorDocumentId,
        TextEdit[] textEdits,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            razorDocumentId,
            context => FixHtmlTextEditsAsync(context, textEdits, cancellationToken),
            cancellationToken);

    private async ValueTask<Response> IsValidWrapWithTagLocationAsync(
        RemoteDocumentContext context,
        LinePositionSpan range,
        CancellationToken cancellationToken)
    {
        var codeDocument = await context.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var isValid = await WrapWithTagHelper.IsValidWrappingRangeAsync(codeDocument, range, cancellationToken).ConfigureAwait(false);
        return Response.Results(isValid);
    }

    private async ValueTask<TextEditResponse> FixHtmlTextEditsAsync(
        RemoteDocumentContext context,
        TextEdit[] textEdits,
        CancellationToken cancellationToken)
    {
        if (textEdits.Length == 0)
        {
            return TextEditResponse.Results(textEdits);
        }

        var codeDocument = await context.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var htmlSourceText = codeDocument.GetHtmlSourceText();

        var fixedEdits = FormattingUtilities.FixHtmlTextEdits(htmlSourceText, textEdits);
        return TextEditResponse.Results(fixedEdits);
    }
}