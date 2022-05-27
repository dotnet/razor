// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal class AggregateCompletionListProvider
    {
        private readonly IReadOnlyList<CompletionListProvider> _completionListProviders;
        private readonly ILogger<AggregateCompletionListProvider> _logger;

        public AggregateCompletionListProvider(
            IEnumerable<CompletionListProvider> completionListProviders,
            ILoggerFactory loggerFactory)
        {
            _completionListProviders = completionListProviders.ToArray();
            _logger = loggerFactory.CreateLogger<AggregateCompletionListProvider>();

            var allTriggerCharacters = _completionListProviders.SelectMany(provider => provider.TriggerCharacters);
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
            var completionListTasks = new List<Task<VSInternalCompletionList?>>(_completionListProviders.Count);
            foreach (var completionListProvider in _completionListProviders)
            {
                if (completionContext.TriggerKind == CompletionTriggerKind.TriggerCharacter &&
                    completionContext.TriggerCharacter is not null &&
                    !completionListProvider.TriggerCharacters.Contains(completionContext.TriggerCharacter))
                {
                    // Trigger character doesn't apply
                    continue;
                }

                try
                {
                    var task = completionListProvider.GetCompletionListAsync(absoluteIndex, completionContext, documentContext, clientCapabilities, cancellationToken);
                    completionListTasks.Add(task);
                }
                catch (Exception ex) when (ex is not TaskCanceledException)
                {
                    _logger.LogError(ex, "Resolving completions failed synchronously unexpectedly.");
                }
            }

            var completionLists = new Queue<VSInternalCompletionList>();
            foreach (var completionListTask in completionListTasks)
            {
                try
                {
                    var completionList = await completionListTask.ConfigureAwait(false);
                    if (completionList is not null)
                    {
                        completionLists.Enqueue(completionList);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Resolving completions failed unexpectedly.");
                }
            }

            if (completionLists.Count == 0)
            {
                return null;
            }

            var finalCompletionList = completionLists.Dequeue();
            while (completionLists.Count > 0)
            {
                var nextCompletionList = completionLists.Dequeue();
                finalCompletionList = CompletionListMerger.Merge(finalCompletionList, nextCompletionList);
            }

            return finalCompletionList;
        }
    }
}
