// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.Extensions.Logging;
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
        // A semantic token is composed of 5 integers: deltaLine, deltaStart, length, tokenType, tokenModifiers
        private const int IntegersPerToken = 5;

        // This number is picked on a gut-feeling of what we imagine might be performant
        private const int MaxSemanticVersionPerDoc = 4;

        private const int MaxDocumentLimit = 50;

        // Multiple cache requests or updates may be received concurrently. We need this lock to
        // ensure we aren't making concurrent modifications to the cache's underlying dictionary.
        private readonly object _dictLock = new();

        // Nested cache mapping (URI -> (semanticVersion -> (line #s -> tokens on line)))
        private readonly MemoryCache<DocumentUri, MemoryCache<VersionStamp, Dictionary<int, ImmutableArray<int>>>> _cache = new(MaxDocumentLimit);

        /// <summary>
        /// Caches tokens on a per-line basis. If the given line has already been cached for the document
        /// and its semantic version, the line's tokens will be ignored. Note caching will not occur if the
        /// range contains partial lines.
        /// </summary>
        /// <param name="uri">The URI associated with the passed-in tokens.</param>
        /// <param name="semanticVersion">The document's semantic version associated with the passed-in tokens.</param>
        /// <param name="range">The range associated with the passed-in tokens.</param>
        /// <param name="tokens">The complete set of tokens for the passed-in range.</param>
        public void CacheTokens(
            DocumentUri uri,
            VersionStamp semanticVersion,
            Range range,
            int[] tokens)
        {
            // For the most part, today the LSP client doesn't send us ranges that start or end midway through a line.
            // The exception to this is if the user is at the end of the document which does not have a new line.
            // Handling this caching case would require adding a significant amount of logic, so for now we'll skip
            // this scenario since it's most common in smaller files for which using the cache isn't as important.
            if (range.Start.Character != 0 || range.End.Character != 0)
            {
                return;
            }

            if (!_cache.TryGetValue(uri, out var documentCache))
            {
                documentCache = new MemoryCache<VersionStamp, Dictionary<int, ImmutableArray<int>>>(sizeLimit: MaxSemanticVersionPerDoc);
                _cache.Set(uri, documentCache);
            }

            if (!documentCache.TryGetValue(semanticVersion, out var lineToTokensDict))
            {
                lineToTokensDict = new Dictionary<int, ImmutableArray<int>>();
                documentCache.Set(semanticVersion, lineToTokensDict);
            }

            CacheTokensPerLine(range, tokens, lineToTokensDict);
            FillInEmptyLineGaps(range, lineToTokensDict);
        }

        /// <summary>
        /// Attempts to retrieve the cached tokens for a given document/semanticVersion/range. If the cache does
        /// not have complete tokens for the range, it's possible to return only tokens associated with a portion
        /// of the range.
        /// </summary>
        /// <param name="uri">The URI associated with the cached tokens.</param>
        /// <param name="semanticVersion">The semantic version associated with the cached tokens.</param>
        /// <param name="requestedRange">The requested range for the desired tokens.</param>
        /// <param name="logger">Optional logger to record outcome of cache lookup.</param>
        /// <param name="cachedTokens">If found, contains the cached range and cached tokens.</param>
        /// <returns>
        /// True if at least a partial match for the range was found. The 'Range' out var specifies the subset of
        /// the requested range that was found, and the 'Tokens' out var contains the tokens for that subset range.
        /// No results will be returned if the requested range contains partial lines.
        /// </returns>
        public bool TryGetCachedTokens(
            DocumentUri uri,
            VersionStamp semanticVersion,
            Range requestedRange,
            ILogger? logger,
            [NotNullWhen(true)] out (Range Range, ImmutableArray<int> Tokens)? cachedTokens)
        {
            // Don't return results for partial lines, we don't handle them currently due to
            // the need for extra logic and computation.
            if (requestedRange.Start.Character != 0 || requestedRange.End.Character != 0)
            {
                cachedTokens = null;
                return false;
            }

            if (!_cache.TryGetValue(uri, out var documentCache) ||
                !documentCache.TryGetValue(semanticVersion, out var lineToTokensDict))
            {
                logger?.LogInformation($"Cache missing for range: {requestedRange}");

                // No cached results found
                cachedTokens = null;
                return false;
            }

            var tokens = GetCachedTokens(requestedRange, lineToTokensDict, out var cachedRangeStart, out var numLinesInCachedRange);
            if (tokens.Length == 0)
            {
                logger?.LogInformation($"Cache lookup returned no results for range: {requestedRange}");

                // We couldn't find any tokens associated with the passed-in range
                cachedTokens = null;
                return false;
            }

            Assumes.NotNull(cachedRangeStart);

            // We can potentially return a partial range match if we can't find a full range match.
            var endPosition = new Position { Line = cachedRangeStart.Line + numLinesInCachedRange, Character = 0 };
            var range = new Range { Start = cachedRangeStart, End = endPosition };

            cachedTokens = (range, tokens);
            return true;
        }

        /// <summary>
        /// Given the tokens, place them in the lineToTokensDict according to the line they belong on.
        /// </summary>
        /// <param name="range">The range within the document that these tokens represent.</param>
        /// <param name="tokens">The tokens for the document within the given range.
        /// Line count begins at the begining of the document regardless of the area the range represents.</param>
        /// <param name="lineToTokensDict">A dictionary onto which to add tokens.</param>
        private void CacheTokensPerLine(Range range, int[] tokens, Dictionary<int, ImmutableArray<int>> lineToTokensDict)
        {
            var absoluteLine = 0;

            // Cache the tokens associated with each line
            var tokenIndex = 0;
            while (tokenIndex < tokens.Length)
            {
                absoluteLine += tokens[tokenIndex];

                // Don't cache tokens outside of the given range
                if (absoluteLine < range.Start.Line)
                {
                    tokenIndex += IntegersPerToken;
                    continue;
                }

                if (absoluteLine > range.End.Line)
                {
                    break;
                }

                lock (_dictLock)
                {
                    if (lineToTokensDict.TryGetValue(absoluteLine, out _))
                    {
                        // This line is already cached, so we can skip it
                        tokenIndex += IntegersPerToken;
                        continue;
                    }
                }

                // Cache until we hit the next line or the end of the tokens
                var lineArrayBuilder = ImmutableArray.CreateBuilder<int>();
                do
                {
                    lineArrayBuilder.AddRange(
                        tokens[tokenIndex],
                        tokens[tokenIndex + 1],
                        tokens[tokenIndex + 2],
                        tokens[tokenIndex + 3],
                        tokens[tokenIndex + 4]);

                    tokenIndex += IntegersPerToken;
                }
                while (tokenIndex < tokens.Length && tokens[tokenIndex] == 0);

                lock (_dictLock)
                {
                    lineToTokensDict[absoluteLine] = lineArrayBuilder.ToImmutableArray();
                }
            }
        }

        private void FillInEmptyLineGaps(Range range, Dictionary<int, ImmutableArray<int>> lineToTokensDict)
        {
            // Fill in the gaps for empty lines. When looking up tokens later, this helps us differentiate
            // between empty lines vs. lines we don't have cached tokens for.
            lock (_dictLock)
            {
                for (var lineIndex = range.Start.Line; lineIndex < range.End.Line; lineIndex++)
                {
                    if (!lineToTokensDict.TryGetValue(lineIndex, out _))
                    {
                        lineToTokensDict.Add(lineIndex, ImmutableArray<int>.Empty);
                    }
                }
            }
        }

        private ImmutableArray<int> GetCachedTokens(
            Range requestedRange,
            Dictionary<int, ImmutableArray<int>> lineToTokensDict,
            out Position? cachedRangeStart,
            out int numLinesInCachedRange)
        {
            var tokens = ImmutableArray.CreateBuilder<int>();

            // Keep track of where the cached range begins
            cachedRangeStart = null;

            // Keep track of the number of lines in the cached range
            numLinesInCachedRange = 0;

            // Keeps track of the current empty line count since the last line of tokens (needed for calculating line offsets)
            var currentEmptyLineCount = 0;

            // Retrieve the cached tokens associated with the passed-in range
            for (var currentLineNumber = requestedRange.Start.Line; currentLineNumber < requestedRange.End.Line; currentLineNumber++)
            {
                ImmutableArray<int> lineTokens;
                lock (_dictLock)
                {
                    if (!lineToTokensDict.TryGetValue(currentLineNumber, out lineTokens))
                    {
                        if (tokens.Count != 0)
                        {
                            // We already have some sort of cached results, but we've now reached
                            // a gap in the cache which we have no tokens for.
                            // For ease of computation, we only want to return a continuous range.
                            // We'll keep our current set of cached results and exit the loop early.
                            break;
                        }

                        // We don't have cached tokens for the line. We'll check other lines in the
                        // requested range to see if we might have cached tokens for those.
                        continue;
                    }
                }

                cachedRangeStart ??= new Position { Line = currentLineNumber, Character = 0 };
                numLinesInCachedRange++;

                // If a line's List is empty, it means the line has no tokens. However, we still want
                // to keep track of empty lines in order to later compute line offsets.
                if (lineTokens.Length == 0)
                {
                    currentEmptyLineCount++;
                    continue;
                }

                // We've found a non-empty cached line. Go through and process it.
                ProcessCachedLine(tokens, currentEmptyLineCount, currentLineNumber, lineTokens);

                currentEmptyLineCount = 0;
            }

            return tokens.ToImmutableArray();
        }

        private static ImmutableArray<int>.Builder ProcessCachedLine(
            ImmutableArray<int>.Builder tokens,
            int currentEmptyLineCount,
            int currentLineNumber,
            IReadOnlyList<int> lineTokens)
        {
            // If we're at the start of the cached range (i.e. we've found no prior tokens in
            // the range up til now), the line offset should be relative to the start of the
            // document.
            if (tokens.Count == 0)
            {
                tokens.Add(currentLineNumber);
            }
            // If we've already found prior cached tokens in the requested range, the line
            // offset should be relative to the end of the last cached non-empty line.
            // Example 1:
            //     token1
            //     token2
            // `token2` should have a line offset of 1.
            // Example 2:
            //     token1
            //     [empty line]
            //     [empty line]
            //     token2
            // `token2` should have a line offset of 3 since we need to take empty lines
            // into account.
            else
            {
                tokens.Add(1 + currentEmptyLineCount);
            }

            // Add the rest of the tokens in the line and return.
            tokens.AddRange(lineTokens.Skip(1));
            return tokens;
        }
    }
}
