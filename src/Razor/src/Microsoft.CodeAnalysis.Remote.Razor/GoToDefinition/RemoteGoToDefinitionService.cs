// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Remote.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Roslyn.LanguageServer.Protocol;
using ExternalHandlers = Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost.Handlers;
using RoslynLocation = Roslyn.LanguageServer.Protocol.Location;
using RoslynPosition = Roslyn.LanguageServer.Protocol.Position;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteGoToDefinitionService(in ServiceArgs args) : RazorDocumentServiceBase(in args), IRemoteGoToDefinitionService
{
    internal sealed class Factory : FactoryBase<IRemoteGoToDefinitionService>
    {
        protected override IRemoteGoToDefinitionService CreateService(in ServiceArgs args)
            => new RemoteGoToDefinitionService(in args);
    }

    private readonly IFilePathService _filePathService = args.ExportProvider.GetExportedValue<IFilePathService>();
    private readonly IDocumentMappingService _documentMappingService = args.ExportProvider.GetExportedValue<IDocumentMappingService>();

    public ValueTask<RoslynLocation[]?> GetDefinitionAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        JsonSerializableDocumentId documentId,
        RoslynPosition position,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            documentId,
            context => GetDefinitionAsync(context, position, cancellationToken),
            cancellationToken);

    private async ValueTask<RoslynLocation[]?> GetDefinitionAsync(
        RemoteDocumentContext context,
        RoslynPosition position,
        CancellationToken cancellationToken)
    {
        var codeDocument = await context.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var absoluteIndex = codeDocument.Source.Text.GetRequiredAbsoluteIndex(position.ToLinePosition());

        var generatedDocument = await context.GetGeneratedDocumentAsync(_filePathService, cancellationToken).ConfigureAwait(false);

        if (_documentMappingService.TryMapToGeneratedDocumentPosition(codeDocument.GetCSharpDocument(), absoluteIndex, out var mappedPosition, out _))
        {
            var locations = await ExternalHandlers.GoToDefinition
                .GetDefinitionsAsync(
                    RemoteWorkspaceAccessor.GetWorkspace(),
                    generatedDocument,
                    typeOnly: false,
                    mappedPosition,
                    cancellationToken)
                .ConfigureAwait(false);

            if (locations is null and not [])
            {
                return locations;
            }

            using var mappedLocations = new PooledArrayBuilder<RoslynLocation>(locations.Length);

            foreach (var location in locations)
            {
                var (uri, range) = location;

                var (mappedDocumentUri, mappedRange) = await _documentMappingService
                    .MapToHostDocumentUriAndRangeAsync((RemoteDocumentSnapshot)context.Snapshot, uri, range.ToLinePositionSpan(), cancellationToken)
                    .ConfigureAwait(false);

                var mappedLocation = RoslynLspFactory.CreateLocation(mappedDocumentUri, mappedRange);

                mappedLocations.Add(mappedLocation);
            }

            return mappedLocations.ToArray();
        }

        return null;
    }
}
