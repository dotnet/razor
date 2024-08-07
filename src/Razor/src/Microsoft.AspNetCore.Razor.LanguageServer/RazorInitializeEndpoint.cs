// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

[RazorLanguageServerEndpoint(Methods.InitializeName)]
internal class RazorInitializeEndpoint(
        LanguageServerFeatureOptions languageServerFeatureOptions) : IRazorDocumentlessRequestHandler<InitializeParams, InitializeResult>
{
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions = languageServerFeatureOptions;

    public bool MutatesSolutionState { get; } = true;

    public Task<InitializeResult> HandleRequestAsync(InitializeParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        var capabilitiesManager = requestContext.GetRequiredService<IInitializeManager<InitializeParams, InitializeResult>>();

        capabilitiesManager.SetInitializeParams(request);
        var serverCapabilities = capabilitiesManager.GetInitializeResult();

        var telemetryReporter = requestContext.GetRequiredService<ITelemetryReporter>();

        telemetryReporter.ReportEvent("initialize", Severity.Normal, new
            Property(nameof(LanguageServerFeatureOptions.ForceRuntimeCodeGeneration), _languageServerFeatureOptions.ForceRuntimeCodeGeneration)
            );

        return Task.FromResult(serverCapabilities);
    }
}
