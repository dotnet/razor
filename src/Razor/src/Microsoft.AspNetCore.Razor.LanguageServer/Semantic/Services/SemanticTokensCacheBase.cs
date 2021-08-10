// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using Microsoft.CodeAnalysis.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol;
using System.Collections.Generic;
using System;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal abstract class SemanticTokensCacheBase<T>
    {
        private readonly int _maxCachesPerDoc = 5;

        private readonly object _lock = new();

        private readonly MemoryCache<DocumentUri, List<T>> _documentToTokenSets = new();

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
                if (tokenSets.Count >= _maxCachesPerDoc)
                {
                    tokenSets.RemoveAt(0);
                }

                // Case 3: Document has less than the maximum number of token sets cached.
                // Add new token set to cache.
                tokenSets.Add(tokens);
            }
        }

        public T? GetCachedTokensData(DocumentUri uri, string resultId)
        {
            lock (_lock)
            {
                if (!_documentToTokenSets.TryGetValue(uri, out var tokenSets))
                {
                    return default;
                }

                // TO-DO: Update comment
                var matchingTokenSet = GetMatchingTokenSet(tokenSets, resultId);
                return matchingTokenSet;
            }
        }

        public abstract T? GetMatchingTokenSet(List<T> tokenSets, string resultId);
    }
}
