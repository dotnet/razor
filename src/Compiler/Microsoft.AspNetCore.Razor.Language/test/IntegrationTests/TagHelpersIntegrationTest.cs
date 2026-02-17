// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.IntegrationTests;

public class TagHelpersIntegrationTest() : IntegrationTestBase(layer: TestProject.Layer.Compiler)
{
    [Fact]
    public void SimpleTagHelpers()
    {
        // Arrange
        TagHelperCollection tagHelpers =
        [
            CreateTagHelperDescriptor(
                tagName: "input",
                typeName: "InputTagHelper",
                assemblyName: "TestAssembly")
        ];

        var projectEngine = CreateProjectEngine(builder => builder.SetTagHelpers(tagHelpers));
        var projectItem = CreateProjectItemFromFile();

        // Act
        var codeDocument = projectEngine.Process(projectItem);

        // Assert
        AssertDocumentNodeMatchesBaseline(codeDocument.GetRequiredDocumentNode());
    }

    [Fact]
    public void TagHelpersWithBoundAttributes()
    {
        // Arrange
        TagHelperCollection tagHelpers =
        [
            CreateTagHelperDescriptor(
                tagName: "input",
                typeName: "InputTagHelper",
                assemblyName: "TestAssembly",
                attributes:
                [
                    builder => builder
                        .Name("bound")
                        .PropertyName("FooProp")
                        .TypeName("System.String"),
                ])
        ];

        var projectEngine = CreateProjectEngine(builder => builder.SetTagHelpers(tagHelpers));
        var projectItem = CreateProjectItemFromFile();

        // Act
        var codeDocument = projectEngine.Process(projectItem);

        // Assert
        AssertDocumentNodeMatchesBaseline(codeDocument.GetRequiredDocumentNode());
    }

    [Fact]
    public void NestedTagHelpers()
    {
        // Arrange
        TagHelperCollection tagHelpers =
        [
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
                attributes:
                [
                    builder => builder
                        .Name("value")
                        .PropertyName("FooProp")
                        .TypeName("System.String"),
                ])
        ];

        var projectEngine = CreateProjectEngine(builder => builder.SetTagHelpers(tagHelpers));
        var projectItem = CreateProjectItemFromFile();

        // Act
        var codeDocument = projectEngine.Process(projectItem);

        // Assert
        AssertDocumentNodeMatchesBaseline(codeDocument.GetRequiredDocumentNode());
    }

    [Fact]
    public void AddTagHelperDirective_TrackedAsUnused_WhenNoTagHelpersReferenced()
    {
        // Arrange
        TagHelperCollection tagHelpers =
        [
            CreateTagHelperDescriptor(
                tagName: "input",
                typeName: "InputTagHelper",
                assemblyName: "TestAssembly")
        ];

        var projectEngine = CreateProjectEngine(builder => builder.SetTagHelpers(tagHelpers));
        var projectItem = AddProjectItemFromText("""
            @addTagHelper *, TestAssembly
            <div>Hello</div>
            """, filePath: "Index.cshtml");

        // Act
        var codeDocument = projectEngine.Process(projectItem);

        // Assert
        var unusedDirectives = codeDocument.GetUnusedDirectives();
        var unusedDirective = Assert.Single(unusedDirectives);
        Assert.Contains("addTagHelper", unusedDirective.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddTagHelperDirective_NotTrackedAsUnused_WhenTagHelperReferenced()
    {
        // Arrange
        TagHelperCollection tagHelpers =
        [
            CreateTagHelperDescriptor(
                tagName: "input",
                typeName: "InputTagHelper",
                assemblyName: "TestAssembly")
        ];

        var projectEngine = CreateProjectEngine(builder => builder.SetTagHelpers(tagHelpers));
        var projectItem = AddProjectItemFromText("""
            @addTagHelper *, TestAssembly
            <input />
            """, filePath: "Index.cshtml");

        // Act
        var codeDocument = projectEngine.Process(projectItem);

        // Assert
        Assert.Empty(codeDocument.GetUnusedDirectives());
    }

    [Fact]
    public void AddTagHelperDirective_StoresDirectiveTagHelperContributions()
    {
        // Arrange
        TagHelperCollection tagHelpers =
        [
            CreateTagHelperDescriptor(
                tagName: "input",
                typeName: "InputTagHelper",
                assemblyName: "TestAssembly")
        ];

        var projectEngine = CreateProjectEngine(builder => builder.SetTagHelpers(tagHelpers));
        var projectItem = AddProjectItemFromText("""
            @addTagHelper *, TestAssembly
            <div>Hello</div>
            """, filePath: "Index.cshtml");

        // Act
        var codeDocument = projectEngine.Process(projectItem);

        // Assert
        var contributions = codeDocument.GetDirectiveTagHelperContributions();
        var contribution = Assert.Single(contributions);
        Assert.Contains("addTagHelper", contribution.Directive.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(contribution.ContributedTagHelpers);
    }

    private static TagHelperDescriptor CreateTagHelperDescriptor(
        string tagName,
        string typeName,
        string assemblyName,
        IEnumerable<Action<BoundAttributeDescriptorBuilder>>? attributes = null)
    {
        var builder = TagHelperDescriptorBuilder.CreateTagHelper(typeName, assemblyName);
        builder.SetTypeName(typeName, typeNamespace: null, typeNameIdentifier: null);

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
