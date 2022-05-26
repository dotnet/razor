// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal static class CompletionListMerger
    {
        private static readonly object EmptyData = new object();

        public static VSInternalCompletionList? Merge(VSInternalCompletionList? completionListA, VSInternalCompletionList? completionListB)
        {
            if (completionListA is null)
            {
                return completionListB;
            }

            if (completionListB is null)
            {
                return completionListA;
            }

            EnsureMergeableCommitCharacters(completionListA, completionListB);
            EnsureMergeableData(completionListA, completionListB);

            var mergedIsIncomplete = completionListA.IsIncomplete || completionListB.IsIncomplete;
            var mergedItems = completionListA.Items.Concat(completionListB.Items).ToArray();
            var mergedData = MergeData(completionListA.Data, completionListB.Data);
            var mergedCommitCharacters = completionListA.CommitCharacters ?? completionListB.CommitCharacters;
            var mergedSuggestionMode = completionListA.SuggestionMode || completionListB.SuggestionMode;

            // We don't fully support merging continue characters currently. Razor doesn't currently use them so subsequent (i.e. delegated) completion lists always win.
            var mergedContinueWithCharacters = completionListA.ContinueCharacters ?? completionListB.ContinueCharacters;

            // We don't fully support merging edit ranges currently. Razor doesn't currently use them so subsequent (i.e. delegated) completion lists always win.
            var mergedItemDefaultsEditRange = completionListA.ItemDefaults?.EditRange ?? completionListB.ItemDefaults?.EditRange;

            var mergedCompletionList = new VSInternalCompletionList()
            {
                CommitCharacters = mergedCommitCharacters,
                Data = mergedData,
                IsIncomplete = mergedIsIncomplete,
                Items = mergedItems,
                SuggestionMode = mergedSuggestionMode,
                ContinueCharacters = mergedContinueWithCharacters,
                ItemDefaults = new CompletionListItemDefaults()
                {
                    EditRange = mergedItemDefaultsEditRange,
                }
            };

            return mergedCompletionList;
        }

        private static void EnsureMergeableData(VSInternalCompletionList completionListA, VSInternalCompletionList completionListB)
        {
            if (completionListA.Data != completionListB.Data &&
                completionListA.Data is null || completionListB.Data is null)
            {
                // One of the completion lists have data while the other does not, we need to ensure that any non-data centric items don't get incorrect data associated

                // The candidate completion list will be one where we populate empty data for any `null` specifying data given we'll be merging
                // two completion lists together we don't want incorrect data to be inherited down
                var candidateCompletionList = completionListA.Data is null ? completionListA : completionListB;
                for (var i = 0; i < candidateCompletionList.Items.Length; i++)
                {
                    var item = candidateCompletionList.Items[i];
                    if (item.Data is null)
                    {
                        item.Data = EmptyData;
                    }
                }
            }
        }

        private static void EnsureMergeableCommitCharacters(VSInternalCompletionList completionListA, VSInternalCompletionList completionListB)
        {
            var aInheritsCommitCharacters = completionListA.CommitCharacters is not null || completionListA.ItemDefaults?.CommitCharacters is not null;
            var bInheritsCommitCharacters = completionListB.CommitCharacters is not null || completionListB.ItemDefaults?.CommitCharacters is not null;
            if (aInheritsCommitCharacters && bInheritsCommitCharacters)
            {
                // Need to merge commit characters because both are trying to inherit

                var inheritableCommitCharacterCompletionsA = GetCompletionsThatDoNotSpecifyCommitCharacters(completionListA);
                var inheritableCommitCharacterCompletionsB = GetCompletionsThatDoNotSpecifyCommitCharacters(completionListB);
                IReadOnlyList<VSInternalCompletionItem>? completionItemsToStopInheriting;
                VSInternalCompletionList? completionListToStopInheriting;

                // Decide which completion list has more items that benefit from "inheriting" commit characters.
                if (inheritableCommitCharacterCompletionsA.Count >= inheritableCommitCharacterCompletionsB.Count)
                {
                    completionListToStopInheriting = completionListB;
                    completionItemsToStopInheriting = inheritableCommitCharacterCompletionsB;
                }
                else
                {
                    completionListToStopInheriting = completionListA;
                    completionItemsToStopInheriting = inheritableCommitCharacterCompletionsA;
                }

                for (var i = 0; i < completionItemsToStopInheriting.Count; i++)
                {
                    if (completionListToStopInheriting.CommitCharacters is not null)
                    {
                        completionItemsToStopInheriting[i].VsCommitCharacters = completionListToStopInheriting.CommitCharacters;
                    }
                    else if (completionListToStopInheriting.ItemDefaults?.CommitCharacters is not null)
                    {
                        completionItemsToStopInheriting[i].VsCommitCharacters = completionListToStopInheriting.ItemDefaults?.CommitCharacters;
                    }
                }

                completionListToStopInheriting.CommitCharacters = null;

                if (completionListToStopInheriting.ItemDefaults is not null)
                {
                    completionListToStopInheriting.ItemDefaults.CommitCharacters = null;
                }
            }
        }

        public static object? MergeData(object? data1, object? data2)
        {
            if (data1 is null)
            {
                return data2;
            }

            if (data2 is null)
            {
                return data1;
            }

            return new MergedCompletionListData(data1, data2);
        }

        private static IReadOnlyList<VSInternalCompletionItem> GetCompletionsThatDoNotSpecifyCommitCharacters(VSInternalCompletionList completionList)
        {
            var inheritableCompletions = new List<VSInternalCompletionItem>();

            for (var i = 0; i < completionList.Items.Length; i++)
            {
                var completionItem = completionList.Items[i] as VSInternalCompletionItem;
                if (completionItem is null ||
                    completionItem.CommitCharacters is not null ||
                    completionItem.VsCommitCharacters is not null)
                {
                    // Completion item wasn't the right type or already specifies commit characters
                    continue;
                }

                inheritableCompletions.Add(completionItem);
            }

            return inheritableCompletions;
        }

        private record MergedCompletionListData(object Data1, object Data2);
    }
}
