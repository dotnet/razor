// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using static Roslyn.LanguageServer.Protocol.RoslynLspExtensions;
using RoslynLocation = Roslyn.LanguageServer.Protocol.Location;
using RoslynLspFactory = Roslyn.LanguageServer.Protocol.RoslynLspFactory;
using RoslynPosition = Roslyn.LanguageServer.Protocol.Position;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(Methods.TextDocumentDefinitionName)]
[Export(typeof(IDynamicRegistrationProvider))]
[ExportCohostStatelessLspService(typeof(CohostGoToDefinitionEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostGoToDefinitionEndpoint(IRemoteServiceInvoker remoteServiceInvoker)
    : AbstractRazorCohostDocumentRequestHandler<TextDocumentPositionParams, RoslynLocation[]?>, IDynamicRegistrationProvider
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    public Registration? GetRegistration(VSInternalClientCapabilities clientCapabilities, DocumentFilter[] filter, RazorCohostRequestContext requestContext)
    {
        if (clientCapabilities.TextDocument?.Definition?.DynamicRegistration == true)
        {
            return new Registration
            {
                Method = Methods.TextDocumentDefinitionName,
                RegisterOptions = new DefinitionOptions()
            };
        }

        return null;
    }

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(TextDocumentPositionParams request)
        => request.TextDocument.ToRazorTextDocumentIdentifier();

    protected override Task<RoslynLocation[]?> HandleRequestAsync(TextDocumentPositionParams request, RazorCohostRequestContext context, CancellationToken cancellationToken)
        => HandleRequestAsync(
            context.TextDocument.AssumeNotNull(),
            RoslynLspFactory.CreatePosition(request.Position.ToLinePosition()),
            cancellationToken);

    private async Task<RoslynLocation[]?> HandleRequestAsync(TextDocument razorDocument, RoslynPosition linePosition, CancellationToken cancellationToken)
    {
        var result =  await _remoteServiceInvoker
            .TryInvokeAsync<IRemoteGoToDefinitionService, RoslynLocation[]?>(
                razorDocument.Project.Solution,
                (service, solutionInfo, cancellationToken) =>
                    service.GetDefinitionAsync(solutionInfo, razorDocument.Id, linePosition, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);

        return result;
    }
}
