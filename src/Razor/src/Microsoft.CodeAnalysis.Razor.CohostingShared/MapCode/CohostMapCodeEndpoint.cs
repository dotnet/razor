// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Features;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Telemetry;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(VSInternalMethods.WorkspaceMapCodeName)]
[Export(typeof(IDynamicRegistrationProvider))]
[ExportRazorStatelessLspService(typeof(CohostMapCodeEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostMapCodeEndpoint(
    ITelemetryReporter telemetryReporter,
    IRemoteServiceInvoker remoteServiceInvoker)
    : AbstractRazorCohostRequestHandler<VSInternalMapCodeParams, WorkspaceEdit?>, IDynamicRegistrationProvider
{
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;

    protected override bool MutatesSolutionState => false;
    protected override bool RequiresLSPSolution => true;

    public ImmutableArray<Registration> GetRegistrations(VSInternalClientCapabilities clientCapabilities, RazorCohostRequestContext requestContext)
    {
        return [new Registration
        {
            Method = VSInternalMethods.WorkspaceMapCodeName,
            RegisterOptions = new TextDocumentRegistrationOptions()
        }];
    }

    protected async override Task<WorkspaceEdit?> HandleRequestAsync(VSInternalMapCodeParams request, RazorCohostRequestContext context, CancellationToken cancellationToken)
    {
        var solution = context.Solution.AssumeNotNull();
        var mappings = request.Mappings;
        var correlationId = request.MapCodeCorrelationId ?? System.Guid.NewGuid();

        using var ts = _telemetryReporter.TrackLspRequest(VSInternalMethods.WorkspaceMapCodeName, RazorLSPConstants.CohostLanguageServerName, TelemetryThresholds.MapCodeRazorTelemetryThreshold, correlationId);

        return await HandleRequestAsync(solution, mappings, correlationId, cancellationToken).ConfigureAwait(false);
    }

    private ValueTask<WorkspaceEdit?> HandleRequestAsync(Solution solution, VSInternalMapCodeMapping[] mappings, System.Guid correlationId, CancellationToken cancellationToken)
        => _remoteServiceInvoker.TryInvokeAsync<IRemoteMapCodeService, WorkspaceEdit?>(
            solution,
            (service, solutionInfo, ct) => service.MapCodeAsync(solutionInfo, mappings, correlationId, ct),
            cancellationToken);

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CohostMapCodeEndpoint instance)
    {
        public ValueTask<WorkspaceEdit?> HandleRequestAsync(Solution solution, VSInternalMapCodeMapping[] mappings, System.Guid correlationId, CancellationToken cancellationToken)
            => instance.HandleRequestAsync(solution, mappings, correlationId, cancellationToken);
    }
}
