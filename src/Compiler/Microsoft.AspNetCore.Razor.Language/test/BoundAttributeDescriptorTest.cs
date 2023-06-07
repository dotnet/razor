﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Xunit;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Razor.Language.Test;

public class BoundAttributeDescriptorTest
{
    [Fact]
    public void BoundAttributeDescriptor_HashDoesNotChangeWithType()
    {
        var expectedPropertyName = "PropertyName";

        var tagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        _ = tagHelperBuilder.Metadata(TypeName("TestTagHelper"));

        var intBuilder = new DefaultBoundAttributeDescriptorBuilder(tagHelperBuilder, TagHelperConventions.DefaultKind);
        _ = intBuilder
            .Name("test")
            .Metadata(PropertyName(expectedPropertyName))
            .TypeName(typeof(int).FullName);

        var intDescriptor = intBuilder.Build();

        var stringBuilder = new DefaultBoundAttributeDescriptorBuilder(tagHelperBuilder, TagHelperConventions.DefaultKind);
        _ = stringBuilder
            .Name("test")
            .Metadata(PropertyName(expectedPropertyName))
            .TypeName(typeof(string).FullName);
        var stringDescriptor = stringBuilder.Build();

        Assert.Equal(intDescriptor.GetHashCode(), stringDescriptor.GetHashCode());
    }
}
