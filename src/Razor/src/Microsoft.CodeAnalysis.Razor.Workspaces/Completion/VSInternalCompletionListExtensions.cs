﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Razor;

namespace Microsoft.CodeAnalysis.Razor.Completion;

internal static class VSInternalCompletionListExtensions
{
    public static void SetResultId(
        this RazorVSInternalCompletionList completionList,
        int resultId,
        VSInternalClientCapabilities clientCapabilities)
    {
        var data = JsonSerializer.SerializeToElement(new JsonObject()
        {
            [VSInternalCompletionItemExtensions.ResultIdKey] = resultId,
        });

        if (clientCapabilities.SupportsAnyCompletionListData())
        {
            // Ensure there is data at the completion list level, but only if ItemDefaults isn't set by the delegated server,
            // or if they've set both.
            var hasDefaultsData = completionList.ItemDefaults?.Data is not null;
            if (completionList.Data is not null || !hasDefaultsData)
            {
                completionList.Data = CompletionListMerger.MergeData(data, completionList.Data);
            }

            if (hasDefaultsData)
            {
                completionList.ItemDefaults.AssumeNotNull().Data = CompletionListMerger.MergeData(data, completionList.ItemDefaults.Data);
            }

            // Merge data for items that won't inherit the default
            foreach (var completionItem in completionList.Items.Where(c => c.Data is not null))
            {
                completionItem.Data = CompletionListMerger.MergeData(data, completionItem.Data);
            }
        }
        else
        {
            // No CompletionList.Data support
            foreach (var completionItem in completionList.Items)
            {
                completionItem.Data = CompletionListMerger.MergeData(data, completionItem.Data);
            }
        }
    }
}
