// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language;

public class TagHelperMatchingConventionsTest
{
    public static TheoryData RequiredAttributeDescriptorData
    {
        get
        {
            // requiredAttributeDescriptor, attributeName, attributeValue, expectedResult
            return new TheoryData<Action<RequiredAttributeDescriptorBuilder>, string, string, bool>
                {
                    {
                        builder => builder.Name("key"),
                        "KeY",
                        "value",
                        true
                    },
                    {
                        builder => builder.Name("key"),
                        "keys",
                        "value",
                        false
                    },
                    {
                        builder => builder
                            .Name("route-")
                            .NameComparison(RequiredAttributeNameComparison.PrefixMatch),
                        "ROUTE-area",
                        "manage",
                        true
                    },
                    {
                        builder => builder
                            .Name("route-")
                            .NameComparison(RequiredAttributeNameComparison.PrefixMatch),
                        "routearea",
                        "manage",
                        false
                    },
                    {
                        builder => builder
                            .Name("route-")
                            .NameComparison(RequiredAttributeNameComparison.PrefixMatch),
                        "route-",
                        "manage",
                        false
                    },
                    {
                        builder => builder
                            .Name("key")
                            .NameComparison(RequiredAttributeNameComparison.FullMatch),
                        "KeY",
                        "value",
                        true
                    },
                    {
                        builder => builder
                            .Name("key")
                            .NameComparison(RequiredAttributeNameComparison.FullMatch),
                        "keys",
                        "value",
                        false
                    },
                    {
                        builder => builder
                            .Name("key")
                            .NameComparison(RequiredAttributeNameComparison.FullMatch)
                            .Value("value")
                            .ValueComparison(RequiredAttributeValueComparison.FullMatch),
                        "key",
                        "value",
                        true
                    },
                    {
                        builder => builder
                            .Name("key")
                            .NameComparison(RequiredAttributeNameComparison.FullMatch)
                            .Value("value")
                            .ValueComparison(RequiredAttributeValueComparison.FullMatch),
                        "key",
                        "Value",
                        false
                    },
                    {
                        builder => builder
                            .Name("class")
                            .NameComparison(RequiredAttributeNameComparison.FullMatch)
                            .Value("btn")
                            .ValueComparison(RequiredAttributeValueComparison.PrefixMatch),
                        "class",
                        "btn btn-success",
                        true
                    },
                    {
                        builder => builder
                            .Name("class")
                            .NameComparison(RequiredAttributeNameComparison.FullMatch)
                            .Value("btn")
                            .ValueComparison(RequiredAttributeValueComparison.PrefixMatch),
                        "class",
                        "BTN btn-success",
                        false
                    },
                    {
                        builder => builder
                            .Name("href")
                            .NameComparison(RequiredAttributeNameComparison.FullMatch)
                            .Value("#navigate")
                            .ValueComparison(RequiredAttributeValueComparison.SuffixMatch),
                        "href",
                        "/home/index#navigate",
                        true
                    },
                    {
                        builder => builder
                            .Name("href")
                            .NameComparison(RequiredAttributeNameComparison.FullMatch)
                            .Value("#navigate")
                            .ValueComparison(RequiredAttributeValueComparison.SuffixMatch),
                        "href",
                        "/home/index#NAVigate",
                        false
                    },
                };
        }
    }

    [Theory]
    [MemberData(nameof(RequiredAttributeDescriptorData))]
    public void Matches_ReturnsExpectedResult(
        Action<RequiredAttributeDescriptorBuilder> configure,
        string attributeName,
        string attributeValue,
        bool expectedResult)
    {
        // Arrange
        var tagHelperBuilder = new TagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        var tagMatchingRuleBuilder = new TagMatchingRuleDescriptorBuilder(tagHelperBuilder);
        var builder = new RequiredAttributeDescriptorBuilder(tagMatchingRuleBuilder);

        configure(builder);

        var requiredAttibute = builder.Build();

        // Act
        var result = TagHelperMatchingConventions.SatisfiesRequiredAttribute(requiredAttibute, attributeName, attributeValue);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void CanSatisfyBoundAttribute_IndexerAttribute_ReturnsFalseIsNotMatching()
    {
        // Arrange
        var tagHelperBuilder = new TagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        var builder = new BoundAttributeDescriptorBuilder(tagHelperBuilder);
        builder.AsDictionary("asp-", typeof(Dictionary<string, string>).FullName);

        var boundAttribute = builder.Build();

        // Act
        var result = TagHelperMatchingConventions.CanSatisfyBoundAttribute("style", boundAttribute);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanSatisfyBoundAttribute_IndexerAttribute_ReturnsTrueIfMatching()
    {
        // Arrange
        var tagHelperBuilder = new TagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        var builder = new BoundAttributeDescriptorBuilder(tagHelperBuilder);
        builder.AsDictionary("asp-", typeof(Dictionary<string, string>).FullName);

        var boundAttribute = builder.Build();

        // Act
        var result = TagHelperMatchingConventions.CanSatisfyBoundAttribute("asp-route-controller", boundAttribute);

        // Assert
        Assert.True(result);
    }
}
