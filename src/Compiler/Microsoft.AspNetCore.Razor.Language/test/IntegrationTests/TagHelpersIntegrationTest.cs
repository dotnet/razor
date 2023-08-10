﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using Xunit;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Razor.Language.IntegrationTests;

public class TagHelpersIntegrationTest : IntegrationTestBase
{
    [Fact]
    public void SimpleTagHelpers()
    {
        // Arrange
        var descriptors = new[]
        {
                CreateTagHelperDescriptor(
                    tagName: "input",
                    typeName: "InputTagHelper",
                    assemblyName: "TestAssembly")
            };

        var projectEngine = CreateProjectEngine(builder => builder.AddTagHelpers(descriptors));
        var projectItem = CreateProjectItemFromFile();

        // Act
        var codeDocument = projectEngine.Process(projectItem);

        // Assert
        AssertDocumentNodeMatchesBaseline(codeDocument.GetDocumentIntermediateNode());
    }

    [Fact]
    public void TagHelpersWithBoundAttributes()
    {
        // Arrange
        var descriptors = new[]
        {
                CreateTagHelperDescriptor(
                    tagName: "input",
                    typeName: "InputTagHelper",
                    assemblyName: "TestAssembly",
                    attributes: new Action<BoundAttributeDescriptorBuilder>[]
                    {
                        builder => builder
                            .Name("bound")
                            .Metadata(PropertyName("FooProp"))
                            .TypeName("System.String"),
                    })
            };

        var projectEngine = CreateProjectEngine(builder => builder.AddTagHelpers(descriptors));
        var projectItem = CreateProjectItemFromFile();

        // Act
        var codeDocument = projectEngine.Process(projectItem);

        // Assert
        AssertDocumentNodeMatchesBaseline(codeDocument.GetDocumentIntermediateNode());
    }

    [Fact]
    public void NestedTagHelpers()
    {
        // Arrange
        var descriptors = new[]
        {
                CreateTagHelperDescriptor(
                    tagName: "p",
                    typeName: "PTagHelper",
                    assemblyName: "TestAssembly"),
                CreateTagHelperDescriptor(
                    tagName: "form",
                    typeName: "FormTagHelper",
                    assemblyName: "TestAssembly"),
                CreateTagHelperDescriptor(
                    tagName: "input",
                    typeName: "InputTagHelper",
                    assemblyName: "TestAssembly",
                    attributes: new Action<BoundAttributeDescriptorBuilder>[]
                    {
                        builder => builder
                            .Name("value")
                            .Metadata(PropertyName("FooProp"))
                            .TypeName("System.String"),
                    })
            };

        var projectEngine = CreateProjectEngine(builder => builder.AddTagHelpers(descriptors));
        var projectItem = CreateProjectItemFromFile();

        // Act
        var codeDocument = projectEngine.Process(projectItem);

        // Assert
        var syntaxTree = codeDocument.GetSyntaxTree();
        var irTree = codeDocument.GetDocumentIntermediateNode();
        AssertDocumentNodeMatchesBaseline(codeDocument.GetDocumentIntermediateNode());
    }

    private static TagHelperDescriptor CreateTagHelperDescriptor(
        string tagName,
        string typeName,
        string assemblyName,
        IEnumerable<Action<BoundAttributeDescriptorBuilder>> attributes = null)
    {
        var builder = TagHelperDescriptorBuilder.Create(typeName, assemblyName);
        builder.Metadata(TypeName(typeName));

        if (attributes != null)
        {
            foreach (var attributeBuilder in attributes)
            {
                builder.BoundAttributeDescriptor(attributeBuilder);
            }
        }

        builder.TagMatchingRuleDescriptor(ruleBuilder => ruleBuilder.RequireTagName(tagName));

        var descriptor = builder.Build();

        return descriptor;
    }
}
