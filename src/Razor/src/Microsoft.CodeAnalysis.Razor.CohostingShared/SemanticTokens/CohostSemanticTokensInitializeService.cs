// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

[Export(typeof(IRazorCohostStartupService))]
[method: ImportingConstructor]
internal class CohostSemanticTokensInitializeService(
    LanguageServerFeatureOptions languageServerFeatureOptions) : IRazorCohostStartupService
{
    private readonly LanguageServerFeatureOptions _options = languageServerFeatureOptions;

    public int Order => WellKnownStartupOrder.Default;

    public Task StartupAsync(VSInternalClientCapabilities clientCapabilities, RazorCohostRequestContext requestContext, CancellationToken cancellationToken)
    {
        if (_options.UseRazorCohostServer)
        {
            // Normally, Roslyn triggers semantic tokens refreshes for additional document changes, but not for Razor documents.
            // In cohosting, we want it to consider Razor documents too.
            CodeAnalysis.ExternalAccess.Razor.Cohost.Handlers.SemanticTokensRange.RegisterRefresh(requestContext);
        }

        return Task.CompletedTask;
    }
}
