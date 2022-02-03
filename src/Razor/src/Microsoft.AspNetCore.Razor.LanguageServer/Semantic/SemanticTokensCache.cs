// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    /// <summary>
    /// Caches tokens on a per-line basis for documents based on semantic version.
    /// </summary>
    /// <remarks>
    /// The cache makes the assumption that the tokens passed in are complete for the given range.
    /// </remarks>
    internal sealed class SemanticTokensCache
    {
        private const int MaxSemanticVersionPerDoc = 4;

        // Nested cache mapping (URI -> (semanticVersion -> (line #s -> tokens on line)))
        private readonly MemoryCache<DocumentUri, MemoryCache<VersionStamp, Dictionary<int, IReadOnlyList<int>>>> _cache = new();

        /// <summary>
        /// Caches tokens on a per-line basis. If the given line has already been cached for the document
        /// and its semantic version, the line's tokens will be ignored.
        /// </summary>
        /// <param name="uri">The URI associated with the passed-in tokens.</param>
        /// <param name="semanticVersion">The document's semantic version associated with the passed-in tokens.</param>
        /// <param name="range">The range associated with the passed-in tokens.</param>
        /// <param name="tokens">The complete set of tokens for the passed-in range.</param>
        internal void CacheTokens(
            DocumentUri uri,
            VersionStamp semanticVersion,
            Range range,
            int[] tokens)
        {
            if (!_cache.TryGetValue(uri, out var documentCache))
            {
                documentCache = new MemoryCache<VersionStamp, Dictionary<int, IReadOnlyList<int>>>(sizeLimit: MaxSemanticVersionPerDoc);
                _cache.Set(uri, documentCache);
            }

            if (!documentCache.TryGetValue(semanticVersion, out var lineToTokensDict))
            {
                lineToTokensDict = new Dictionary<int, IReadOnlyList<int>>();
                documentCache.Set(semanticVersion, lineToTokensDict);
            }

            var absoluteLine = 0;

            // Cache the tokens associated with each line
            for (var tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex += 5)
            {
                absoluteLine += tokens[tokenIndex];

                // Don't cache tokens outside of the given range
                if (absoluteLine < range.Start.Line)
                {
                    continue;
                }

                if (absoluteLine >= range.End.Line)
                {
                    break;
                }

                if (lineToTokensDict.TryGetValue(absoluteLine, out _))
                {
                    // This line is already cached, so we can skip it
                    continue;
                }

                var lineTokens = new List<int>();

                // Cache the line's tokens
                while (tokenIndex < tokens.Length)
                {
                    lineTokens.Add(tokens[tokenIndex]);
                    lineTokens.Add(tokens[tokenIndex + 1]);
                    lineTokens.Add(tokens[tokenIndex + 2]);
                    lineTokens.Add(tokens[tokenIndex + 3]);
                    lineTokens.Add(tokens[tokenIndex + 4]);

                    if (tokenIndex + 5 >= tokens.Length || tokens[tokenIndex + 5] != 0)
                    {
                        // We've hit the next line or the end of the tokens
                        break;
                    }

                    tokenIndex += 5;
                }

                lineToTokensDict.Add(absoluteLine, lineTokens);
            }

            // Fill in the gaps for empty lines. When looking up tokens later, this helps us differentiate
            // between empty lines vs. lines we don't have cached tokens for
            for (var lineIndex = range.Start.Line; lineIndex < range.End.Line; lineIndex++)
            {
                if (!lineToTokensDict.TryGetValue(lineIndex, out _))
                {
                    lineToTokensDict.Add(lineIndex, Array.Empty<int>());
                }
            }
        }

        /// <summary>
        /// Attempts to retrieve the cached tokens for a given document/semanticVersion/range. If the cache does
        /// not have complete tokens for the range, it's possible to return only tokens associated with a portion
        /// of the range.
        /// </summary>
        /// <param name="uri">The URI associated with the cached tokens.</param>
        /// <param name="semanticVersion">The semantic version associated with the cached tokens.</param>
        /// <param name="requestedRange">The requested range for the desired tokens.</param>
        /// <param name="cachedTokens">If found, contains the cached range and cached tokens.</param>
        /// <returns></returns>
        internal bool TryGetCachedTokens(
            DocumentUri uri,
            VersionStamp semanticVersion,
            Range requestedRange,
            [NotNullWhen(true)] out (Range Range, List<int> Tokens)? cachedTokens)
        {
            if (!_cache.TryGetValue(uri, out var documentCache) ||
                !documentCache.TryGetValue(semanticVersion, out var lineToTokensDict))
            {
                // No cached results found
                cachedTokens = null;
                return false;
            }

            var tokens = new List<int>();

            // Keep track of where the cached range begins
            Position? cachedRangeStart = null;

            // Keep track of where the cached range ends
            var cachedRangeNumLines = 0;

            // Keeps track of the current empty line count (needed for calculating line offsets)
            var emptyLineCount = 0;

            // Retrieve the cached tokens associated with the passed-in range
            for (var line = requestedRange.Start.Line; line < requestedRange.End.Line; line++)
            {
                if (!lineToTokensDict.TryGetValue(line, out var lineTokens))
                {
                    if (tokens.Count != 0)
                    {
                        // We already have some sort of cached results, but we've now reached
                        // a gap in the cache which we have no tokens for.
                        // Since we only want to return a continuous range, we'll exit early.
                        break;
                    }

                    continue;
                }

                if (cachedRangeStart is null)
                {
                    cachedRangeStart = new Position { Line = line, Character = 0 };
                }

                cachedRangeNumLines++;

                // If a line's List is empty, it means the line has no tokens
                if (lineTokens.Count == 0)
                {
                    emptyLineCount++;
                    continue;
                }

                // We've found a non-empty cached line. Go through and process it.
                for (var lineTokenIndex = 0; lineTokenIndex < lineTokens.Count; lineTokenIndex++)
                {
                    // If we're at the start of the cached range, the line offset should be relative
                    // to the start of the document.
                    if (tokens.Count == 0)
                    {
                        tokens.Add(line);
                    }
                    // If we're not at start at the cached range but we're at the start of a line,
                    // the line offset should be relative to the end of the last cached line.
                    else if (lineTokenIndex == 0)
                    {
                        tokens.Add(1 + emptyLineCount);
                    }
                    else
                    {
                        tokens.Add(lineTokens[lineTokenIndex]);
                    }
                }

                emptyLineCount = 0;
            }

            if (tokens.Count == 0)
            {
                // We couldn't find any tokens associated with the passed-in range
                cachedTokens = null;
                return false;
            }

            Assumes.NotNull(cachedRangeStart);

            var endPosition = new Position { Line = cachedRangeStart.Line + cachedRangeNumLines, Character = 0 };
            var range = new Range { Start = cachedRangeStart, End = endPosition };

            cachedTokens = (range, tokens);
            return true;
        }
    }
}
