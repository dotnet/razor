// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.AutoInsert;
using Microsoft.CodeAnalysis.Razor.Protocol.AutoInsert;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Remote.Razor;
internal class RemoteAutoInsertService(
    IRazorServiceBroker serviceBroker,
    DocumentSnapshotFactory documentSnapshotFactory,
    IAutoInsertService autoInsertService)
    : RazorDocumentServiceBase(serviceBroker, documentSnapshotFactory), IRemoteAutoInsertService
{
    private readonly IAutoInsertService _autoInsertService = autoInsertService;

    public IEnumerable<string> TriggerCharacters => _autoInsertService.TriggerCharacters;

    public ValueTask<RemoteInsertTextEdit?> TryResolveInsertionAsync(
        RazorPinnedSolutionInfoWrapper solutionInfo,
        DocumentId documentId,
        Position position,
        string character,
        bool autoCloseTags,
        CancellationToken cancellationToken)
    => RunServiceAsync(
            solutionInfo,
            documentId,
            context => TryResolveInsertionAsync(
                context,
                position,
                character,
                autoCloseTags,
                cancellationToken),
            cancellationToken);

    private async ValueTask<RemoteInsertTextEdit?> TryResolveInsertionAsync(
        RemoteDocumentContext remoteDocumentContext,
        Position position,
        string character,
        bool autoCloseTags,
        CancellationToken cancellationToken)
    {
        var insertTextEdit = await _autoInsertService.TryResolveInsertionAsync(
            remoteDocumentContext.Snapshot,
            position,
            character,
            autoCloseTags,
            cancellationToken);
        return insertTextEdit.HasValue ? RemoteInsertTextEdit.FromLspInsertTextEdit(insertTextEdit.Value) : null;
    }
}
