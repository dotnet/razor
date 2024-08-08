// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.AutoInsert;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.AutoInsert;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

using Response = Microsoft.CodeAnalysis.Razor.Remote.RemoteResponse<Microsoft.CodeAnalysis.Razor.Protocol.AutoInsert.RemoteInsertTextEdit?>;

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
    private readonly IRazorDocumentMappingService _documentMappingService
        = args.ExportProvider.GetExportedValue<IRazorDocumentMappingService>();

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

        var languageKind = _documentMappingService.GetLanguageKind(codeDocument, index, rightAssociative: true);
        if (languageKind is RazorLanguageKind.Html)
        {
            return Response.CallHtml;
        }
        else if (languageKind is RazorLanguageKind.Razor)
        {
            linePosition.Deconstruct(out var line, out var lineCharacter);
            var insertTextEdit = await _autoInsertService.TryResolveInsertionAsync(
                remoteDocumentContext.Snapshot,
                new Position(line, lineCharacter),
                character,
                autoCloseTags,
                cancellationToken);
            return insertTextEdit.HasValue
                ? Response.Results(RemoteInsertTextEdit.FromLspInsertTextEdit(insertTextEdit.Value))
                : Response.NoFurtherHandling;
        }

        // TODO: handle C# case

        return Response.NoFurtherHandling;
    }
}
