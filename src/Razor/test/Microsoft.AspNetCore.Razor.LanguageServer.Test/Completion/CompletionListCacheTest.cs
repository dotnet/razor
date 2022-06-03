// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    public class CompletionListCacheTest
    {
        private CompletionListCache CompletionListCache { get; } = new CompletionListCache();

        private object Context { get; } = new object();

        [Fact]
        public void TryGet_SetCompletionList_ReturnsTrue()
        {
            // Arrange
            var completionList = new VSInternalCompletionList();
            var resultId = CompletionListCache.Set(completionList, Context);

            // Act
            var result = CompletionListCache.TryGet(resultId, out var cacheEntry);

            // Assert
            Assert.True(result);
            Assert.Same(completionList, cacheEntry.CompletionList);
            Assert.Same(Context, cacheEntry.Context);
        }

        [Fact]
        public void TryGet_UnknownCompletionList_ReturnsTrue()
        {
            // Act
            var result = CompletionListCache.TryGet(1234, out var cachedEntry);

            // Assert
            Assert.False(result);
            Assert.Null(cachedEntry);
        }

        [Fact]
        public void TryGet_EvictedCompletionList_ReturnsFalse()
        {
            // Arrange
            var initialCompletionList = new VSInternalCompletionList();
            var initialCompletionListResultId = CompletionListCache.Set(initialCompletionList, Context);
            for (var i = 0; i < CompletionListCache.MaxCacheSize; i++)
            {
                // We now fill the completion list cache up until its cache max so that the initial completion list we set gets evicted.
                CompletionListCache.Set(new VSInternalCompletionList(), Context);
            }

            // Act
            var result = CompletionListCache.TryGet(initialCompletionListResultId, out var cachedEntry);

            // Assert
            Assert.False(result);
            Assert.Null(cachedEntry);
        }
    }
}
