// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal class RazorCompletionResolveEndpoint : IVSCompletionResolveEndpoint
    {
        private readonly AggregateCompletionItemResolver _completionItemResolver;
        private readonly CompletionListCache _completionListCache;
        private VSInternalClientCapabilities? _clientCapabilities;

        public RazorCompletionResolveEndpoint(
            AggregateCompletionItemResolver completionItemResolver,
            CompletionListCache completionListCache)
        {
            _completionItemResolver = completionItemResolver;
            _completionListCache = completionListCache;
        }

        public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            _clientCapabilities = clientCapabilities;
            return null;
        }

        public async Task<VSInternalCompletionItem> Handle(VSCompletionItemBridge completionItemBridge, CancellationToken cancellationToken)
        {
            VSInternalCompletionItem completionItem = completionItemBridge;

            if (!completionItem.TryGetCompletionListResultIds(out var resultIds))
            {
                // Unable to lookup completion item result info
                return completionItem;
            }

            object? originalRequestContext = null;
            VSInternalCompletionList? containingCompletionlist = null;
            foreach (var resultId in resultIds)
            {
                if (!_completionListCache.TryGet(resultId, out var cacheEntry))
                {
                    continue;
                }

                // See if this is the right completion list for this corresponding completion item. We cross-check this based on label only given that
                // is what users interact with.
                if (cacheEntry.CompletionList.Items.Any(completion => string.Equals(completionItem.Label, completion.Label, StringComparison.Ordinal)))
                {
                    originalRequestContext = cacheEntry.Context;
                    containingCompletionlist = cacheEntry.CompletionList;
                    break;
                }
            }

            if (containingCompletionlist is null)
            {
                // Couldn't find an assocaited completion list
                return completionItem;
            }

            var resolvedCompletionItem = await _completionItemResolver.ResolveAsync(
                completionItem,
                containingCompletionlist,
                originalRequestContext,
                _clientCapabilities,
                cancellationToken).ConfigureAwait(false);
            resolvedCompletionItem ??= completionItem;

            return resolvedCompletionItem;
        }
    }
}
