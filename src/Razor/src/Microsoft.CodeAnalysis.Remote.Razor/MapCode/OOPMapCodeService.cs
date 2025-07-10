// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Composition;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.MapCode;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Remote.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using ExternalHandlers = Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost.Handlers;
using RazorSyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.CodeAnalysis.Remote.Razor.MapCode;

[Export(typeof(IMapCodeService)), Shared]
[method: ImportingConstructor]
internal class OOPMapCodeService(
    IClientCapabilitiesService clientCapabilitiesService,
    IDocumentMappingService documentMappingService)
    : AbstractMapCodeService(documentMappingService)
{
    private readonly IClientCapabilitiesService _clientCapabilitiesService = clientCapabilitiesService;
    private readonly IDocumentMappingService _documentMappingService = documentMappingService;

    protected override bool TryCreateDocumentContext(ISolutionQueryOperations queryOperations, Uri uri, [NotNullWhen(true)] out DocumentContext? documentContext)
    {
        var filePath = RazorUri.GetDocumentFilePathFromUri(uri);
        if (queryOperations.GetProjectsContainingDocument(filePath) is [{ } project] &&
            project.GetDocument(filePath) is { } snapshot)
        {
            documentContext = new RemoteDocumentContext(uri, (RemoteDocumentSnapshot)snapshot);
            return true;
        }

        documentContext = null;
        return false;
    }

    protected async override Task<(Uri MappedDocumentUri, LinePositionSpan MappedRange)> MapToHostDocumentUriAndRangeAsync(DocumentContext documentContext, Uri generatedDocumentUri, LinePositionSpan generatedDocumentRange, CancellationToken cancellationToken)
    {
        Debug.Assert(documentContext is RemoteDocumentContext, "This method only works on document snapshots created in the OOP process");
        var snapshot = (RemoteDocumentSnapshot)documentContext.Snapshot;
        return await _documentMappingService.MapToHostDocumentUriAndRangeAsync(snapshot, generatedDocumentUri, generatedDocumentRange, cancellationToken).ConfigureAwait(false);
    }

    protected async override Task<WorkspaceEdit?> TryGetCSharpMapCodeEditsAsync(DocumentContext documentContext, Guid mapCodeCorrelationId, RazorSyntaxNode nodeToMap, LspLocation[][] focusLocations, CancellationToken cancellationToken)
    {
        Debug.Assert(_clientCapabilitiesService.CanGetClientCapabilities);
        Debug.Assert(documentContext is RemoteDocumentContext, "This method only works on document snapshots created in the OOP process");
        var snapshot = (RemoteDocumentSnapshot)documentContext.Snapshot;

        var generatedDocument = await snapshot.GetGeneratedDocumentAsync(cancellationToken).ConfigureAwait(false);

        var request = new VSInternalMapCodeParams()
        {
            Mappings = [new VSInternalMapCodeMapping()
            {
                TextDocument = documentContext.GetTextDocumentIdentifier().WithUri(generatedDocument.CreateUri()),
                Contents = [nodeToMap.ToString()],
                FocusLocations = focusLocations,
            }],
            MapCodeCorrelationId = mapCodeCorrelationId,
        };

        return await ExternalHandlers.MapCode.GetMappedWorkspaceEditAsync(
            generatedDocument.Project.Solution,
            request.Mappings,
            _clientCapabilitiesService.ClientCapabilities.Workspace?.WorkspaceEdit?.DocumentChanges ?? false,
            cancellationToken).ConfigureAwait(false);
    }
}
