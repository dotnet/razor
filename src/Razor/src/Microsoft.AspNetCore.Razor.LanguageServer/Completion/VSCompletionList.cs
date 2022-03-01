// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    /// <summary>
    /// A subclass of the LSP protocol <see cref="CompletionList"/> that contains extensions specific to Visual Studio.
    /// </summary>
    internal class VSCompletionList : CompletionList
    {
        protected VSCompletionList(CompletionList innerCompletionList) : this(innerCompletionList.Items, innerCompletionList.IsIncomplete)
        {
        }

        protected VSCompletionList(IEnumerable<CompletionItem> completionItems, bool isIncomplete) : base(completionItems, isIncomplete)
        {
        }

        /// <summary>
        /// Gets or sets the default <see cref="CompletionItem.CommitCharacters"/> used for completion items.
        /// </summary>
        [JsonProperty("_vs_commitCharacters")]
        public Container<VSCommitCharacter> CommitCharacters { get; set; }

        /// <summary>
        /// Gets or sets the default <see cref="CompletionItem.Data"/> used for completion items.
        /// </summary>
        [JsonProperty("_vs_data")]
        public object Data { get; set; }

        public static VSCompletionList Convert(CompletionList completionList, VSCompletionListCapability vsCompletionListCapability)
        {
            var vsCompletionList = new VSCompletionList(completionList);
            if (vsCompletionListCapability.CommitCharacters)
            {
                vsCompletionList = PromoteCommonCommitCharactersOntoList(vsCompletionList);
            }

            if (vsCompletionListCapability.Data)
            {
                vsCompletionList = PromotedDataOntoList(vsCompletionList);
            }

            return vsCompletionList;
        }

        private static VSCompletionList PromoteCommonCommitCharactersOntoList(VSCompletionList completionList)
        {
            var commitCharacterReferences = new Dictionary<object, int>();
            var highestUsedCount = 0;
            Container<VSCommitCharacter> mostUsedCommitCharacters = null;
            foreach (var completionItem in completionList.Items)
            {
                var commitCharacters = completionItem is VSCompletionItem vsCompletionItem
                    ? vsCompletionItem.VsCommitCharacters
                    : ToVSCommitCharacter(completionItem.CommitCharacters);
                if (commitCharacters is null)
                {
                    continue;
                }

                commitCharacterReferences.TryGetValue(commitCharacters, out var existingCount);
                existingCount++;

                if (existingCount > highestUsedCount)
                {
                    // Capture the most used commit character counts so we don't need to re-iterate the array later
                    mostUsedCommitCharacters = commitCharacters;
                    highestUsedCount = existingCount;
                }

                commitCharacterReferences[commitCharacters] = existingCount;
            }

            // Promote the most used commit characters onto the list and remove duplicates from child items.
            var promotedCompletionItems = new List<CompletionItem>();
            foreach (var completionItem in completionList.Items)
            {
                if (completionItem.CommitCharacters != null && SequenceEqual(completionItem.CommitCharacters, mostUsedCommitCharacters))
                {
                    var clearedCompletionItem = completionItem with { CommitCharacters = null };
                    promotedCompletionItems.Add(clearedCompletionItem);
                }

                if (completionItem is VSCompletionItem vsCompletionItem &&
                    vsCompletionItem.VsCommitCharacters != null &&
                    vsCompletionItem.VsCommitCharacters.Equals(mostUsedCommitCharacters))
                {
                    var clearedCompletionItem = vsCompletionItem with { VsCommitCharacters = null };
                    promotedCompletionItems.Add(clearedCompletionItem);
                }
                else
                {
                    promotedCompletionItems.Add(completionItem);
                }
            }

            var promotedCompletionList = new VSCompletionList(promotedCompletionItems, completionList.IsIncomplete)
            {
                CommitCharacters = mostUsedCommitCharacters,
                Data = completionList.Data,
            };
            return promotedCompletionList;

            static bool SequenceEqual(Container<string> a, Container<VSCommitCharacter> b)
            {
                if (a.Count() != b.Count())
                {
                    return false;
                }

                for (var i = 0; i < a.Count(); i++)
                {
                    var str = a.ElementAt(i);
                    var vs = b.ElementAt(i);

                    if (!vs.Equals(str))
                    {
                        return false;
                    }
                }

                return true;
            }

            static Container<VSCommitCharacter> ToVSCommitCharacter(Container<string> commitCharacters)
            {
                var result = new List<VSCommitCharacter>();
                foreach (var commitCharacter in commitCharacters)
                {
                    result.Add(new VSCommitCharacter
                    {
                        Character = commitCharacter,
                        Insert = true,
                    });
                }

                return result;
            }
        }

        private static VSCompletionList PromotedDataOntoList(VSCompletionList completionList)
        {
            // This piece makes a massive assumption that all completion items will have a resultId associated with them and their
            // data properties will all be the same. Therefore, we can inspect the first item and empty out the rest.
            var commonDataItem = completionList.FirstOrDefault();
            if (commonDataItem is null)
            {
                // No common data items, nothing to do
                return completionList;
            }

            var promotedCompletionItems = new List<CompletionItem>();
            foreach (var completionItem in completionList.Items)
            {
                var clearedCompletionItem = completionItem with { Data = null };
                promotedCompletionItems.Add(clearedCompletionItem);
            }

            var promotedCompletionList = new VSCompletionList(promotedCompletionItems, completionList.IsIncomplete)
            {
                CommitCharacters = completionList.CommitCharacters,
                Data = commonDataItem.Data,
            };
            return promotedCompletionList;
        }
    }
}
