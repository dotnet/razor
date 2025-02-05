// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.CodeAnalysis.Razor.MapCode;
using Microsoft.CodeAnalysis.Razor.Telemetry;

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
    ITelemetryReporter telemetryReporter) : IRazorDocumentlessRequestHandler<VSInternalMapCodeParams, WorkspaceEdit?>, ICapabilitiesProvider
{
    private readonly IMapCodeService _mapCodeService = mapCodeService;
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

        return await _mapCodeService.MapCodeAsync(mapperParams.Mappings, mapCodeCorrelationId, cancellationToken).ConfigureAwait(false);
    }
}
