// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.MapCode;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteMapCodeService(in ServiceArgs args) : RazorDocumentServiceBase(in args), IRemoteMapCodeService
{
    internal sealed class Factory : FactoryBase<IRemoteMapCodeService>
    {
        protected override IRemoteMapCodeService CreateService(in ServiceArgs args)
            => new RemoteMapCodeService(in args);
    }

    private readonly IMapCodeService _mapCodeService = args.ExportProvider.GetExportedValue<IMapCodeService>();
    private readonly RemoteSnapshotManager _snapshotManager = args.ExportProvider.GetExportedValue<RemoteSnapshotManager>();

    public ValueTask<CSharpFocusLocationsAndNodes?> GetCSharpFocusLocationsAndNodesAsync(JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo, TextDocumentIdentifier textDocument, LspLocation[][] focusLocations, string content, Guid correlationId, CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            solution => GetCSharpFocusLocationsAndNodesAsync(solution, textDocument, focusLocations, content, cancellationToken),
            cancellationToken);

    private async ValueTask<CSharpFocusLocationsAndNodes?> GetCSharpFocusLocationsAndNodesAsync(Solution solution, TextDocumentIdentifier textDocument, LspLocation[][] focusLocations, string content, CancellationToken cancellationToken)
    {
        var queryOperations = _snapshotManager.GetSnapshot(solution);
        var result = await _mapCodeService.GetCSharpFocusLocationsAndNodesAsync(queryOperations, textDocument, focusLocations, content, cancellationToken).ConfigureAwait(false);

        if (result is not null)
        {
            // We have to map from the Razor document Uri to the generated document Uri or Roslyn will not be able to generate the edits
            foreach (var locations in result.FocusLocations)
            {
                foreach (var location in locations)
                {
                    if (location is not null &&
                        solution.TryGetRazorDocument(location.DocumentUri.GetRequiredParsedUri(), out var razorDocument) &&
                        queryOperations.GetProject(razorDocument.Project) is { } projectSnapshot &&
                        projectSnapshot.GetDocument(razorDocument) is { } razorDocumentSnapshot &&
                        await projectSnapshot.GetGeneratedDocumentAsync(razorDocumentSnapshot, cancellationToken).ConfigureAwait(false) is { } generatedDocument)
                    {
                        location.DocumentUri = generatedDocument.CreateDocumentUri();
                    }
                }
            }
        }

        return result;
    }

    public ValueTask<ImmutableArray<TextDocumentEdit>> MapCSharpEditsAndRazorCodeAsync(JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo, string content, ImmutableArray<WorkspaceEdit> csharpEdits, TextDocumentIdentifier textDocument, LspLocation[][] focusLocations, CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            solution => MapCSharpEditsAndRazorCodeAsync(solution, content, csharpEdits, textDocument, focusLocations, cancellationToken),
            cancellationToken);

    private async ValueTask<ImmutableArray<TextDocumentEdit>> MapCSharpEditsAndRazorCodeAsync(Solution solution, string content, ImmutableArray<WorkspaceEdit> csharpEdits, TextDocumentIdentifier textDocument, LspLocation[][] focusLocations, CancellationToken cancellationToken)
    {
        var queryOperations = _snapshotManager.GetSnapshot(solution);
        using var _ = ListPool<TextDocumentEdit>.GetPooledObject(out var changes);
        await _mapCodeService.MapCSharpEditsAndRazorCodeAsync(queryOperations, content, changes, csharpEdits, textDocument, focusLocations, cancellationToken).ConfigureAwait(false);
        return [.. changes];
    }
}
