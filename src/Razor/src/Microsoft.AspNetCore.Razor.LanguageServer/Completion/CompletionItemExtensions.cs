// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal static class CompletionItemExtensions
    {
        private const string ResultIdKey = "_resultId";

        public static CompletionItem CreateWithCompletionListResultId(this CompletionItem completionItem, long resultId)
        {
            if (completionItem is null)
            {
                throw new ArgumentNullException(nameof(completionItem));
            }

            var data = completionItem.Data ?? new JObject();
            data[ResultIdKey] = resultId;
            completionItem = completionItem with { Data = data };

            var result = completionItem.ToVSCompletionItem();
            return result;
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

        public static VSCompletionItem ToVSCompletionItem(this CompletionItem completion)
        {
            if (completion is null)
            {
                throw new ArgumentNullException(nameof(completion));
            }

            var vsCommitCharacters = GetVSCommitCharacters(completion).ToArray();

            return new VSCompletionItem
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
                VsCommitCharacters = vsCommitCharacters,
            };

            static IEnumerable<VSCommitCharacter> GetVSCommitCharacters(CompletionItem completion)
            {
                if (completion.CommitCharacters is null)
                {
                    yield break;
                }

                foreach (var commitCharacter in completion.CommitCharacters)
                {
                    yield return new VSCommitCharacter
                    {
                        Character = commitCharacter,
                        Insert = completion.InsertTextFormat != InsertTextFormat.Snippet,
                    };
                }
            }
        }
    }
}
