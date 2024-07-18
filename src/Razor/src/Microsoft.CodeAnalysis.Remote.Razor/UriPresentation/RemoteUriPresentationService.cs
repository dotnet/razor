// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.DocumentPresentation;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed partial class RemoteUriPresentationService(in ServiceArgs args) : RazorDocumentServiceBase(in args), IRemoteUriPresentationService
{
    internal sealed class Factory : FactoryBase<IRemoteUriPresentationService>
    {
        protected override IRemoteUriPresentationService CreateService(in ServiceArgs args)
            => new RemoteUriPresentationService(in args);
    }

    private readonly IRazorDocumentMappingService _documentMappingService = args.ExportProvider.GetExportedValue<IRazorDocumentMappingService>();

    public ValueTask<IRemoteUriPresentationService.Response> GetPresentationAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId razorDocumentId,
        LinePositionSpan span,
        Uri[]? uris,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            razorDocumentId,
            context => GetPresentationAsync(context, span, uris, cancellationToken),
            cancellationToken);

    private static IRemoteUriPresentationService.Response ShouldCallHtml => new(ShouldCallHtml: true, TextChange: null);
    private static IRemoteUriPresentationService.Response DontCallHtml => new(ShouldCallHtml: false, TextChange: null);
    private static IRemoteUriPresentationService.Response TextChange(TextChange textChange) => new(ShouldCallHtml: false, TextChange: textChange);

    private async ValueTask<IRemoteUriPresentationService.Response> GetPresentationAsync(
        RemoteDocumentContext context,
        LinePositionSpan span,
        Uri[]? uris,
        CancellationToken cancellationToken)
    {
        var sourceText = await context.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        if (!sourceText.TryGetAbsoluteIndex(span.Start.Line, span.Start.Character, out var index))
        {
            // If the position is invalid then we shouldn't expect to be able to handle a Html response
            return DontCallHtml;
        }

        var codeDocument = await context.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

        var languageKind = _documentMappingService.GetLanguageKind(codeDocument, index, rightAssociative: true);
        if (languageKind is not RazorLanguageKind.Html)
        {
            // Roslyn doesn't currently support Uri presentation, and whilst it might seem counter intuitive,
            // our support for Uri presentation is to insert a Html tag, so we only support Html

            // If Roslyn add support in future then this is where it would go.
            return DontCallHtml;
        }

        var razorFileUri = UriPresentationHelper.GetComponentFileNameFromUriPresentationRequest(uris, Logger);
        if (razorFileUri is null)
        {
            return ShouldCallHtml;
        }

        var solution = context.TextDocument.Project.Solution;

        // Make sure we go through Roslyn to go from the Uri the client sent us, to one that it has a chance of finding in the solution
        var uriToFind = RazorUri.GetDocumentFilePathFromUri(razorFileUri);
        var ids = solution.GetDocumentIdsWithFilePath(uriToFind);
        if (ids.Length == 0)
        {
            return ShouldCallHtml;
        }

        // We assume linked documents would produce the same component tag so just take the first
        var otherDocument = solution.GetAdditionalDocument(ids[0]);
        if (otherDocument is null)
        {
            return ShouldCallHtml;
        }

        var otherSnapshot = DocumentSnapshotFactory.GetOrCreate(otherDocument);
        var descriptor = await otherSnapshot.TryGetTagHelperDescriptorAsync(cancellationToken).ConfigureAwait(false);

        if (descriptor is null)
        {
            return ShouldCallHtml;
        }

        var tag = descriptor.TryGetComponentTag();
        if (tag is null)
        {
            return ShouldCallHtml;
        }

        return TextChange(new TextChange(span.ToTextSpan(sourceText), tag));
    }
}
