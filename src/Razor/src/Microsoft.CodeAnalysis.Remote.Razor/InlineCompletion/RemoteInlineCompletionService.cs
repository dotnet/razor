﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteInlineCompletionService(in ServiceArgs args) : RazorDocumentServiceBase(in args), IRemoteInlineCompletionService
{
    internal sealed class Factory : FactoryBase<IRemoteInlineCompletionService>
    {
        protected override IRemoteInlineCompletionService CreateService(in ServiceArgs args)
            => new RemoteInlineCompletionService(in args);
    }

    private readonly IDocumentMappingService _documentMappingService = args.ExportProvider.GetExportedValue<IDocumentMappingService>();

    public ValueTask<InlineCompletionRequestInfo?> GetInlineCompletionInfoAsync(RazorPinnedSolutionInfoWrapper solutionInfo, DocumentId documentId, LinePosition linePosition, CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            documentId,
            context => GetInlineCompletionInfoAsync(context, linePosition, cancellationToken),
            cancellationToken);

    public async ValueTask<InlineCompletionRequestInfo?> GetInlineCompletionInfoAsync(RemoteDocumentContext context, LinePosition linePosition, CancellationToken cancellationToken)
    {
        var codeDocument = await context.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var csharpDocument = codeDocument.GetCSharpDocument();

        if (!codeDocument.Source.Text.TryGetAbsoluteIndex(linePosition, out var hostDocumentPosition))
        {
            return null;
        }

        if (!_documentMappingService.TryMapToGeneratedDocumentPosition(csharpDocument, hostDocumentPosition, out var mappedPosition, out _))
        {
            return null;
        }

        var generatedDocument = await context.Snapshot.GetGeneratedDocumentAsync(cancellationToken).ConfigureAwait(false);
        return new InlineCompletionRequestInfo(
            GeneratedDocumentUri: generatedDocument.CreateUri(),
            Position: mappedPosition);
    }

    public ValueTask<FormattedInlineCompletionInfo?> FormatInlineCompletionAsync(RazorPinnedSolutionInfoWrapper solutionInfo, DocumentId documentId, RazorFormattingOptions options, LinePositionSpan span, string text, CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            documentId,
            context => FormatInlineCompletionAsync(context, options, span, text, cancellationToken),
            cancellationToken);

    private async ValueTask<FormattedInlineCompletionInfo?> FormatInlineCompletionAsync(RemoteDocumentContext context, RazorFormattingOptions options, LinePositionSpan span, string text, CancellationToken cancellationToken)
    {
        var codeDocument = await context.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var csharpDocument = codeDocument.GetCSharpDocument();

        if (!_documentMappingService.TryMapToHostDocumentRange(csharpDocument, span, out var razorRange))
        {
            return null;
        }

        var hostDocumentIndex = codeDocument.Source.Text.GetRequiredAbsoluteIndex(razorRange.End);

        var formattingContext = FormattingContext.Create(context.Snapshot, codeDocument, options, useNewFormattingEngine: false);
        if (!SnippetFormatter.TryGetSnippetWithAdjustedIndentation(formattingContext, text, hostDocumentIndex, out var newSnippetText))
        {
            return null;
        }

        return new FormattedInlineCompletionInfo(razorRange, newSnippetText);
    }
}
