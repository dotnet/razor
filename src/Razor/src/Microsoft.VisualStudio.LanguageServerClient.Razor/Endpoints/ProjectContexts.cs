// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using StreamJsonRpc;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

internal partial class RazorCustomMessageTarget
{
    [JsonRpcMethod(CustomMessageNames.RazorProjectContextsEndpoint, UseSingleObjectParameterDeserialization = true)]
    public Task<VSProjectContextList?> ProjectContextsAsync(DelegatedProjectContextsParams request, CancellationToken _)
    {
        // Previously we would have asked Roslyn for their ProjectContexts, so we can make sure we pass a ProjectContext they understand
        // to them when we ask them for things. Now that we generate unique file names for generated files, we no longer need to do that
        // as the generated file will only be in one project, so we can just use our own ProjectContexts. This makes other things much
        // easier because we're not trying to understand Roslyn concepts.

        if (!_documentManager.TryGetDocument(request.Uri, out var lspDocument) ||
            !lspDocument.TryGetAllVirtualDocuments<CSharpVirtualDocumentSnapshot>(out var virtualDocuments))
        {
            return Task.FromResult<VSProjectContextList?>(null);
        }

        using var projectContexts = new PooledArrayBuilder<VSProjectContext>(capacity: virtualDocuments.Length);

        foreach (var doc in virtualDocuments)
        {
            // We can sometimes get requests before we have project information in our virtual documents, which
            // means a null key, which would cause deserialization issues.
            // TODO: Make this say "Misc Files"?
            if (doc.ProjectKey.Id is null)
            {
                return Task.FromResult<VSProjectContextList?>(null);
            }

            projectContexts.Add(new VSProjectContext
            {
                Id = doc.ProjectKey.Id,
                Kind = VSProjectKind.CSharp,
                Label = doc.ProjectKey.Id
            });
        }

        var result = new VSProjectContextList
        {
            DefaultIndex = 0,
            ProjectContexts = projectContexts.ToArray(),
        };
        return Task.FromResult<VSProjectContextList?>(result);
    }
}
