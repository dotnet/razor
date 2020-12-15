// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor.Serialization;
using Xunit;

namespace Microsoft.VisualStudio.Editor.Razor
{
    public class ConditionalWeakHashSetTests
    {
        [Fact]
        public void GetOrAdd_EquivilentStrings_RetrievesFirstReference()
        {
            // Arrange
            var cache = new ConditionalWeakHashSet<string>();
            // String format to prevent them from being RefEqual
            var str1 = $"stuff {1}";
            var str2 = $"stuff {1}";
            // Sanity check that these aren't already equal
            Assert.False(ReferenceEquals(str1, str2));

            // Act
            // Force a colleciton
            _ = cache.GetOrAddValue(str1);
            GC.Collect();
            var result = cache.GetOrAddValue(str2);

            // Assert
            Assert.Same(result, str1);
            Assert.False(ReferenceEquals(result, str2));
        }

        [Fact]
        public void GetOrAdd_NullReturnsNull()
        {
            // Arrange
            var cache = new ConditionalWeakHashSet<string>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => cache.GetOrAddValue(null));
        }

        [Fact]
        public void GetOrAdd_DisposesReleasedReferencesOnExpand()
        {
            // Arrange
            var cache = new ConditionalWeakHashSet<string>();

            // Act
            StringArea();

            // Force a collection
            GC.Collect();
            var str1 = $"{1}";
            var result = cache.GetOrAddValue(str1);

            // Assert
            Assert.Same(result, str1);

            void StringArea()
            {
                var first = $"{1}";
                var test = cache.GetOrAddValue(first);
                Assert.Same(first, test);
                cache.GetOrAddValue($"{2}");
            }
        }
    }
}
