// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost.Handlers;
using Microsoft.CodeAnalysis.Razor.AutoInsert;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.AutoInsert;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

using Response = Microsoft.CodeAnalysis.Razor.Remote.RemoteResponse<Microsoft.CodeAnalysis.Razor.Protocol.AutoInsert.RemoteInsertTextEdit?>;
using RoslynFormattingOptions = Roslyn.LanguageServer.Protocol.FormattingOptions;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal class RemoteAutoInsertService(in ServiceArgs args)
    : RazorDocumentServiceBase(in args), IRemoteAutoInsertService
{
    internal sealed class Factory : FactoryBase<IRemoteAutoInsertService>
    {
        protected override IRemoteAutoInsertService CreateService(in ServiceArgs args)
            => new RemoteAutoInsertService(in args);
    }

    private readonly IAutoInsertService _autoInsertService
        = args.ExportProvider.GetExportedValue<IAutoInsertService>();
    private readonly IDocumentMappingService _documentMappingService
        = args.ExportProvider.GetExportedValue<IDocumentMappingService>();
    private readonly IFilePathService _filePathService =
        args.ExportProvider.GetExportedValue<IFilePathService>();

    public ValueTask<Response> TryResolveInsertionAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId documentId,
        LinePosition linePosition,
        string character,
        bool autoCloseTags,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            documentId,
            context => TryResolveInsertionAsync(
                context,
                linePosition,
                character,
                autoCloseTags,
                cancellationToken),
            cancellationToken);

    private async ValueTask<Response> TryResolveInsertionAsync(
        RemoteDocumentContext remoteDocumentContext,
        LinePosition linePosition,
        string character,
        bool autoCloseTags,
        CancellationToken cancellationToken)
    {
        var sourceText = await remoteDocumentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        if (!sourceText.TryGetAbsoluteIndex(linePosition, out var index))
        {
            return Response.NoFurtherHandling;
        }

        var codeDocument = await remoteDocumentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

        // Always try our own service first, regardless of language
        // E.g. if ">" is typed for html tag, it's actually our auto-insert provider
        // that adds closing tag instead of HTML even though we are in HTML
        var insertTextEdit = _autoInsertService.TryResolveInsertion(
            codeDocument,
            linePosition.ToPosition(),
            character,
            autoCloseTags);

        if (insertTextEdit is { } edit)
        {
            return Response.Results(RemoteInsertTextEdit.FromLspInsertTextEdit(edit));
        }

        var languageKind = _documentMappingService.GetLanguageKind(codeDocument, index, rightAssociative: true);
        if (languageKind is RazorLanguageKind.Razor)
        {
            // If we are in Razor and got no edit from our own service, there is nothing else to do
            return Response.NoFurtherHandling;
        }
        else if (languageKind is RazorLanguageKind.Html)
        {
            return AutoInsertService.HtmlAllowedAutoInsertTriggerCharacters.Contains(character)
                ? Response.CallHtml
                : Response.NoFurtherHandling;
        }

        // C# case

        if (!AutoInsertService.CSharpAllowedAutoInsertTriggerCharacters.Contains(character))
        {
            return Response.NoFurtherHandling;
        }

        var csharpDocument = codeDocument.GetCSharpDocument();
        if (_documentMappingService.TryMapToGeneratedDocumentPosition(csharpDocument, index, out var mappedPosition, out _))
        {
            var generatedDocument = await remoteDocumentContext.GetGeneratedDocumentAsync(_filePathService, cancellationToken).ConfigureAwait(false);
            // TODO: use correct options rather than default
            var formattingOptions = new RoslynFormattingOptions();
            var autoInsertResponseItem = await OnAutoInsert.GetOnAutoInsertResponseAsync(
                generatedDocument,
                mappedPosition,
                character,
                formattingOptions,
                cancellationToken
            );
            return autoInsertResponseItem is not null
                ? Response.Results(RemoteInsertTextEdit.FromRoslynAutoInsertResponse(autoInsertResponseItem))
                : Response.NoFurtherHandling;
        }

        return Response.NoFurtherHandling;
    }
}
