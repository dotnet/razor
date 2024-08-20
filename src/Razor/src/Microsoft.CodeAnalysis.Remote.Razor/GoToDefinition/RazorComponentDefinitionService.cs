// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.GoToDefinition;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Remote.Razor.GoToDefinition;

[Export(typeof(IRazorComponentDefinitionService)), Shared]
[method: ImportingConstructor]
internal sealed class RazorComponentDefinitionService(
    IRazorComponentSearchEngine componentSearchEngine,
    IDocumentMappingService documentMappingService,
    ILoggerFactory loggerFactory)
    : AbstractRazorComponentDefinitionService(componentSearchEngine, documentMappingService, loggerFactory.GetOrCreateLogger<RazorComponentDefinitionService>())
{
    protected override async ValueTask<SyntaxTree> GetCSharpSyntaxTreeAsync(IDocumentSnapshot documentSnapshot, CancellationToken cancellationToken)
    {
        Debug.Assert(documentSnapshot is RemoteDocumentSnapshot, "This method only works on document snapshots created in the OOP process");

        var remoteSnapshot = (RemoteDocumentSnapshot)documentSnapshot;
        var document = await remoteSnapshot.GetGeneratedDocumentAsync().ConfigureAwait(false);

        if (document.TryGetSyntaxTree(out var syntaxTree))
        {
            return syntaxTree;
        }

        var tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        return tree.AssumeNotNull();
    }
}
