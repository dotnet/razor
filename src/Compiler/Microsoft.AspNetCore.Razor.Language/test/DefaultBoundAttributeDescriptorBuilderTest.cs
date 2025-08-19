// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.AspNetCore.Razor.Language;

public class DefaultBoundAttributeDescriptorBuilderTest
{
    [Fact]
    public void DisplayName_SetsDescriptorsDisplayName()
    {
        // Arrange
        var expectedDisplayName = "ExpectedDisplayName";

        var tagHelperBuilder = new TagHelperDescriptorBuilder(TagHelperKind.ITagHelper, "TestTagHelper", "Test");

        var builder = new BoundAttributeDescriptorBuilder(tagHelperBuilder);
        builder.DisplayName(expectedDisplayName);

        // Act
        var descriptor = builder.Build();

        // Assert
        Assert.Equal(expectedDisplayName, descriptor.DisplayName);
    }

    [Fact]
    public void DisplayName_DefaultsToPropertyLookingDisplayName()
    {
        // Arrange
        var tagHelperBuilder = new TagHelperDescriptorBuilder(TagHelperKind.ITagHelper, "TestTagHelper", "Test")
        {
            TypeName = "TestTagHelper"
        };

        var builder = new BoundAttributeDescriptorBuilder(tagHelperBuilder);
        builder
            .TypeName(typeof(int).FullName)
            .PropertyName("SomeProperty");

        // Act
        var descriptor = builder.Build();

        // Assert
        Assert.Equal("int TestTagHelper.SomeProperty", descriptor.DisplayName);
    }
}
