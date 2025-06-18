// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language;

public class DefaultRequiredAttributeDescriptorBuilderTest
{
    [Fact]
    public void Build_DisplayNameIsName_NameComparisonFullMatch()
    {
        // Arrange
        var builder = TagHelperDescriptorBuilder.Create(TagHelperConventions.DefaultKind, "TestTagHelper", "Test")
            .TagMatchingRuleDescriptor(rule => rule
                .RequireAttributeDescriptor(attribute => attribute
                    .Name("asp-action", RequiredAttributeNameComparison.FullMatch)));

        // Act
        var tagHelper = builder.Build();
        var attribute = tagHelper.TagMatchingRules[0].Attributes[0];

        // Assert
        Assert.Equal("asp-action", attribute.DisplayName);
    }

    [Fact]
    public void Build_DisplayNameIsNameWithDots_NameComparisonPrefixMatch()
    {
        // Arrange
        var builder = TagHelperDescriptorBuilder.Create(TagHelperConventions.DefaultKind, "TestTagHelper", "Test")
            .TagMatchingRuleDescriptor(rule => rule
                .RequireAttributeDescriptor(attribute => attribute
                    .Name("asp-route-", RequiredAttributeNameComparison.PrefixMatch)));

        // Act
        var tagHelper = builder.Build();
        var attribute = tagHelper.TagMatchingRules[0].Attributes[0];

        // Assert
        Assert.Equal("asp-route-...", attribute.DisplayName);
    }

    [Fact]
    public void Metadata_Same()
    {
        // When SetMetadata is called on multiple builders with the same metadata collection,
        // they should share the instance.

        // Arrange
        var metadata = MetadataCollection.Create(KeyValuePair.Create<string, string?>("TestKey", "TestValue"));

        var builder = TagHelperDescriptorBuilder.Create(TagHelperConventions.DefaultKind, "TestTagHelper", "Test")
            .TagMatchingRuleDescriptor(rule => rule
                .RequireAttributeDescriptor(attribute => attribute
                    .Name("test1")
                    .SetMetadata(metadata))
                .RequireAttributeDescriptor(attribute => attribute
                    .Name("test2")
                    .SetMetadata(metadata)));

        // Act
        var tagHelper = builder.Build();
        var attribute1 = tagHelper.TagMatchingRules[0].Attributes[0];
        var attribute2 = tagHelper.TagMatchingRules[0].Attributes[1];

        // Assert
        Assert.Same(attribute1.Metadata, attribute2.Metadata);
    }

    [Fact]
    public void Metadata_NotSame()
    {
        // When Metadata is accessed on multiple builders with the same metadata,
        // they do not share the instance.

        // Arrange
        var builder = TagHelperDescriptorBuilder.Create(TagHelperConventions.DefaultKind, "TestTagHelper", "Test")
            .TagMatchingRuleDescriptor(rule => rule
                .RequireAttributeDescriptor(attribute => attribute
                    .Name("test1")
                    .Metadata.Add("TestKey", "TestValue"))
                .RequireAttributeDescriptor(attribute => attribute
                    .Name("test2")
                    .Metadata.Add("TestKey", "TestValue")));

        // Act
        var tagHelper = builder.Build();
        var attribute1 = tagHelper.TagMatchingRules[0].Attributes[0];
        var attribute2 = tagHelper.TagMatchingRules[0].Attributes[1];

        // Assert
        Assert.NotSame(attribute1.Metadata, attribute2.Metadata);
    }
}
