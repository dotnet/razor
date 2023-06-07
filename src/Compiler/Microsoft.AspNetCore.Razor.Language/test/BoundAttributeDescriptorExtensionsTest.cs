﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Xunit;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Razor.Language;

public class BoundAttributeDescriptorExtensionsTest
{
    [Fact]
    public void GetPropertyName_ReturnsPropertyName()
    {
        // Arrange
        var expectedPropertyName = "IntProperty";

        var tagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        tagHelperBuilder.Metadata(TypeName("TestTagHelper"));

        var builder = new DefaultBoundAttributeDescriptorBuilder(tagHelperBuilder, TagHelperConventions.DefaultKind);
        builder
            .Name("test")
            .Metadata(PropertyName(expectedPropertyName))
            .TypeName(typeof(int).FullName);

        var descriptor = builder.Build();

        // Act
        var propertyName = descriptor.GetPropertyName();

        // Assert
        Assert.Equal(expectedPropertyName, propertyName);
    }

    [Fact]
    public void GetPropertyName_ReturnsNullIfNoPropertyName()
    {
        // Arrange
        var tagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        tagHelperBuilder.Metadata(TypeName("TestTagHelper"));

        var builder = new DefaultBoundAttributeDescriptorBuilder(tagHelperBuilder, TagHelperConventions.DefaultKind);
        builder
            .Name("test")
            .TypeName(typeof(int).FullName);

        var descriptor = builder.Build();

        // Act
        var propertyName = descriptor.GetPropertyName();

        // Assert
        Assert.Null(propertyName);
    }

    [Fact]
    public void IsDefaultKind_ReturnsTrue_IfKindIsDefault()
    {
        // Arrange
        var tagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        tagHelperBuilder.Metadata(TypeName("TestTagHelper"));

        var builder = new DefaultBoundAttributeDescriptorBuilder(tagHelperBuilder, TagHelperConventions.DefaultKind);
        builder
            .Name("test")
            .Metadata(PropertyName("IntProperty"))
            .TypeName(typeof(int).FullName);

        var descriptor = builder.Build();

        // Act
        var isDefault = descriptor.IsDefaultKind();

        // Assert
        Assert.True(isDefault);
    }

    [Fact]
    public void IsDefaultKind_ReturnsFalse_IfKindIsNotDefault()
    {
        // Arrange
        var tagHelperBuilder = new DefaultTagHelperDescriptorBuilder("other-kind", "TestTagHelper", "Test");
        tagHelperBuilder.Metadata(TypeName("TestTagHelper"));

        var builder = new DefaultBoundAttributeDescriptorBuilder(tagHelperBuilder, "other-kind");
        builder
            .Name("test")
            .Metadata(PropertyName("IntProperty"))
            .TypeName(typeof(int).FullName);

        var descriptor = builder.Build();

        // Act
        var isDefault = descriptor.IsDefaultKind();

        // Assert
        Assert.False(isDefault);
    }

    [Fact]
    public void ExpectsStringValue_ReturnsTrue_ForStringProperty()
    {
        // Arrange
        var tagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        tagHelperBuilder.Metadata(TypeName("TestTagHelper"));

        var builder = new DefaultBoundAttributeDescriptorBuilder(tagHelperBuilder, TagHelperConventions.DefaultKind);
        builder
            .Name("test")
            .Metadata(PropertyName("BoundProp"))
            .TypeName(typeof(string).FullName);

        var descriptor = builder.Build();

        // Act
        var result = descriptor.ExpectsStringValue("test");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ExpectsStringValue_ReturnsFalse_ForNonStringProperty()
    {
        // Arrange
        var tagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        tagHelperBuilder.Metadata(TypeName("TestTagHelper"));

        var builder = new DefaultBoundAttributeDescriptorBuilder(tagHelperBuilder, TagHelperConventions.DefaultKind);
        builder
            .Name("test")
            .Metadata(PropertyName("BoundProp"))
            .TypeName(typeof(bool).FullName);

        var descriptor = builder.Build();

        // Act
        var result = descriptor.ExpectsStringValue("test");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ExpectsStringValue_ReturnsTrue_StringIndexerAndNameMatch()
    {
        // Arrange
        var tagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        tagHelperBuilder.Metadata(TypeName("TestTagHelper"));

        var builder = new DefaultBoundAttributeDescriptorBuilder(tagHelperBuilder, TagHelperConventions.DefaultKind);
        builder
            .Name("test")
            .Metadata(PropertyName("BoundProp"))
            .TypeName("System.Collection.Generic.IDictionary<string, string>")
            .AsDictionary("prefix-test-", typeof(string).FullName);

        var descriptor = builder.Build();

        // Act
        var result = descriptor.ExpectsStringValue("prefix-test-key");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ExpectsStringValue_ReturnsFalse_StringIndexerAndNameMismatch()
    {
        // Arrange
        var tagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        tagHelperBuilder.Metadata(TypeName("TestTagHelper"));

        var builder = new DefaultBoundAttributeDescriptorBuilder(tagHelperBuilder, TagHelperConventions.DefaultKind);
        builder
            .Name("test")
            .Metadata(PropertyName("BoundProp"))
            .TypeName("System.Collection.Generic.IDictionary<string, string>")
            .AsDictionary("prefix-test-", typeof(string).FullName);

        var descriptor = builder.Build();

        // Act
        var result = descriptor.ExpectsStringValue("test");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ExpectsBooleanValue_ReturnsTrue_ForBooleanProperty()
    {
        // Arrange
        var tagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        tagHelperBuilder.Metadata(TypeName("TestTagHelper"));

        var builder = new DefaultBoundAttributeDescriptorBuilder(tagHelperBuilder, TagHelperConventions.DefaultKind);
        builder
            .Name("test")
            .Metadata(PropertyName("BoundProp"))
            .TypeName(typeof(bool).FullName);

        var descriptor = builder.Build();

        // Act
        var result = descriptor.ExpectsBooleanValue("test");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ExpectsBooleanValue_ReturnsFalse_ForNonBooleanProperty()
    {
        // Arrange
        var tagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        tagHelperBuilder.Metadata(TypeName("TestTagHelper"));

        var builder = new DefaultBoundAttributeDescriptorBuilder(tagHelperBuilder, TagHelperConventions.DefaultKind);
        builder
            .Name("test")
            .Metadata(PropertyName("BoundProp"))
            .TypeName(typeof(int).FullName);

        var descriptor = builder.Build();

        // Act
        var result = descriptor.ExpectsBooleanValue("test");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ExpectsBooleanValue_ReturnsTrue_BooleanIndexerAndNameMatch()
    {
        // Arrange
        var tagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        tagHelperBuilder.Metadata(TypeName("TestTagHelper"));

        var builder = new DefaultBoundAttributeDescriptorBuilder(tagHelperBuilder, TagHelperConventions.DefaultKind);
        builder
            .Name("test")
            .Metadata(PropertyName("BoundProp"))
            .TypeName("System.Collection.Generic.IDictionary<string, bool>")
            .AsDictionary("prefix-test-", typeof(bool).FullName);

        var descriptor = builder.Build();

        // Act
        var result = descriptor.ExpectsBooleanValue("prefix-test-key");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ExpectsBooleanValue_ReturnsFalse_BooleanIndexerAndNameMismatch()
    {
        // Arrange
        var tagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        tagHelperBuilder.Metadata(TypeName("TestTagHelper"));

        var builder = new DefaultBoundAttributeDescriptorBuilder(tagHelperBuilder, TagHelperConventions.DefaultKind);
        builder
            .Name("test")
            .Metadata(PropertyName("BoundProp"))
            .TypeName("System.Collection.Generic.IDictionary<string, bool>")
            .AsDictionary("prefix-test-", typeof(bool).FullName);

        var descriptor = builder.Build();

        // Act
        var result = descriptor.ExpectsBooleanValue("test");

        // Assert
        Assert.False(result);
    }
}
