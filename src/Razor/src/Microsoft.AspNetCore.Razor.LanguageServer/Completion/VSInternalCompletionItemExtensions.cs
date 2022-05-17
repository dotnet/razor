// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal static class VSInternalCompletionItemExtensions
    {
        private const string ResultIdKey = "_resultId";

        public static VSInternalCompletionItem CreateWithCompletionListResultId(
            this VSInternalCompletionItem completionItem,
            long resultId)
        {
            if (completionItem is null)
            {
                throw new ArgumentNullException(nameof(completionItem));
            }

            var data = completionItem.Data as JObject ?? new JObject();
            data[ResultIdKey] = resultId;
            completionItem.Data = data;

            return completionItem;
        }

        public static bool TryGetCompletionListResultId(this VSInternalCompletionItem completion, [NotNullWhen(true)] out int? resultId)
        {
            if (completion is null)
            {
                throw new ArgumentNullException(nameof(completion));
            }

            if (completion.Data is JObject data && data.ContainsKey(ResultIdKey))
            {
                resultId = data[ResultIdKey]?.ToObject<int>();
                return resultId is not null;
            }

            resultId = default;
            return false;
        }

        public static void UseCommitCharactersFrom(
            this VSInternalCompletionItem completionItem,
            RazorCompletionItem razorCompletionItem,
            VSInternalClientCapabilities clientCapabilities)
        {
            if (razorCompletionItem.CommitCharacters == null || razorCompletionItem.CommitCharacters.Count == 0)
            {
                return;
            }

            var supportsVSExtensions = clientCapabilities?.SupportsVisualStudioExtensions ?? false;
            if (supportsVSExtensions)
            {
                var vsCommitCharacters = razorCompletionItem
                    .CommitCharacters
                    .Select(c => new VSInternalCommitCharacter() { Character = c.Character, Insert = c.Insert })
                    .ToArray();
                completionItem.VsCommitCharacters = vsCommitCharacters;
            }
            else
            {
                completionItem.CommitCharacters = razorCompletionItem
                    .CommitCharacters
                    .Select(c => c.Character)
                    .ToArray();
            }
        }
    }
}
