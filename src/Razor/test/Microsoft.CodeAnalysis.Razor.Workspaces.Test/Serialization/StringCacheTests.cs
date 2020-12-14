// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.Editor.Razor
{
    public class StringCacheTests
    {
        [Fact]
        public void GetOrAdd_RetrievesFirstReference()
        {
            // Arrange
            var cache = new StringCache();
            // String format to prevent them from being RefEqual
            var str1 = $"stuff {1}";
            var str2 = $"stuff {1}";
            // Sanity check that these aren't already equal
            Assert.False(Object.ReferenceEquals(str1, str2));

            // Act
            _ = cache.GetorAdd(str1);
            var result = cache.GetOrAdd(str2);

            // Assert
            Assert.True(Object.ReferenceEquals(result, str1), "Result should have been RefEq to str1");
            Assert.False(Object.ReferenceEquals(result, str2));
        }

        [Fact]
        public void GetOrAdd_NullReturnsNull(){
            // Arrange
            var cache = new StringCache();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => cache.GetOrAdd(null));
        }

        [Fact]
        public void GetOrAdd_DisposesReleasedReferencesOnExpand()
        {
            // Arrange
            var cache = new StringCache();

            // Act
            var str1 = "1"

            cache.GetOrAdd(str1);
            cache.GetOrAdd("2");

            // Assert

            throw new NotImplementedExceptions();
        }
    }
}
