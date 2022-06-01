// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation
{
    internal class DelegatedCompletionItemResolver : CompletionItemResolver
    {
        private readonly ClientNotifierServiceBase _languageServer;

        public DelegatedCompletionItemResolver(ClientNotifierServiceBase languageServer)
        {
            _languageServer = languageServer;
        }

        public override async Task<VSInternalCompletionItem?> ResolveAsync(
            VSInternalCompletionItem item,
            VSInternalCompletionList containingCompletionlist,
            object? originalRequestContext,
            VSInternalClientCapabilities? clientCapabilities,
            CancellationToken cancellationToken)
        {
            if (originalRequestContext is not DelegatedCompletionResolutionContext resolutionContext)
            {
                // Can't recognize the original request context, bail.
                return null;
            }

            var labelQuery = item.Label;
            var associatedDelegatedCompletion = containingCompletionlist.Items.FirstOrDefault(completion => string.Equals(labelQuery, completion.Label, StringComparison.Ordinal));
            if (associatedDelegatedCompletion is null)
            {
                return null;
            }

            item.Data = associatedDelegatedCompletion.Data ?? resolutionContext.OriginalCompletionListData;

            var delegatedParams = resolutionContext.OriginalRequestParams;
            var delegatedResolveParams = new DelegatedCompletionItemResolveParams(
                delegatedParams.HostDocument,
                item,
                delegatedParams.ProjectedKind);
            var delegatedRequest = await _languageServer.SendRequestAsync(LanguageServerConstants.RazorCompletionResolveEndpointName, delegatedResolveParams).ConfigureAwait(false);
            var resolvedCompletionItem = await delegatedRequest.Returning<VSInternalCompletionItem?>(cancellationToken).ConfigureAwait(false);
            return resolvedCompletionItem;
        }
    }
}
