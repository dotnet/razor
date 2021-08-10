// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#pragma warning disable CS0618
#nullable disable

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Semantic
{
    public class SemanticTokensCacheTest
    {
        [Fact]
        public void SemanticTokensCache_GetCacheItem()
        {
            // Arrange
            var cache = new SemanticTokensCache<SemanticTokens>();
            var documentUri = new DocumentUri("file", authority: null, path: "\\\\testpath", query: null, fragment: null);
            var tokens = new SemanticTokens { ResultId = "10", Data = ImmutableArray<int>.Empty };

            cache.UpdateCache(documentUri, tokens);

            // Act
            var cachedTokens = cache.GetCachedTokensData(documentUri, "10");

            // Assert
            Assert.NotNull(cachedTokens);
            Assert.Equal("10", cachedTokens.ResultId);
        }

        [Fact]
        public void SemanticTokensCache_GetInvalidCacheItem()
        {
            // Arrange
            var cache = new SemanticTokensCache<SemanticTokens>();
            var documentUri = new DocumentUri("file", authority: null, path: "\\\\testpath", query: null, fragment: null);
            var tokens = new SemanticTokens { ResultId = "10", Data = ImmutableArray<int>.Empty };

            cache.UpdateCache(documentUri, tokens);

            // Act
            var cachedTokens = cache.GetCachedTokensData(documentUri, "0");

            // Assert
            Assert.Null(cachedTokens);
        }

        [Fact]
        public void SemanticTokensCache_EvictOldestItem()
        {
            // Arrange
            var cache = new SemanticTokensCache<SemanticTokens>();
            var firstDocumentUri = new DocumentUri("file", authority: null, path: "\\\\testpath1", query: null, fragment: null);
            var secondDocumentUri = new DocumentUri("file", authority: null, path: "\\\\testpath2", query: null, fragment: null);
            var tokens = new SemanticTokens { ResultId = "10", Data = ImmutableArray<int>.Empty };

            cache.UpdateCache(firstDocumentUri, tokens);
            cache.UpdateCache(secondDocumentUri, tokens);

            // Fill up the first document's cache to one past its max
            var dummyResultId = "1";
            for (var i = 0; i < SemanticTokensCache<SemanticTokens>.MaxCachesPerDoc; i++)
            {
                tokens = new SemanticTokens { ResultId = dummyResultId, Data = ImmutableArray<int>.Empty };
                cache.UpdateCache(firstDocumentUri, tokens);
                dummyResultId += "1";
            }

            // Act
            var firstDocCachedTokens = cache.GetCachedTokensData(firstDocumentUri, "10");
            var secondDocCachedTokens = cache.GetCachedTokensData(secondDocumentUri, "10");

            // Assert
            Assert.Null(firstDocCachedTokens);
            Assert.NotNull(secondDocCachedTokens);
            Assert.Equal("10", secondDocCachedTokens.ResultId);
        }
    }
}
