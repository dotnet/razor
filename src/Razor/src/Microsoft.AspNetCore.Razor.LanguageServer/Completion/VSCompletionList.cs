// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal class VSCompletionList : CompletionList
    {
        protected VSCompletionList(CompletionList innerCompletionList) : base (innerCompletionList.Items, innerCompletionList.IsIncomplete)
        {
        }

        public Container<string> CommitCharacters { get; set; }

        public object Data { get; set; }

        public static VSCompletionList Convert(CompletionList completionList, VSCompletionListCapability vsCompletionListCapability)
        {
            var vsCompletionList = new VSCompletionList(completionList);
            if (vsCompletionListCapability.CommitCharacters)
            {
                PromoteCommonCommitCharactersOntoList(vsCompletionList);
            }

            if (vsCompletionListCapability.Data)
            {
                PromotedDataOntoList(vsCompletionList);
            }

            return vsCompletionList;
        }

        private static void PromoteCommonCommitCharactersOntoList(VSCompletionList completionList)
        {
            var commitCharacterReferences = new Dictionary<object, int>();
            var highestUsedCount = 0;
            Container<string> mostUsedCommitCharacters = null;
            foreach (var completionItem in completionList.Items)
            {
                var commitCharacters = completionItem.CommitCharacters;
                if (commitCharacters == null)
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

            // Promoted the most used commit characters onto the list and then remove these from child items.
            completionList.CommitCharacters = mostUsedCommitCharacters;
            foreach (var completionItem in completionList.Items)
            {
                if (completionItem.CommitCharacters == completionList.CommitCharacters)
                {
                    completionItem.CommitCharacters = null;
                }
            }
        }

        private static void PromotedDataOntoList(VSCompletionList completionList)
        {
            // This piece makes a massive assumption that all completion items will have a resultId associated with them and their
            // data properties will all be the same. Therefore, we can inspect the first item and empty out the rest.
            var commonDataItem = completionList.FirstOrDefault();
            if (commonDataItem == null)
            {
                // Empty list
                return;
            }

            completionList.Data = commonDataItem.Data;
            foreach (var completionItem in completionList.Items)
            {
                completionItem.Data = null;
            }
        }
    }
}
