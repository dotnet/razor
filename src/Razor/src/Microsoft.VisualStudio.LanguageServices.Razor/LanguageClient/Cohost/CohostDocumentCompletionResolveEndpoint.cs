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
internal sealed class CohostDocumentCompletionResolveEndpoint : AbstractRazorCohostDocumentRequestHandler<RoslynVSInternalCompletionItem, RoslynVSInternalCompletionItem>, IDynamicRegistrationProvider
{
    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    public ImmutableArray<Registration> GetRegistrations(VSInternalClientCapabilities clientCapabilities, RazorCohostRequestContext requestContext)
    {
        if (clientCapabilities.TextDocument?.Completion?.DynamicRegistration is true)
        {
            return [new Registration()
            {
                Method = Methods.TextDocumentCompletionResolveName,
                RegisterOptions = new CompletionRegistrationOptions()
                {
                    ResolveProvider = true
                }
            }];
        }

        return [];
    }

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(RoslynVSInternalCompletionItem request)
    {
        var completionResolveParams = CohostDocumentCompletionResolveParams.GetCohostDocumentCompletionResolveParams(request);
        return Roslyn.LanguageServer.Protocol.RoslynLspExtensions.ToRazorTextDocumentIdentifier(completionResolveParams.TextDocument);
    }

    protected override Task<RoslynVSInternalCompletionItem> HandleRequestAsync(RoslynVSInternalCompletionItem request, RazorCohostRequestContext context, CancellationToken cancellationToken)
        => HandleRequestAsync(request, cancellationToken);

    private Task<RoslynVSInternalCompletionItem> HandleRequestAsync(RoslynVSInternalCompletionItem request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(request);
        }

        // TODO: actual request processing code

        return Task.FromResult(request);
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CohostDocumentCompletionResolveEndpoint instance)
    {
        public Task<RoslynVSInternalCompletionItem> HandleRequestAsync(
            RoslynVSInternalCompletionItem request,
            CancellationToken cancellationToken)
                => instance.HandleRequestAsync(request, cancellationToken);
    }
}
