﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.Razor.Snippets;

[Export(typeof(SnippetCompletionItemProvider))]
internal sealed class SnippetCompletionItemProvider
{
    [ImportingConstructor]
    public SnippetCompletionItemProvider(SnippetCache snippetCache)
    {
        SnippetCache = snippetCache;
    }

    public SnippetCache SnippetCache { get; }

    public void AddSnippetCompletions(
        RazorLanguageKind projectedKind,
        VSInternalCompletionInvokeKind invokeKind,
        string? triggerCharacter,
        ref PooledArrayBuilder<CompletionItem> builder)
    {
        // Temporary fix: snippets are broken in CSharp. We're investigating
        // but this is very disruptive. This quick fix unblocks things.
        // TODO: Add an option to enable this.
        if (projectedKind != RazorLanguageKind.Html)
        {
            return;
        }

        // Don't add snippets for deletion of a character
        if (invokeKind == VSInternalCompletionInvokeKind.Deletion)
        {
            return;
        }

        // Don't add snippets if the trigger characters contain whitespace
        if (triggerCharacter is not null && triggerCharacter.Contains(' '))
        {
            return;
        }

        var snippets = SnippetCache.GetSnippets(ConvertLanguageKind(projectedKind));
        if (snippets.IsDefaultOrEmpty)
        {
            return;
        }

        builder.AddRange(snippets
            .Select(s => new CompletionItem()
            {
                Label = s.Shortcut,
                Detail = s.Description,
                InsertTextFormat = InsertTextFormat.Snippet,
                InsertText = s.Shortcut,
                Data = s.CompletionData,
                Kind = CompletionItemKind.Snippet,
                CommitCharacters = []
            }));
    }

    private static SnippetLanguage ConvertLanguageKind(RazorLanguageKind languageKind)
        => languageKind switch
        {
            RazorLanguageKind.CSharp => SnippetLanguage.CSharp,
            RazorLanguageKind.Html => SnippetLanguage.Html,
            RazorLanguageKind.Razor => SnippetLanguage.Razor,
            _ => throw new InvalidOperationException($"Unexpected value {languageKind}")
        };
}
