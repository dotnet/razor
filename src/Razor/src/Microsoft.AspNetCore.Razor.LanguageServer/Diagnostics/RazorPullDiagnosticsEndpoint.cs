// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;

internal class RazorPullDiagnosticsEndpoint
    : AbstractRazorDelegatingEndpoint<VSInternalDocumentDiagnosticsParamsBridge, IEnumerable<VSInternalDiagnosticReport>>,
    IRazorPullDiagnosticsEndpoint
{
    public RazorPullDiagnosticsEndpoint(
        LanguageServerFeatureOptions languageServerFeatureOptions,
        RazorDocumentMappingService documentMappingService,
        ClientNotifierServiceBase languageServer,
        ILoggerFactory loggerFactory)
        : base(languageServerFeatureOptions, documentMappingService, languageServer, loggerFactory.CreateLogger<RazorPullDiagnosticsEndpoint>())
    {
    }

    protected override string CustomMessageTarget => RazorLanguageServerCustomMessageTargets.RazorPullDiagnosticEndpointName;

    public RegistrationExtensionResult GetRegistration(VSInternalClientCapabilities clientCapabilities)
    {
        return new RegistrationExtensionResult("_vs_supportsDiagnosticRequests", options: true);
    }

    protected override Task<IDelegatedParams?> CreateDelegatedParamsAsync(
        VSInternalDocumentDiagnosticsParamsBridge request,
        RazorRequestContext requestContext,
        Projection? projection,
        CancellationToken cancellationToken)
    {
        var documentContext = requestContext.GetRequiredDocumentContext();
        var delegatedParams = new DelegatedDiagnosticParams(documentContext.Identifier, RazorLanguageKind.CSharp);

        return Task.FromResult<IDelegatedParams?>(delegatedParams);
    }

    protected override async Task<IEnumerable<VSInternalDiagnosticReport>> HandleDelegatedResponseAsync(IEnumerable<VSInternalDiagnosticReport> delegatedResponse, VSInternalDocumentDiagnosticsParamsBridge originalRequest, RazorRequestContext requestContext, Projection? projection, CancellationToken cancellationToken)
    {
        var documentContext = requestContext.GetRequiredDocumentContext();
        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken);

        foreach(var report in delegatedResponse)
        {
            if (report.Diagnostics is not null)
            {
                foreach(var diagnostic in report.Diagnostics)
                {
                    if(_documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, diagnostic.Range, out var razorRange))
                    {
                        diagnostic.Range = razorRange;
                    }
                }
            }
        }

        return delegatedResponse;
    }
}
