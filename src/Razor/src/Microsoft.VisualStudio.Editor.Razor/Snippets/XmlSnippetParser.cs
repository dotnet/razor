// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Composition;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Microsoft.VisualStudio.Editor.Razor.Snippets;

/// <summary>
/// Server instance agnostic snippet parser and cache.
/// This can be re-used across LSP servers as we're just storing an
/// internal representation of an XML snippet.
/// </summary>
[Export(typeof(XmlSnippetParser)), Shared]
internal partial class XmlSnippetParser
{
    private readonly ILogger? _logger;

    [ImportingConstructor]
    public XmlSnippetParser(ILoggerFactory? loggerFactory)
    {
        _logger = loggerFactory?.CreateLogger<XmlSnippetParser>();
    }

    /// <summary>
    /// Cache to hold onto the parsed XML for a particular snippet.
    /// </summary>
    private readonly ConcurrentDictionary<string, ParsedXmlSnippet?> _parsedSnippetsCache = new();

    internal ParsedXmlSnippet? GetParsedXmlSnippet(SnippetInfo matchingSnippetInfo)
    {
        if (_parsedSnippetsCache.TryGetValue(matchingSnippetInfo.Title, out var cachedSnippet))
        {
            if (cachedSnippet == null)
            {
                _logger?.LogWarning($"Returning a null cached snippet for {matchingSnippetInfo.Title}");
            }

            return cachedSnippet;
        }

        ParsedXmlSnippet? parsedSnippet = null;
        try
        {
            _logger?.LogInformation($"Reading snippet for {matchingSnippetInfo.Title} with path {matchingSnippetInfo.Path}");
            parsedSnippet = GetAndParseSnippetFromFile(matchingSnippetInfo);
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, $"Got exception parsing xml snippet {matchingSnippetInfo.Title} from file {matchingSnippetInfo.Path}");
        }

        // Add the snippet to the cache regardless of if we succeeded in parsing it.
        // We're not likely to succeed in parsing on a second try if we failed initially, so we cache it to avoid repeatedly failing.
        _parsedSnippetsCache.TryAdd(matchingSnippetInfo.Title, parsedSnippet);
        return parsedSnippet;
    }

    private static ParsedXmlSnippet GetAndParseSnippetFromFile(SnippetInfo snippetInfo)
    {
        // Read the XML file to get the snippet and snippet metadata.
        var matchingSnippet = RetrieveSnippetXmlFromFile(snippetInfo);

        Debug.Assert(matchingSnippet.IsExpansionSnippet(), "Only expansion snippets are supported");

        if (!matchingSnippet.IsExpansionSnippet())
        {
            throw new InvalidOperationException();
        }

        var expansion = new ExpansionTemplate(matchingSnippet);

        // Parse the snippet XML into snippet parts we can cache.
        var parsedSnippet = expansion.Parse();
        return parsedSnippet;
    }

    private static CodeSnippet RetrieveSnippetXmlFromFile(SnippetInfo snippetInfo)
    {
        var path = snippetInfo.Path;
        if (path == null)
        {
            throw new ArgumentException($"Missing file path for snippet {snippetInfo.Title}");
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Snippet {snippetInfo.Title} has an invalid file path: {snippetInfo.Path}");
        }

        // Load the xml for the snippet from disk.
        // Any exceptions thrown here we allow to bubble up and let the queue log it.
        var snippet = CodeSnippet.ReadSnippetFromFile(snippetInfo.Path, snippetInfo.Title);
        return snippet;
    }

    internal TestAccessor GetTestAccessor() => new TestAccessor(this);

    internal readonly struct TestAccessor
    {
        private readonly XmlSnippetParser _snippetParser;
        public TestAccessor(XmlSnippetParser snippetParser)
        {
            _snippetParser = snippetParser;
        }

        public int GetCachedSnippetsCount() => _snippetParser._parsedSnippetsCache.Count;

        public ParsedXmlSnippet GetCachedSnippet(string snippet) => _snippetParser._parsedSnippetsCache[snippet]!;
    }
}
