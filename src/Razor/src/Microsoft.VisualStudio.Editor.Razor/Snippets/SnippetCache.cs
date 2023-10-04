// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.PooledObjects;
using static Microsoft.VisualStudio.Editor.Razor.Snippets.XmlSnippetParser;

namespace Microsoft.VisualStudio.Editor.Razor.Snippets;

[Export(typeof(SnippetCache)), Shared]
internal class SnippetCache
{
    private Dictionary<SnippetLanguage, ImmutableArray<SnippetInfo>> _snippetCache = new();
    private ReadWriterLocker _lock = new();

    internal void Update(SnippetLanguage language, ImmutableArray<SnippetInfo> snippets)
    {
        using (_lock.EnterWriteLock())
        {
            _snippetCache[language] = snippets;
        }
    }

    public ImmutableArray<SnippetInfo> GetSnippets(SnippetLanguage language)
    {
        using var _ = _lock.EnterReadLock();
        return _snippetCache[language];
    }

    internal string? TryResolveSnippetString(SnippetCompletionData completionData)
    {
        // Search through all the snippets to find a match
        var snippets = _snippetCache.Values;
        var snippet = snippets.SelectMany(v => v).FirstOrDefault(completionData.Matches);
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

            functionSnippetBuilder.Append(part.GetInsertionString());
        }

        return functionSnippetBuilder.ToString();
    }
}
