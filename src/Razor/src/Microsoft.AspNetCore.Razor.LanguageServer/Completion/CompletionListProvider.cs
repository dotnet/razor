// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal class CompletionListProvider
    {
        private readonly RazorCompletionListProvider _razorCompletionListProvider;
        private readonly DelegatedCompletionListProvider _delegatedCompletionListProvider;

        public CompletionListProvider(RazorCompletionListProvider razorCompletionListProvider, DelegatedCompletionListProvider delegatedCompletionListProvider)
        {
            _razorCompletionListProvider = razorCompletionListProvider;
            _delegatedCompletionListProvider = delegatedCompletionListProvider;

            var allTriggerCharacters = razorCompletionListProvider.TriggerCharacters.Concat(delegatedCompletionListProvider.TriggerCharacters);
            var distinctTriggerCharacters = new HashSet<string>(allTriggerCharacters);
            AggregateTriggerCharacters = distinctTriggerCharacters.ToImmutableHashSet();
        }

        public ImmutableHashSet<string> AggregateTriggerCharacters { get; }

        public async Task<VSInternalCompletionList?> GetCompletionListAsync(
            int absoluteIndex,
            VSInternalCompletionContext completionContext,
            DocumentContext documentContext,
            VSInternalClientCapabilities clientCapabilities,
            CancellationToken cancellationToken)
        {
            // First we delegate to get completion items from the individual language server
            var delegatedCompletionList = await GetCompletionListAsync(_delegatedCompletionListProvider, absoluteIndex, completionContext, documentContext, clientCapabilities, cancellationToken).ConfigureAwait(false);

            // Now we get the Razor completion list, using information from the actual language server if necessary
            var razorCompletionList = await GetCompletionListAsync(_razorCompletionListProvider, absoluteIndex, completionContext, documentContext, clientCapabilities, cancellationToken).ConfigureAwait(false);

            var finalCompletionList = CompletionListMerger.Merge(razorCompletionList, delegatedCompletionList);

            return finalCompletionList;
        }

        private Task<VSInternalCompletionList?> GetCompletionListAsync(ICompletionListProvider completionListProvider, int absoluteIndex, VSInternalCompletionContext completionContext, DocumentContext documentContext, VSInternalClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            if (completionContext.TriggerKind == CompletionTriggerKind.TriggerCharacter &&
                    completionContext.TriggerCharacter is not null &&
                    !completionListProvider.TriggerCharacters.Contains(completionContext.TriggerCharacter))
            {
                // Trigger character doesn't apply
                return Task.FromResult<VSInternalCompletionList?>(null);
            }

            return completionListProvider.GetCompletionListAsync(absoluteIndex, completionContext, documentContext, clientCapabilities, cancellationToken);
        }
    }
}
