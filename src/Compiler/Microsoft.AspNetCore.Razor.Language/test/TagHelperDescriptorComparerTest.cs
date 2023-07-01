﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using Xunit;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Razor.Language;

public class TagHelperDescriptorComparerTest
{
    [Fact]
    public void GetHashCode_SameTagHelperDescriptors_HashCodeMatches()
    {
        // Arrange
        var descriptor1 = CreateTagHelperDescriptor(
                tagName: "input",
                typeName: "InputTagHelper",
                assemblyName: "TestAssembly",
                attributes: new Action<BoundAttributeDescriptorBuilder>[]
                {
                    builder => builder
                        .Name("value")
                        .Metadata(PropertyName("FooProp"))
                        .TypeName("System.String"),
                });
        var descriptor2 = CreateTagHelperDescriptor(
                tagName: "input",
                typeName: "InputTagHelper",
                assemblyName: "TestAssembly",
                attributes: new Action<BoundAttributeDescriptorBuilder>[]
                {
                    builder => builder
                        .Name("value")
                        .Metadata(PropertyName("FooProp"))
                        .TypeName("System.String"),
                });

        // Act
        var hashCode1 = descriptor1.GetHashCode();
        var hashCode2 = descriptor2.GetHashCode();

        // Assert
        Assert.Equal(hashCode1, hashCode2);
    }

    [Fact]
    public void GetHashCode_FQNAndNameTagHelperDescriptors_HashCodeMatches()
    {
        // Arrange
        var descriptorName = CreateTagHelperDescriptor(
            tagName: "input",
            typeName: "InputTagHelper",
            assemblyName: "TestAssembly",
            tagMatchingRuleName: "Input",
            attributes: new Action<BoundAttributeDescriptorBuilder>[]
            {
                builder => builder
                    .Name("value")
                    .Metadata(PropertyName("FooProp"))
                    .TypeName("System.String"),
            });

        var descriptorFQN = CreateTagHelperDescriptor(
            tagName: "input",
            typeName: "InputTagHelper",
            assemblyName: "TestAssembly",
            tagMatchingRuleName: "Microsoft.AspNetCore.Components.Forms.Input",
            attributes: new Action<BoundAttributeDescriptorBuilder>[]
            {
                builder => builder
                    .Name("value")
                    .Metadata(PropertyName("FooProp"))
                    .TypeName("System.String"),
            });

        // Act
        var hashCodeName = descriptorName.GetHashCode();
        var hashCodeFQN = descriptorFQN.GetHashCode();

        // Assert
        Assert.Equal(hashCodeName, hashCodeFQN);
    }

    [Fact]
    public void GetHashCode_DifferentTagHelperDescriptors_HashCodeDoesNotMatch()
    {
        // Arrange
        var counterTagHelper = CreateTagHelperDescriptor(
            tagName: "Counter",
            typeName: "CounterTagHelper",
            assemblyName: "Components.Component",
            tagMatchingRuleName: "Input",
            attributes: new Action<BoundAttributeDescriptorBuilder>[]
            {
                builder => builder
                    .Name("IncrementBy")
                    .Metadata(PropertyName("IncrementBy"))
                    .TypeName("System.Int32"),
            });

        var inputTagHelper = CreateTagHelperDescriptor(
            tagName: "input",
            typeName: "InputTagHelper",
            assemblyName: "TestAssembly",
            tagMatchingRuleName: "Microsoft.AspNetCore.Components.Forms.Input",
            attributes: new Action<BoundAttributeDescriptorBuilder>[]
            {
                builder => builder
                    .Name("value")
                    .Metadata(PropertyName("FooProp"))
                    .TypeName("System.String"),
            });

        // Act
        var hashCodeCounter = counterTagHelper.GetHashCode();
        var hashCodeInput = inputTagHelper.GetHashCode();

        // Assert
        Assert.NotEqual(hashCodeCounter, hashCodeInput);
    }

    private static TagHelperDescriptor CreateTagHelperDescriptor(
        string tagName,
        string typeName,
        string assemblyName,
        string tagMatchingRuleName = null,
        IEnumerable<Action<BoundAttributeDescriptorBuilder>> attributes = null)
    {
        var builder = TagHelperDescriptorBuilder.Create(typeName, assemblyName) as DefaultTagHelperDescriptorBuilder;
        builder.Metadata(TypeName(typeName));

        if (attributes != null)
        {
            foreach (var attributeBuilder in attributes)
            {
                builder.BoundAttributeDescriptor(attributeBuilder);
            }
        }

        builder.TagMatchingRuleDescriptor(ruleBuilder => ruleBuilder.RequireTagName(tagMatchingRuleName ?? tagName));

        var descriptor = builder.Build();

        return descriptor;
    }
}
