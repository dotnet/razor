// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Microsoft.AspNetCore.Razor.PooledObjects;
using static Microsoft.VisualStudio.Editor.Razor.Snippets.XmlSnippetParser;

namespace Microsoft.VisualStudio.Editor.Razor.Snippets;

[Export(typeof(SnippetCache)), Shared]
internal class SnippetCache
{
    private ImmutableArray<SnippetInfo> _snippets;

    internal void Update(ImmutableArray<SnippetInfo> snippets)
        => ImmutableInterlocked.InterlockedExchange(ref _snippets, snippets);

    public ImmutableArray<SnippetInfo> GetSnippets() => _snippets;

    internal string? TryResolveSnippetString(SnippetCompletionData completionData)
    {
        var snippets = GetSnippets();
        var snippet = snippets.FirstOrDefault(completionData.Matches);
        if (snippet is null)
        {
            return null;
        }

        var parsedSnippet = snippet.GetParsedXmlSnippet();
        if (parsedSnippet is null)
        {
            return null;
        }

        using var _ = StringBuilderPool.GetPooledObject(out var functionSnippetBuilder);

        foreach (var part in parsedSnippet.Parts)
        {
            if (part is SnippetShortcutPart shortcutPart)
            {
                shortcutPart.Shortcut = snippet.Shortcut;
            }

            functionSnippetBuilder.Append(part.ToString());
        }

        return functionSnippetBuilder.ToString();
    }
}
