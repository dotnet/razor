// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
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

    public ValueTask<WorkspaceEdit?> MapCodeAsync(JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo, VSInternalMapCodeMapping[] mappings, Guid mapCodeCorrelationId, CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            solution => MapCodeAsync(solution, mappings, mapCodeCorrelationId, cancellationToken),
            cancellationToken);

    public async ValueTask<WorkspaceEdit?> MapCodeAsync(Solution solution, VSInternalMapCodeMapping[] mappings, Guid mapCodeCorrelationId, CancellationToken cancellationToken)
    {
        var queryOperations = _snapshotManager.GetSnapshot(solution);
        return await _mapCodeService.MapCodeAsync(queryOperations, mappings, mapCodeCorrelationId, cancellationToken).ConfigureAwait(false);
    }
}
