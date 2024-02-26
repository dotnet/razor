// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation;

/// <summary>
/// Modifies delegated snippet completion items
/// </summary>
/// <remarks>
/// At the moment primarily used to modify C# "using" snippet to "using statement" snippet
/// in order to disambiguate it from Razor "using directive" snippet
/// </remarks>
internal class SnippetResponseRewriter : DelegatedCompletionResponseRewriter
{
    private static readonly FrozenDictionary<string, string> s_snippetToLabel = new Dictionary<string, string>()
    {
        ["using"] = $"using {SR.Statement}"
    }
    .ToFrozenDictionary();

    public override int Order => ExecutionBehaviorOrder.ChangesCompletionItems;

    public override Task<VSInternalCompletionList> RewriteAsync(VSInternalCompletionList completionList, int hostDocumentIndex, DocumentContext hostDocumentContext, DelegatedCompletionParams delegatedParameters, CancellationToken cancellationToken)
    {
        foreach (var item in completionList.Items)
        {
            if (item.Kind == CompletionItemKind.Snippet)
            {
                if (item.Label is null)
                {
                    continue;
                }

                if (s_snippetToLabel.TryGetValue(item.Label, out var newLabel))
                {
                    item.Label = newLabel;
                }
            }
        }

        return Task.FromResult(completionList);
    }
}
