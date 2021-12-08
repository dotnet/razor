// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.Language;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Test
{
    public class TagHelperDescriptorCacheTest
    {
        [Fact]
        public void TagHelperDescriptorCache_TypeNameAffectsHash()
        {
            // Arrange
            var expectedPropertyName = "PropertyName";

            var intTagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
            _ = intTagHelperBuilder.TypeName("TestTagHelper");
            intTagHelperBuilder.BoundAttributeDescriptor(intBuilder =>
                intBuilder
                    .Name("test")
                    .PropertyName(expectedPropertyName)
                    .TypeName(typeof(int).FullName)
            );
            var intTagHelper = intTagHelperBuilder.Build();

            var stringTagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
            _ = stringTagHelperBuilder.TypeName("TestTagHelper");
            stringTagHelperBuilder.BoundAttributeDescriptor(stringBuilder =>
                stringBuilder
                    .Name("test")
                    .PropertyName(expectedPropertyName)
                    .TypeName(typeof(string).FullName)
            );
            var stringTagHelper = stringTagHelperBuilder.Build();

            // Act
            TagHelperDescriptorCache.Set(intTagHelper.GetHashCode(), intTagHelper);

            // Assert
            Assert.False(TagHelperDescriptorCache.TryGetDescriptor(stringTagHelper.GetHashCode(), out var descriptor));
        }
    }
}
