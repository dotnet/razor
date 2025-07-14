// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.MapCode;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Telemetry;
using ExternalHandlers = Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost.Handlers;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(VSInternalMethods.WorkspaceMapCodeName)]
[Export(typeof(IDynamicRegistrationProvider))]
[ExportCohostStatelessLspService(typeof(CohostMapCodeEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostMapCodeEndpoint(
    IClientCapabilitiesService clientCapabilitiesService,
    ITelemetryReporter telemetryReporter,
    IRemoteServiceInvoker remoteServiceInvoker)
    : AbstractRazorCohostRequestHandler<VSInternalMapCodeParams, WorkspaceEdit?>, IDynamicRegistrationProvider
{
    private readonly IClientCapabilitiesService _clientCapabilitiesService = clientCapabilitiesService;
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
        // TO-DO: Apply updates to the workspace before doing mapping. This is currently
        // unimplemented by the client, so we won't bother doing anything for now until
        // we determine what kinds of updates the client will actually send us.
        Debug.Assert(request.Updates is null);

        if (request.Updates is not null)
        {
            return null;
        }

        var solution = context.Solution.AssumeNotNull();
        var correlationId = request.MapCodeCorrelationId ?? Guid.NewGuid();

        using var ts = _telemetryReporter.TrackLspRequest(VSInternalMethods.WorkspaceMapCodeName, RazorLSPConstants.CohostLanguageServerName, TelemetryThresholds.MapCodeRazorTelemetryThreshold, correlationId);

        return await HandleRequestAsync(context, solution, request.Mappings, correlationId, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<WorkspaceEdit?> HandleRequestAsync(RazorCohostRequestContext? context, Solution solution, VSInternalMapCodeMapping[] mappings, Guid correlationId, CancellationToken cancellationToken)
    {
        using var _ = ListPool<TextDocumentEdit>.GetPooledObject(out var changes);
        foreach (var mapping in mappings)
        {
            if (mapping.TextDocument is null || mapping.FocusLocations is null)
            {
                continue;
            }

            foreach (var content in mapping.Contents)
            {
                var csharpFocusLocationsAndNodes = await _remoteServiceInvoker.TryInvokeAsync<IRemoteMapCodeService, CSharpFocusLocationsAndNodes?>(
                    solution,
                    (service, solutionInfo, ct) => service.GetCSharpFocusLocationsAndNodesAsync(solutionInfo, mapping.TextDocument, mapping.FocusLocations, content, correlationId, ct),
                    cancellationToken).ConfigureAwait(false);

                using var csharpEditsBuilder = new PooledArrayBuilder<WorkspaceEdit>();
                if (csharpFocusLocationsAndNodes is not null)
                {
                    using var ts = _telemetryReporter.TrackLspRequest(VSInternalMethods.WorkspaceMapCodeName, RazorLSPConstants.RoslynLanguageServerName, TelemetryThresholds.MapCodeRazorTelemetryThreshold, correlationId);

                    foreach (var csharpBody in csharpFocusLocationsAndNodes.CSharpNodeBodies)
                    {
                        var edit = await GetCSharpMapCodeEditAsync(context, solution, mapping.TextDocument, csharpBody, csharpFocusLocationsAndNodes.FocusLocations, cancellationToken).ConfigureAwait(false);
                        if (edit is null)
                        {
                            // If any of the C# doesn't map, assume none of it will, and try the next step
                            csharpEditsBuilder.Clear();
                            continue;
                        }

                        csharpEditsBuilder.Add(edit);
                    }
                }

                var csharpEdits = csharpEditsBuilder.ToImmutable();
                var result = await _remoteServiceInvoker.TryInvokeAsync<IRemoteMapCodeService, ImmutableArray<TextDocumentEdit>>(
                    solution,
                    (service, solutionInfo, ct) => service.MapCSharpEditsAndRazorCodeAsync(solutionInfo, content, csharpEdits, mapping.TextDocument, mapping.FocusLocations, ct),
                    cancellationToken).ConfigureAwait(false);

                if (result.IsDefaultOrEmpty)
                {
                    continue;
                }

                changes.AddRange(result);
            }
        }

        if (changes.Count == 0)
        {
            // No changes were made, return null to indicate no edits.
            return null;
        }

        AbstractMapCodeService.MergeEdits(changes);

        return new WorkspaceEdit
        {
            DocumentChanges = changes.ToArray()
        };
    }

    private async Task<WorkspaceEdit?> GetCSharpMapCodeEditAsync(RazorCohostRequestContext? context, Solution solution, TextDocumentIdentifier textDocument, string nodeToMapContents, LspLocation[][] focusLocations, CancellationToken cancellationToken)
    {
        if (!solution.TryGetRazorDocument(textDocument.DocumentUri.GetRequiredParsedUri(), out var razorDocument) ||
            !razorDocument.TryComputeHintNameFromRazorDocument(out var hintName) ||
            await razorDocument.Project.TryGetSourceGeneratedDocumentFromHintNameAsync(hintName, cancellationToken).ConfigureAwait(false) is not { } generatedDocument)
        {
            return null;
        }

        var mapping = new VSInternalMapCodeMapping()
        {
            TextDocument = new() { DocumentUri = generatedDocument.CreateDocumentUri() },
            Contents = [nodeToMapContents],
            FocusLocations = focusLocations,
        };

        return await ExternalHandlers.MapCode.GetMappedWorkspaceEditAsync(
            context,
            generatedDocument.Project.Solution,
            [mapping],
            _clientCapabilitiesService.ClientCapabilities.Workspace?.WorkspaceEdit?.DocumentChanges ?? false,
            cancellationToken).ConfigureAwait(false);
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CohostMapCodeEndpoint instance)
    {
        public ValueTask<WorkspaceEdit?> HandleRequestAsync(Solution solution, VSInternalMapCodeMapping[] mappings, Guid correlationId, CancellationToken cancellationToken)
            => instance.HandleRequestAsync(context: null, solution, mappings, correlationId, cancellationToken);
    }
}
