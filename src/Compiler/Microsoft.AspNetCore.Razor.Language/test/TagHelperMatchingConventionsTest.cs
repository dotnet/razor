﻿// Licensed to the .NET Foundation under one or more agreements.
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
                            .NameComparisonMode(RequiredAttributeDescriptor.NameComparisonMode.PrefixMatch),
                        "ROUTE-area",
                        "manage",
                        true
                    },
                    {
                        builder => builder
                            .Name("route-")
                            .NameComparisonMode(RequiredAttributeDescriptor.NameComparisonMode.PrefixMatch),
                        "routearea",
                        "manage",
                        false
                    },
                    {
                        builder => builder
                            .Name("route-")
                            .NameComparisonMode(RequiredAttributeDescriptor.NameComparisonMode.PrefixMatch),
                        "route-",
                        "manage",
                        false
                    },
                    {
                        builder => builder
                            .Name("key")
                            .NameComparisonMode(RequiredAttributeDescriptor.NameComparisonMode.FullMatch),
                        "KeY",
                        "value",
                        true
                    },
                    {
                        builder => builder
                            .Name("key")
                            .NameComparisonMode(RequiredAttributeDescriptor.NameComparisonMode.FullMatch),
                        "keys",
                        "value",
                        false
                    },
                    {
                        builder => builder
                            .Name("key")
                            .NameComparisonMode(RequiredAttributeDescriptor.NameComparisonMode.FullMatch)
                            .Value("value")
                            .ValueComparisonMode(RequiredAttributeDescriptor.ValueComparisonMode.FullMatch),
                        "key",
                        "value",
                        true
                    },
                    {
                        builder => builder
                            .Name("key")
                            .NameComparisonMode(RequiredAttributeDescriptor.NameComparisonMode.FullMatch)
                            .Value("value")
                            .ValueComparisonMode(RequiredAttributeDescriptor.ValueComparisonMode.FullMatch),
                        "key",
                        "Value",
                        false
                    },
                    {
                        builder => builder
                            .Name("class")
                            .NameComparisonMode(RequiredAttributeDescriptor.NameComparisonMode.FullMatch)
                            .Value("btn")
                            .ValueComparisonMode(RequiredAttributeDescriptor.ValueComparisonMode.PrefixMatch),
                        "class",
                        "btn btn-success",
                        true
                    },
                    {
                        builder => builder
                            .Name("class")
                            .NameComparisonMode(RequiredAttributeDescriptor.NameComparisonMode.FullMatch)
                            .Value("btn")
                            .ValueComparisonMode(RequiredAttributeDescriptor.ValueComparisonMode.PrefixMatch),
                        "class",
                        "BTN btn-success",
                        false
                    },
                    {
                        builder => builder
                            .Name("href")
                            .NameComparisonMode(RequiredAttributeDescriptor.NameComparisonMode.FullMatch)
                            .Value("#navigate")
                            .ValueComparisonMode(RequiredAttributeDescriptor.ValueComparisonMode.SuffixMatch),
                        "href",
                        "/home/index#navigate",
                        true
                    },
                    {
                        builder => builder
                            .Name("href")
                            .NameComparisonMode(RequiredAttributeDescriptor.NameComparisonMode.FullMatch)
                            .Value("#navigate")
                            .ValueComparisonMode(RequiredAttributeDescriptor.ValueComparisonMode.SuffixMatch),
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
        var tagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        var tagMatchingRuleBuilder = new DefaultTagMatchingRuleDescriptorBuilder(tagHelperBuilder);
        var builder = new DefaultRequiredAttributeDescriptorBuilder(tagMatchingRuleBuilder);

        configure(builder);

        var requiredAttibute = builder.Build();

        // Act
        var result = TagHelperMatchingConventions.SatisfiesRequiredAttribute(attributeName, attributeValue, requiredAttibute);

        // Assert
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void CanSatisfyBoundAttribute_IndexerAttribute_ReturnsFalseIsNotMatching()
    {
        // Arrange
        var tagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        var builder = new DefaultBoundAttributeDescriptorBuilder(tagHelperBuilder, TagHelperConventions.DefaultKind);
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
        var tagHelperBuilder = new DefaultTagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "TestTagHelper", "Test");
        var builder = new DefaultBoundAttributeDescriptorBuilder(tagHelperBuilder, TagHelperConventions.DefaultKind);
        builder.AsDictionary("asp-", typeof(Dictionary<string, string>).FullName);

        var boundAttribute = builder.Build();

        // Act
        var result = TagHelperMatchingConventions.CanSatisfyBoundAttribute("asp-route-controller", boundAttribute);

        // Assert
        Assert.True(result);
    }
}
