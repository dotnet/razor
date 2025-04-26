// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Telemetry;
using Microsoft.VisualStudio.Razor.Settings;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(VSInternalMethods.DocumentPullDiagnosticName)]
[Export(typeof(IDynamicRegistrationProvider))]
[ExportCohostStatelessLspService(typeof(CohostDocumentPullDiagnosticsEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostDocumentPullDiagnosticsEndpoint(
    IRemoteServiceInvoker remoteServiceInvoker,
    IHtmlRequestInvoker requestInvoker,
    IClientSettingsManager clientSettingsManager,
    ITelemetryReporter telemetryReporter,
    ILoggerFactory loggerFactory)
    : CohostDocumentPullDiagnosticsEndpointBase(remoteServiceInvoker, requestInvoker, telemetryReporter, loggerFactory), IDynamicRegistrationProvider
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;
    private readonly IClientSettingsManager _clientSettingsManager = clientSettingsManager;

    public ImmutableArray<Registration> GetRegistrations(VSInternalClientCapabilities clientCapabilities, RazorCohostRequestContext requestContext)
    {
        if (clientCapabilities.TextDocument?.Diagnostic?.DynamicRegistration is true)
        {
            return [new Registration()
            {
                Method = VSInternalMethods.DocumentPullDiagnosticName,
                RegisterOptions = new VSInternalDiagnosticRegistrationOptions()
                {
                    DiagnosticKinds = [VSInternalDiagnosticKind.Syntax, VSInternalDiagnosticKind.Task]
                }
            }];
        }

        return [];
    }

    protected override Task<VSInternalDiagnosticReport[]?> HandleRequestAsync(VSInternalDocumentDiagnosticsParams request, RazorCohostRequestContext context, CancellationToken cancellationToken)
    {
        if (request.QueryingDiagnosticKind?.Value == VSInternalDiagnosticKind.Task.Value)
        {
            return HandleTaskListItemRequestAsync(
                context.TextDocument.AssumeNotNull(),
                _clientSettingsManager.GetClientSettings().AdvancedSettings.TaskListDescriptors,
                cancellationToken);
        }

        return HandleRequestAsync(context.TextDocument.AssumeNotNull(), cancellationToken);
    }

    private async Task<VSInternalDiagnosticReport[]?> HandleTaskListItemRequestAsync(TextDocument razorDocument, ImmutableArray<string> taskListDescriptors, CancellationToken cancellationToken)
    {
        var diagnostics = await _remoteServiceInvoker.TryInvokeAsync<IRemoteDiagnosticsService, ImmutableArray<LspDiagnostic>>(
            razorDocument.Project.Solution,
            (service, solutionInfo, cancellationToken) => service.GetTaskListDiagnosticsAsync(solutionInfo, razorDocument.Id, taskListDescriptors, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        if (diagnostics.IsDefaultOrEmpty)
        {
            return null;
        }

        return
        [
            new()
            {
                Diagnostics = [.. diagnostics],
                ResultId = Guid.NewGuid().ToString()
            }
        ];
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CohostDocumentPullDiagnosticsEndpoint instance)
    {
        public Task<VSInternalDiagnosticReport[]?> HandleRequestAsync(TextDocument razorDocument, CancellationToken cancellationToken)
            => instance.HandleRequestAsync(razorDocument, cancellationToken);

        public Task<VSInternalDiagnosticReport[]?> HandleTaskListItemRequestAsync(TextDocument razorDocument, ImmutableArray<string> taskListDescriptors, CancellationToken cancellationToken)
            => instance.HandleTaskListItemRequestAsync(razorDocument, taskListDescriptors, cancellationToken);
    }
}

