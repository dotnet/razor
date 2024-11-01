// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Remote;
using Roslyn.LanguageServer.Protocol;
using VSLSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(Methods.TextDocumentCodeActionName)]
[Export(typeof(IDynamicRegistrationProvider))]
[ExportCohostStatelessLspService(typeof(CohostCodeActionsEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal class CohostCodeActionsEndpoint(IRemoteServiceInvoker remoteServiceInvoker)
    : AbstractRazorCohostDocumentRequestHandler<CodeActionParams, CodeAction[]?>, IDynamicRegistrationProvider
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    public ImmutableArray<VSLSP.Registration> GetRegistrations(VSLSP.VSInternalClientCapabilities clientCapabilities, VSLSP.DocumentFilter[] filter, RazorCohostRequestContext requestContext)
    {
        if (clientCapabilities.TextDocument?.CodeAction?.DynamicRegistration == true)
        {
            return [new VSLSP.Registration
            {
                Method = Methods.TextDocumentCodeActionName,
                RegisterOptions = new VSLSP.CodeActionRegistrationOptions()
                {
                    DocumentSelector = filter
                }.EnableCodeActions()
            }];
        }

        return [];
    }

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(CodeActionParams request)
        => request.TextDocument.ToRazorTextDocumentIdentifier();

    protected override Task<CodeAction[]?> HandleRequestAsync(CodeActionParams request, RazorCohostRequestContext context, CancellationToken cancellationToken)
        => HandleRequestAsync(context.TextDocument.AssumeNotNull(), request, cancellationToken);

    private async Task<CodeAction[]?> HandleRequestAsync(TextDocument razorDocument, CodeActionParams request, CancellationToken cancellationToken)
    {
        // Normally we could remove the await here, but in this case it neatly converts from ValueTask to Task for us,
        // and more importantly this method is essentially a public API entry point (via LSP) so having it appear in
        // call stacks is desirable
        return await _remoteServiceInvoker.TryInvokeAsync<IRemoteCodeActionsService, CodeAction[]>(
            razorDocument.Project.Solution,
            (service, solutionInfo, cancellationToken) => service.GetCodeActionsAsync(solutionInfo, razorDocument.Id, request, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CohostCodeActionsEndpoint instance)
    {
        public Task<CodeAction[]?> HandleRequestAsync(TextDocument razorDocument, CodeActionParams request, CancellationToken cancellationToken)
            => instance.HandleRequestAsync(razorDocument, request, cancellationToken);
    }
}
