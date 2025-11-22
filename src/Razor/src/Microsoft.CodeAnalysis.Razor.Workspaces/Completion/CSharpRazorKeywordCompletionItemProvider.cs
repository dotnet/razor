// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Microsoft.CodeAnalysis.Razor.Completion;

internal class CSharpRazorKeywordCompletionItemProvider : IRazorCompletionItemProvider
{
    internal static readonly ImmutableArray<RazorCommitCharacter> KeywordCommitCharacters = RazorCommitCharacter.CreateArray([" "]);

    // internal for testing
    // Do not forget to update both insert and display text !important
    internal static readonly ImmutableArray<string> CSharpRazorKeywords =
    [
        "do", "for", "foreach", "if", "lock", "switch", "try", "while"
    ];

    public ImmutableArray<RazorCompletionItem> GetCompletionItems(RazorCompletionContext context)
    {
        // C# Razor keywords are valid in the same locations as directives
        return DirectiveCompletionItemProvider.ShouldProvideCompletions(context)
            ? GetCSharpRazorKeywordCompletionItems()
            : [];
    }

    // Internal for testing
    internal static ImmutableArray<RazorCompletionItem> GetCSharpRazorKeywordCompletionItems()
    {
        var completionItems = new RazorCompletionItem[CSharpRazorKeywords.Length];

        for (var i = 0; i < CSharpRazorKeywords.Length; i++)
        {
            var keyword = CSharpRazorKeywords[i];

            var snippetCompletionItem = RazorCompletionItem.CreateKeyword(
                displayText: keyword,
                insertText: keyword,
                KeywordCommitCharacters);

            completionItems[i] = snippetCompletionItem;
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(completionItems);
    }
}
