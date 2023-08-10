﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions.Version2_X;

public class ViewComponentTagHelperPassTest
{
    [Fact]
    public void ViewComponentTagHelperPass_Execute_IgnoresRegularTagHelper()
    {
        // Arrange
        var codeDocument = CreateDocument(@"
@addTagHelper TestTagHelper, TestAssembly
<p foo=""17"">");

        var tagHelpers = new[]
        {
            TagHelperDescriptorBuilder.Create("TestTagHelper", "TestAssembly")
                .Metadata(TypeName("TestTagHelper"))
                .BoundAttributeDescriptor(attribute => attribute
                    .Name("Foo")
                    .TypeName("System.Int32"))
                .TagMatchingRuleDescriptor(rule => rule.RequireTagName("p"))
                .Build()
        };

        var projectEngine = CreateProjectEngine(tagHelpers);
        var pass = new ViewComponentTagHelperPass()
        {
            Engine = projectEngine.Engine,
        };

        var irDocument = CreateIRDocument(projectEngine, codeDocument);

        // Act
        pass.Execute(codeDocument, irDocument);

        // Assert
        var @class = FindClassNode(irDocument);
        Assert.Equal(3, @class.Children.Count); // No class node created for a VCTH
        for (var i = 0; i < @class.Children.Count; i++)
        {
            Assert.IsNotType<ViewComponentTagHelperIntermediateNode>(@class.Children[i]);
        }
    }

    [Fact]
    public void ViewComponentTagHelperPass_Execute_CreatesViewComponentTagHelper()
    {
        // Arrange
        var codeDocument = CreateDocument(@"
@addTagHelper TestTagHelper, TestAssembly
<tagcloud foo=""17"">");

        var tagHelpers = new[]
        {
            TagHelperDescriptorBuilder.Create(ViewComponentTagHelperConventions.Kind, "TestTagHelper", "TestAssembly")
                .Metadata(
                    TypeName("__Generated__TagCloudViewComponentTagHelper"),
                    new(ViewComponentTagHelperMetadata.Name, "TagCloud"))
                .BoundAttributeDescriptor(attribute => attribute
                    .Name("Foo")
                    .TypeName("System.Int32")
                    .Metadata(PropertyName("Foo")))
                .TagMatchingRuleDescriptor(rule => rule.RequireTagName("tagcloud"))
                .Build()
        };

        var projectEngine = CreateProjectEngine(tagHelpers);
        var pass = new ViewComponentTagHelperPass()
        {
            Engine = projectEngine.Engine,
        };

        var irDocument = CreateIRDocument(projectEngine, codeDocument);

        var vcthFullName = "AspNetCore.test.__Generated__TagCloudViewComponentTagHelper";

        // Act
        pass.Execute(codeDocument, irDocument);

        // Assert
        var tagHelper = FindTagHelperNode(irDocument);
        Assert.Equal(vcthFullName, Assert.IsType<DefaultTagHelperCreateIntermediateNode>(tagHelper.Children[1]).TypeName);
        Assert.Equal("Foo", Assert.IsType<DefaultTagHelperPropertyIntermediateNode>(tagHelper.Children[2]).PropertyName);


        var @class = FindClassNode(irDocument);
        Assert.Equal(4, @class.Children.Count);

        Assert.IsType<ViewComponentTagHelperIntermediateNode>(@class.Children.Last());
    }

    [Fact]
    public void ViewComponentTagHelperPass_Execute_CreatesViewComponentTagHelper_WithIndexer()
    {
        // Arrange
        var codeDocument = CreateDocument(@"
@addTagHelper TestTagHelper, TestAssembly
<tagcloud tag-foo=""17"">");

        var tagHelpers = new[]
        {
            TagHelperDescriptorBuilder.Create(ViewComponentTagHelperConventions.Kind, "TestTagHelper", "TestAssembly")
                .Metadata(
                    TypeName("__Generated__TagCloudViewComponentTagHelper"),
                    new(ViewComponentTagHelperMetadata.Name, "TagCloud"))
                .BoundAttributeDescriptor(attribute => attribute
                    .Name("Foo")
                    .TypeName("System.Collections.Generic.Dictionary<System.String, System.Int32>")
                    .Metadata(PropertyName("Tags"))
                    .AsDictionaryAttribute("foo-", "System.Int32"))
                .TagMatchingRuleDescriptor(rule => rule.RequireTagName("tagcloud"))
                .Build()
        };

        var projectEngine = CreateProjectEngine(tagHelpers);
        var pass = new ViewComponentTagHelperPass()
        {
            Engine = projectEngine.Engine,
        };

        var irDocument = CreateIRDocument(projectEngine, codeDocument);

        var vcthFullName = "AspNetCore.test.__Generated__TagCloudViewComponentTagHelper";

        // Act
        pass.Execute(codeDocument, irDocument);

        // Assert
        var tagHelper = FindTagHelperNode(irDocument);
        Assert.Equal(vcthFullName, Assert.IsType<DefaultTagHelperCreateIntermediateNode>(tagHelper.Children[1]).TypeName);
        Assert.IsType<DefaultTagHelperHtmlAttributeIntermediateNode>(tagHelper.Children[2]);

        var @class = FindClassNode(irDocument);
        Assert.Equal(4, @class.Children.Count);

        Assert.IsType<ViewComponentTagHelperIntermediateNode>(@class.Children[3]);
    }

    [Fact]
    public void ViewComponentTagHelperPass_Execute_CreatesViewComponentTagHelper_Nested()
    {
        // Arrange
        var codeDocument = CreateDocument(@"
@addTagHelper *, TestAssembly
<p foo=""17""><tagcloud foo=""17""></p>");

        var tagHelpers = new[]
        {
            TagHelperDescriptorBuilder.Create("PTestTagHelper", "TestAssembly")
                .Metadata(TypeName("PTestTagHelper"))
                .BoundAttributeDescriptor(attribute => attribute
                    .Metadata(PropertyName("Foo"))
                    .Name("Foo")
                    .TypeName("System.Int32"))
                .TagMatchingRuleDescriptor(rule => rule.RequireTagName("p"))
                .Build(),
            TagHelperDescriptorBuilder.Create(ViewComponentTagHelperConventions.Kind, "TestTagHelper", "TestAssembly")
                .Metadata(
                    TypeName("__Generated__TagCloudViewComponentTagHelper"),
                    new(ViewComponentTagHelperMetadata.Name, "TagCloud"))
                .BoundAttributeDescriptor(attribute => attribute
                    .Metadata(PropertyName("Foo"))
                    .Name("Foo")
                    .TypeName("System.Int32"))
                .TagMatchingRuleDescriptor(rule => rule.RequireTagName("tagcloud"))
                .Build()
        };

        var projectEngine = CreateProjectEngine(tagHelpers);
        var pass = new ViewComponentTagHelperPass()
        {
            Engine = projectEngine.Engine,
        };

        var irDocument = CreateIRDocument(projectEngine, codeDocument);

        var vcthFullName = "AspNetCore.test.__Generated__TagCloudViewComponentTagHelper";

        // Act
        pass.Execute(codeDocument, irDocument);

        // Assert
        var outerTagHelper = FindTagHelperNode(irDocument);
        Assert.Equal("PTestTagHelper", Assert.IsType<DefaultTagHelperCreateIntermediateNode>(outerTagHelper.Children[1]).TypeName);
        Assert.Equal("Foo", Assert.IsType<DefaultTagHelperPropertyIntermediateNode>(outerTagHelper.Children[2]).PropertyName);

        var vcth = FindTagHelperNode(outerTagHelper.Children[0]);
        Assert.Equal(vcthFullName, Assert.IsType<DefaultTagHelperCreateIntermediateNode>(vcth.Children[1]).TypeName);
        Assert.Equal("Foo", Assert.IsType<DefaultTagHelperPropertyIntermediateNode>(vcth.Children[2]).PropertyName);


        var @class = FindClassNode(irDocument);
        Assert.Equal(5, @class.Children.Count);

        Assert.IsType<ViewComponentTagHelperIntermediateNode>(@class.Children.Last());
    }

    private static RazorCodeDocument CreateDocument(string content)
    {
        var source = RazorSourceDocument.Create(content, "test.cshtml");
        return RazorCodeDocument.Create(source);
    }

    private static RazorProjectEngine CreateProjectEngine(params TagHelperDescriptor[] tagHelpers)
    {
        return RazorProjectEngine.Create(b =>
        {
            b.Features.Add(new MvcViewDocumentClassifierPass());

            b.Features.Add(new TestTagHelperFeature(tagHelpers));
        });
    }

    private static DocumentIntermediateNode CreateIRDocument(RazorProjectEngine projectEngine, RazorCodeDocument codeDocument)
    {
        for (var i = 0; i < projectEngine.Phases.Count; i++)
        {
            var phase = projectEngine.Phases[i];
            phase.Execute(codeDocument);

            if (phase is IRazorDirectiveClassifierPhase)
            {
                break;
            }
        }

        // We also expect the default tag helper pass to run first.
        var documentNode = codeDocument.GetDocumentIntermediateNode();

        var defaultTagHelperPass = projectEngine.EngineFeatures.OfType<DefaultTagHelperOptimizationPass>().Single();
        defaultTagHelperPass.Execute(codeDocument, documentNode);

        return codeDocument.GetDocumentIntermediateNode();
    }

    private static ClassDeclarationIntermediateNode FindClassNode(IntermediateNode node)
    {
        var visitor = new ClassDeclarationNodeVisitor();
        visitor.Visit(node);
        return visitor.Node;
    }

    private static TagHelperIntermediateNode FindTagHelperNode(IntermediateNode node)
    {
        var visitor = new TagHelperNodeVisitor();
        visitor.Visit(node);
        return visitor.Node;
    }

    private class ClassDeclarationNodeVisitor : IntermediateNodeWalker
    {
        public ClassDeclarationIntermediateNode Node { get; set; }

        public override void VisitClassDeclaration(ClassDeclarationIntermediateNode node)
        {
            Node = node;
        }
    }

    private class TagHelperNodeVisitor : IntermediateNodeWalker
    {
        public TagHelperIntermediateNode Node { get; set; }

        public override void VisitTagHelper(TagHelperIntermediateNode node)
        {
            Node = node;
        }
    }
}
