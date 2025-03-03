// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion;

[RazorLanguageServerEndpoint(Methods.TextDocumentCompletionResolveName)]
internal class RazorCompletionResolveEndpoint
    : IRazorDocumentlessRequestHandler<VSInternalCompletionItem, VSInternalCompletionItem>,
      ICapabilitiesProvider
{
    private readonly AggregateCompletionItemResolver _completionItemResolver;
    private readonly CompletionListCache _completionListCache;
    private readonly IProjectSnapshotManager _projectSnapshotManager;
    private VSInternalClientCapabilities? _clientCapabilities;

    public RazorCompletionResolveEndpoint(
        AggregateCompletionItemResolver completionItemResolver,
        CompletionListCache completionListCache,
        IProjectSnapshotManager projectSnapshotManager)
    {
        _completionItemResolver = completionItemResolver;
        _completionListCache = completionListCache;
        _projectSnapshotManager = projectSnapshotManager;
    }

    public bool MutatesSolutionState => false;

    public void ApplyCapabilities(VSInternalServerCapabilities serverCapabilities, VSInternalClientCapabilities clientCapabilities)
    {
        _clientCapabilities = clientCapabilities;
    }

    public async Task<VSInternalCompletionItem> HandleRequestAsync(VSInternalCompletionItem completionItem, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        (var originalRequestContext, var containingCompletionList) = _completionListCache.GetOriginalData(completionItem);

        if (containingCompletionList is null)
        {
            // Couldn't find an associated completion list
            return completionItem;
        }

        var resolvedCompletionItem = await _completionItemResolver.ResolveAsync(
            completionItem,
            containingCompletionList,
            originalRequestContext,
            _clientCapabilities,
            _projectSnapshotManager.GetQueryOperations(),
            cancellationToken).ConfigureAwait(false);
        resolvedCompletionItem ??= completionItem;

        return resolvedCompletionItem;
    }
}
