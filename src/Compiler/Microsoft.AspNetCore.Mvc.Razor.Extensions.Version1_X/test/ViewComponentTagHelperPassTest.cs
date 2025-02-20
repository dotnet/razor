// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions.Version1_X;

public class ViewComponentTagHelperPassTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Version_1_1;

    protected override void ConfigureProjectEngine(RazorProjectEngineBuilder builder)
    {
        builder.Features.Add(new MvcViewDocumentClassifierPass());
    }

    protected override void ConfigureCodeDocumentProcessor(RazorCodeDocumentProcessor processor)
    {
        processor.ExecutePhasesThrough<IRazorDocumentClassifierPhase>();

        // We also expect the default tag helper pass to run first.
        processor.ExecutePass<DefaultTagHelperOptimizationPass>();
    }

    [Fact]
    public void ViewComponentTagHelperPass_Execute_IgnoresRegularTagHelper()
    {
        // Arrange
        var tagHelper = TagHelperDescriptorBuilder.Create("TestTagHelper", "TestAssembly")
            .Metadata(TypeName("TestTagHelper"))
            .BoundAttributeDescriptor(attribute => attribute
                .Name("Foo")
                .TypeName("System.Int32"))
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("p"))
            .Build();

        var codeDocument = ProjectEngine.CreateCodeDocument(@"
@addTagHelper TestTagHelper, TestAssembly
<p foo=""17"">",
            [tagHelper]);

        var processor = CreateCodeDocumentProcessor(codeDocument);

        // Act
        processor.ExecutePass<ViewComponentTagHelperPass>();
        var documentNode = processor.GetDocumentNode();

        // Assert
        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);
        Assert.Equal(3, @class.Children.Count); // No class node created for a VCTH

        foreach (var child in @class.Children)
        {
            Assert.IsNotType<ViewComponentTagHelperIntermediateNode>(child);
        }
    }

    [Fact]
    public void ViewComponentTagHelperPass_Execute_CreatesViewComponentTagHelper()
    {
        // Arrange
        var tagHelper = TagHelperDescriptorBuilder.Create(ViewComponentTagHelperConventions.Kind, "TestTagHelper", "TestAssembly")
            .Metadata(
                TypeName("__Generated__TagCloudViewComponentTagHelper"),
                new(ViewComponentTagHelperMetadata.Name, "TagCloud"))
            .BoundAttributeDescriptor(attribute => attribute
                .Name("Foo")
                .TypeName("System.Int32")
                .Metadata(PropertyName("Foo")))
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("tagcloud"))
            .Build();

        var codeDocument = ProjectEngine.CreateCodeDocument(@"
@addTagHelper TestTagHelper, TestAssembly
<tagcloud foo=""17"">",
            [tagHelper]);

        var processor = CreateCodeDocumentProcessor(codeDocument);

        // Act
        processor.ExecutePass<ViewComponentTagHelperPass>();
        var documentNode = processor.GetDocumentNode();

        // Assert
        var tagHelperNode = documentNode.FindTagHelperNode();
        Assert.NotNull(tagHelperNode);
        Assert.Equal("AspNetCore.test.__Generated__TagCloudViewComponentTagHelper", Assert.IsType<DefaultTagHelperCreateIntermediateNode>(tagHelperNode.Children[1]).TypeName);
        Assert.Equal("Foo", Assert.IsType<DefaultTagHelperPropertyIntermediateNode>(tagHelperNode.Children[2]).PropertyName);


        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);
        Assert.Equal(4, @class.Children.Count);

        Assert.IsType<ViewComponentTagHelperIntermediateNode>(@class.Children.Last());
    }

    [Fact]
    public void ViewComponentTagHelperPass_Execute_CreatesViewComponentTagHelper_WithIndexer()
    {
        // Arrange
        var tagHelper = TagHelperDescriptorBuilder.Create(ViewComponentTagHelperConventions.Kind, "TestTagHelper", "TestAssembly")
            .Metadata(
                TypeName("__Generated__TagCloudViewComponentTagHelper"),
                new(ViewComponentTagHelperMetadata.Name, "TagCloud"))
            .BoundAttributeDescriptor(attribute => attribute
                .Name("Foo")
                .TypeName("System.Collections.Generic.Dictionary<System.String, System.Int32>")
                .Metadata(PropertyName("Tags"))
                .AsDictionaryAttribute("foo-", "System.Int32"))
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("tagcloud"))
            .Build();

        var codeDocument = ProjectEngine.CreateCodeDocument(@"
@addTagHelper TestTagHelper, TestAssembly
<tagcloud tag-foo=""17"">",
            [tagHelper]);

        var processor = CreateCodeDocumentProcessor(codeDocument);

        // Act
        processor.ExecutePass<ViewComponentTagHelperPass>();
        var documentNode = processor.GetDocumentNode();

        // Assert
        var tagHelperNode = documentNode.FindTagHelperNode();
        Assert.NotNull(tagHelperNode);
        Assert.Equal("AspNetCore.test.__Generated__TagCloudViewComponentTagHelper", Assert.IsType<DefaultTagHelperCreateIntermediateNode>(tagHelperNode.Children[1]).TypeName);
        Assert.IsType<DefaultTagHelperHtmlAttributeIntermediateNode>(tagHelperNode.Children[2]);

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);
        Assert.Equal(4, @class.Children.Count);

        Assert.IsType<ViewComponentTagHelperIntermediateNode>(@class.Children[3]);
    }

    [Fact]
    public void ViewComponentTagHelperPass_Execute_CreatesViewComponentTagHelper_Nested()
    {
        // Arrange
        var tagHelper1 = TagHelperDescriptorBuilder.Create("PTestTagHelper", "TestAssembly")
            .Metadata(TypeName("PTestTagHelper"))
            .BoundAttributeDescriptor(attribute => attribute
                .Metadata(PropertyName("Foo"))
                .Name("Foo")
                .TypeName("System.Int32"))
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("p"))
            .Build();

        var tagHelper2 = TagHelperDescriptorBuilder.Create(ViewComponentTagHelperConventions.Kind, "TestTagHelper", "TestAssembly")
            .Metadata(
                TypeName("__Generated__TagCloudViewComponentTagHelper"),
                new(ViewComponentTagHelperMetadata.Name, "TagCloud"))
            .BoundAttributeDescriptor(attribute => attribute
                .Metadata(PropertyName("Foo"))
                .Name("Foo")
                .TypeName("System.Int32"))
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("tagcloud"))
            .Build();

        var codeDocument = ProjectEngine.CreateCodeDocument(@"
@addTagHelper *, TestAssembly
<p foo=""17""><tagcloud foo=""17""></p>",
            [tagHelper1, tagHelper2]);

        var processor = CreateCodeDocumentProcessor(codeDocument);

        // Act
        processor.ExecutePass<ViewComponentTagHelperPass>();
        var documentNode = processor.GetDocumentNode();

        // Assert
        var outerTagHelper = documentNode.FindTagHelperNode();
        Assert.NotNull(outerTagHelper);
        Assert.Equal("PTestTagHelper", Assert.IsType<DefaultTagHelperCreateIntermediateNode>(outerTagHelper.Children[1]).TypeName);
        Assert.Equal("Foo", Assert.IsType<DefaultTagHelperPropertyIntermediateNode>(outerTagHelper.Children[2]).PropertyName);

        var viewComponentTagHelper = outerTagHelper.Children[0].FindTagHelperNode();
        Assert.NotNull(viewComponentTagHelper);
        Assert.Equal(
            "AspNetCore.test.__Generated__TagCloudViewComponentTagHelper",
            Assert.IsType<DefaultTagHelperCreateIntermediateNode>(viewComponentTagHelper.Children[1]).TypeName);
        Assert.Equal("Foo", Assert.IsType<DefaultTagHelperPropertyIntermediateNode>(viewComponentTagHelper.Children[2]).PropertyName);

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);
        Assert.Equal(5, @class.Children.Count);

        Assert.IsType<ViewComponentTagHelperIntermediateNode>(@class.Children.Last());
    }
}
