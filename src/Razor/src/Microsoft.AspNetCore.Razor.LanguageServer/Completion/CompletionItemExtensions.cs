// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal static class CompletionItemExtensions
    {
        private const string ResultIdKey = "_resultId";

        public static CompletionItem CreateWithCompletionListResultId(
            this CompletionItem completionItem,
            long resultId,
            PlatformAgnosticCompletionCapability? completionCapability)
        {
            if (completionItem is null)
            {
                throw new ArgumentNullException(nameof(completionItem));
            }

            var data = completionItem.Data ?? new JObject();
            data[ResultIdKey] = resultId;
            completionItem = completionItem with { Data = data };

            if (completionCapability is not null && completionCapability.VSCompletionList != null)
            {
                var result = completionItem.ToVSCompletionItem(completionCapability.VSCompletionList);
                return result;
            }
            else
            {
                return completionItem;
            }
        }

        public static bool TryGetCompletionListResultId(this CompletionItem completion, [NotNullWhen(true)] out int? resultId)
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

        public static VSCompletionItem ToVSCompletionItem(this CompletionItem completion, VSCompletionListCapability? vSCompletionListCapability)
        {
            if (completion is null)
            {
                throw new ArgumentNullException(nameof(completion));
            }

            var result = new VSCompletionItem
            {
                AdditionalTextEdits = completion.AdditionalTextEdits,
                Command = completion.Command,
                CommitCharacters = completion.CommitCharacters,
                Data = completion.Data,
                Deprecated = completion.Deprecated,
                Detail = completion.Detail,
                Documentation = completion.Documentation,
                FilterText = completion.FilterText,
                InsertText = completion.InsertText,
                InsertTextFormat = completion.InsertTextFormat,
                Kind = completion.Kind,
                Label = completion.Label,
                Preselect = completion.Preselect,
                SortText = completion.SortText,
                Tags = completion.Tags,
                TextEdit = completion.TextEdit,
            };

            if (vSCompletionListCapability is not null && vSCompletionListCapability.CommitCharacters)
            {
                var vsCommitCharacters = GetVSCommitCharacters(completion);
                result.VsCommitCharacters = vsCommitCharacters is null ? null : vsCommitCharacters;

                // If we're using VsCommitCharacters don't include CommitCharacters to avoid serializing them.
                if (result.VsCommitCharacters is not null)
                {
                    result = result with { CommitCharacters = null };
                }
            }

            return result;

            static Container<VSCommitCharacter>? GetVSCommitCharacters(CompletionItem completion)
            {
                if (completion.CommitCharacters is null)
                {
                    return null;
                }

                var result = new List<VSCommitCharacter>();
                foreach (var commitCharacter in completion.CommitCharacters)
                {
                    result.Add(new VSCommitCharacter
                    {
                        Character = commitCharacter,
                        Insert = completion.InsertTextFormat != InsertTextFormat.Snippet,
                    });
                }

                return result;
            }
        }
    }
}
