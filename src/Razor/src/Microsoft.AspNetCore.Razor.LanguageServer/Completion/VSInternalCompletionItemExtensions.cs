﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion;

internal static class VSInternalCompletionItemExtensions
{
    private const string ResultIdKey = "_resultId";

    public static bool TryGetCompletionListResultIds(this VSInternalCompletionItem completion, [NotNullWhen(true)] out IReadOnlyList<int>? resultIds)
    {
        if (completion is null)
        {
            throw new ArgumentNullException(nameof(completion));
        }

        if (!CompletionListMerger.TrySplit(completion.Data, out var splitData))
        {
            resultIds = null;
            return false;
        }

        var ids = new List<int>();
        for (var i = 0; i < splitData.Count; i++)
        {
            var data = splitData[i];
            if (data.ContainsKey(ResultIdKey))
            {
                var resultId = data[ResultIdKey]?.ToObject<int>();
                if (resultId is not null)
                {
                    ids.Add(resultId.Value);
                }
            }
        }

        if (ids.Count > 0)
        {
            resultIds = ids;
            return true;
        }

        resultIds = null;
        return false;
    }

    public static void UseCommitCharactersFrom(
        this VSInternalCompletionItem completionItem,
        RazorCompletionItem razorCompletionItem,
        VSInternalClientCapabilities clientCapabilities)
    {
        if (razorCompletionItem.CommitCharacters is null || razorCompletionItem.CommitCharacters.Count == 0)
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
