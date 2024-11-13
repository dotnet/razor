// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using RoslynVSInternalCompletionItem = Roslyn.LanguageServer.Protocol.VSInternalCompletionItem;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(Methods.TextDocumentCompletionResolveName)]
[Export(typeof(IDynamicRegistrationProvider))]
[ExportCohostStatelessLspService(typeof(CohostDocumentCompletionResolveEndpoint))]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostDocumentCompletionResolveEndpoint : AbstractRazorCohostRequestHandler<RoslynVSInternalCompletionItem, RoslynVSInternalCompletionItem>, IDynamicRegistrationProvider
{
    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    public ImmutableArray<Registration> GetRegistrations(VSInternalClientCapabilities clientCapabilities, RazorCohostRequestContext requestContext)
    {
        if (clientCapabilities.TextDocument?.Completion?.DynamicRegistration is true)
        {
            return [new Registration()
            {
                Method = Methods.TextDocumentCompletionResolveName
            }];
        }

        return [];
    }

    protected override Task<RoslynVSInternalCompletionItem> HandleRequestAsync(RoslynVSInternalCompletionItem request, RazorCohostRequestContext context, CancellationToken cancellationToken)
        => HandleRequestAsync(request);

    private Task<RoslynVSInternalCompletionItem> HandleRequestAsync(RoslynVSInternalCompletionItem request)
    {
        return Task.FromResult(request);
    }
}
