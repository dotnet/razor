// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#pragma warning disable CS0618
#nullable enable

using Microsoft.CodeAnalysis.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Generic;
using System;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    /// <summary>
    /// Cache used by <see cref="DefaultRazorSemanticTokensInfoService"/> to keep track of
    /// previously computed tokens.
    /// </summary>
    internal class SemanticTokensCache<T> where T : SemanticTokens
    {
        /// <summary>
        /// Number of cached token sets we store per document. Must be >= 1.
        /// </summary>
        /// <remarks>
        /// Internal for testing only.
        /// </remarks>
        internal const int MaxCachesPerDoc = 5;

        /// <summary>
        /// Multiple cache requests or updates may be received concurrently. We need this lock to
        /// ensure that we aren't making concurrent modifications to the _documentToTokenSets
        /// dictionary.
        /// </summary>
        private readonly object _lock = new();

        #region protected by _lock
        /// <summary>
        /// Maps a document URI to its n most recently cached token sets. We use an in-memory cache
        /// to limit memory allocation.
        /// </summary>
        private readonly MemoryCache<DocumentUri, List<T>> _documentToTokenSets = new();
        #endregion

        /// <summary>
        /// Updates the given document's token set cache. Removes old cache results if the
        /// document's cache is full.
        /// </summary>
        public void UpdateCache(DocumentUri uri, T tokens)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (tokens is null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            lock (_lock)
            {
                // Case 1: Document does not currently have any token sets cached. Create a cache
                // for the document and return.
                if (!_documentToTokenSets.TryGetValue(uri, out var tokenSets))
                {
                    _documentToTokenSets.Set(uri, new List<T> { tokens });
                    return;
                }

                // Case 2: Document already has the maximum number of token sets cached. Remove the
                // oldest token set from the cache, and then add the new token set (see case 3).
                if (tokenSets.Count >= MaxCachesPerDoc)
                {
                    tokenSets.RemoveAt(0);
                }

                // Case 3: Document has less than the maximum number of token sets cached.
                // Add new token set to cache.
                tokenSets.Add(tokens);
            }
        }

        /// <returns>
        /// The cached tokens data for a given document URI and resultId, null if no match is found.
        /// </returns>
        public T? GetCachedTokensData(DocumentUri uri, string resultId)
        {
            lock (_lock)
            {
                if (!_documentToTokenSets.TryGetValue(uri, out var tokenSets))
                {
                    return default;
                }

                var matchingTokenSet = tokenSets.FirstOrDefault(t => t.ResultId == resultId);
                return matchingTokenSet;
            }
        }
    }
}
