﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.IntegrationTests;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions.Version2_X.IntegrationTests;

public class InstrumentationPassIntegrationTest : IntegrationTestBase
{
    private static readonly CSharpCompilation DefaultBaseCompilation = MvcShim.BaseCompilation.WithAssemblyName("AppCode");

    public InstrumentationPassIntegrationTest()
        : base(generateBaselines: null, projectDirectoryHint: "Microsoft.AspNetCore.Mvc.Razor.Extensions.Version2_X")
    {
        Configuration = RazorConfiguration.Create(
            RazorLanguageVersion.Version_2_0,
            "MVC-2.1",
            new[] { new AssemblyExtension("MVC-2.1", typeof(ExtensionInitializer).Assembly) });
    }

    protected override CSharpCompilation BaseCompilation => DefaultBaseCompilation;

    protected override RazorConfiguration Configuration { get; }

    [Fact]
    public void BasicTest()
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
                            .TypeName("System.String"),      // Gets preallocated
                        builder => builder
                            .Name("date")
                            .Metadata(PropertyName("BarProp"))
                            .TypeName("System.DateTime"),    // Doesn't get preallocated
                    })
            };

        var engine = CreateProjectEngine(b =>
        {
            b.AddTagHelpers(descriptors);
            b.Features.Add(new InstrumentationPass());

                // This test includes templates
                b.AddTargetExtension(new TemplateTargetExtension());
        });

        var projectItem = CreateProjectItemFromFile();

        // Act
        var document = engine.Process(projectItem);

        // Assert
        AssertDocumentNodeMatchesBaseline(document.GetDocumentIntermediateNode());

        var csharpDocument = document.GetCSharpDocument();
        AssertCSharpDocumentMatchesBaseline(csharpDocument);
        Assert.Empty(csharpDocument.Diagnostics);
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
