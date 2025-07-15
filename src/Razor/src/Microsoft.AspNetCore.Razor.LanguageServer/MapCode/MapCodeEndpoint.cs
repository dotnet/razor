// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.MapCode;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Telemetry;
using Microsoft.CommonLanguageServerProtocol.Framework;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode;

/// <summary>
/// Maps requested code to a given Razor document.
/// </summary>
/// <remarks>
/// This class and its mapping heuristics will likely be constantly evolving as we receive
/// more advanced inputs from the client.
/// </remarks>
[RazorLanguageServerEndpoint(VSInternalMethods.WorkspaceMapCodeName)]
internal sealed class MapCodeEndpoint(
    IMapCodeService mapCodeService,
    ProjectSnapshotManager projectSnapshotManager,
    IDocumentContextFactory documentContextFactory,
    IClientConnection clientConnection,
    ITelemetryReporter telemetryReporter) : IRazorDocumentlessRequestHandler<VSInternalMapCodeParams, WorkspaceEdit?>, ICapabilitiesProvider
{
    private readonly IMapCodeService _mapCodeService = mapCodeService;
    private readonly ProjectSnapshotManager _projectSnapshotManager = projectSnapshotManager;
    private readonly IDocumentContextFactory _documentContextFactory = documentContextFactory;
    private readonly IClientConnection _clientConnection = clientConnection;
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;

    public bool MutatesSolutionState => false;

    public void ApplyCapabilities(VSInternalServerCapabilities serverCapabilities, VSInternalClientCapabilities _)
    {
        serverCapabilities.EnableMapCodeProvider();
    }

    public async Task<WorkspaceEdit?> HandleRequestAsync(
        VSInternalMapCodeParams mapperParams,
        RazorRequestContext context,
        CancellationToken cancellationToken)
    {
        // TO-DO: Apply updates to the workspace before doing mapping. This is currently
        // unimplemented by the client, so we won't bother doing anything for now until
        // we determine what kinds of updates the client will actually send us.
        Debug.Assert(mapperParams.Updates is null);

        if (mapperParams.Updates is not null)
        {
            return null;
        }

        var mapCodeCorrelationId = mapperParams.MapCodeCorrelationId ?? Guid.NewGuid();
        using var ts = _telemetryReporter.TrackLspRequest(VSInternalMethods.WorkspaceMapCodeName, LanguageServerConstants.RazorLanguageServerName, TelemetryThresholds.MapCodeRazorTelemetryThreshold, mapCodeCorrelationId);

        using var _ = ListPool<TextDocumentEdit>.GetPooledObject(out var changes);
        foreach (var mapping in mapperParams.Mappings)
        {
            if (mapping.TextDocument is null || mapping.FocusLocations is null)
            {
                continue;
            }

            foreach (var content in mapping.Contents)
            {
                var queryOperations = _projectSnapshotManager.GetQueryOperations();
                var csharpFocusLocationsAndNodes = await _mapCodeService.GetCSharpFocusLocationsAndNodesAsync(queryOperations, mapping.TextDocument, mapping.FocusLocations, content, cancellationToken).ConfigureAwait(false);

                if (!_documentContextFactory.TryCreate(mapping.TextDocument, out var documentContext))
                {
                    continue;
                }

                using var csharpEdits = new PooledArrayBuilder<WorkspaceEdit>();
                if (csharpFocusLocationsAndNodes is not null)
                {
                    foreach (var csharpBody in csharpFocusLocationsAndNodes.CSharpNodeBodies)
                    {
                        var edit = await GetCSharpMapCodeEditAsync(documentContext, mapCodeCorrelationId, csharpBody, csharpFocusLocationsAndNodes.FocusLocations, cancellationToken).ConfigureAwait(false);
                        if (edit is null)
                        {
                            // If any of the C# doesn't map, assume none of it will, and try the next step
                            csharpEdits.Clear();
                            continue;
                        }

                        csharpEdits.Add(edit);
                    }
                }

                await _mapCodeService.MapCSharpEditsAndRazorCodeAsync(queryOperations, content, changes, csharpEdits.ToImmutable(), mapping.TextDocument, mapping.FocusLocations, cancellationToken).ConfigureAwait(false);
            }
        }

        if (changes.Count == 0)
        {
            // No changes were made, return null to indicate no edits.
            return null;
        }

        return new WorkspaceEdit
        {
            DocumentChanges = AbstractMapCodeService.GetMergeEdits(changes)
        };
    }

    private async Task<WorkspaceEdit?> GetCSharpMapCodeEditAsync(DocumentContext documentContext, Guid mapCodeCorrelationId, string nodeToMapContents, Location[][] focusLocations, CancellationToken cancellationToken)
    {
        var delegatedRequest = new DelegatedMapCodeParams(
            documentContext.GetTextDocumentIdentifierAndVersion(),
            RazorLanguageKind.CSharp,
            mapCodeCorrelationId,
            [nodeToMapContents],
            FocusLocations: focusLocations);

        try
        {
            return await _clientConnection.SendRequestAsync<DelegatedMapCodeParams, WorkspaceEdit?>(
                CustomMessageNames.RazorMapCodeEndpoint,
                delegatedRequest,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // C# hasn't implemented + merged their C# code mapper yet.
        }

        return null;
    }
}
