// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions.Version2_X;

public class ViewComponentTagHelperPassTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Version_2_1;

    protected override void ConfigureProjectEngine(RazorProjectEngineBuilder builder)
    {
        builder.Features.Add(new MvcViewDocumentClassifierPass());
    }

    protected override void ConfigureProcessor(RazorCodeDocumentProcessor processor)
    {
        processor.RunPhasesTo<IRazorDirectiveClassifierPhase>();

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

        var processor = CreateAndInitializeCodeDocument(@"
@addTagHelper TestTagHelper, TestAssembly
<p foo=""17"">",
            [tagHelper]);

        // Act
        processor.ExecutePass<ViewComponentTagHelperPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

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

        var processor = CreateAndInitializeCodeDocument(@"
@addTagHelper TestTagHelper, TestAssembly
<tagcloud foo=""17"">",
            [tagHelper]);

        // Act
        processor.ExecutePass<ViewComponentTagHelperPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var tagHelperNode = documentNode.FindTagHelperNode();
        Assert.NotNull(tagHelperNode);

        Assert.Equal(
            "AspNetCore.test.__Generated__TagCloudViewComponentTagHelper",
            Assert.IsType<DefaultTagHelperCreateIntermediateNode>(tagHelperNode.Children[1]).TypeName);
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

        var processor = CreateAndInitializeCodeDocument(@"
@addTagHelper TestTagHelper, TestAssembly
<tagcloud tag-foo=""17"">",
            [tagHelper]);

        // Act
        processor.ExecutePass<ViewComponentTagHelperPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var tagHelperNode = documentNode.FindTagHelperNode();
        Assert.NotNull(tagHelperNode);

        Assert.Equal(
            "AspNetCore.test.__Generated__TagCloudViewComponentTagHelper",
            Assert.IsType<DefaultTagHelperCreateIntermediateNode>(tagHelperNode.Children[1]).TypeName);
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

        var processor = CreateAndInitializeCodeDocument(@"
@addTagHelper *, TestAssembly
<p foo=""17""><tagcloud foo=""17""></p>",
            [tagHelper1, tagHelper2]);

        // Act
        processor.ExecutePass<ViewComponentTagHelperPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var outerTagHelperNode = documentNode.FindTagHelperNode();
        Assert.NotNull(outerTagHelperNode);

        Assert.Equal("PTestTagHelper", Assert.IsType<DefaultTagHelperCreateIntermediateNode>(outerTagHelperNode.Children[1]).TypeName);
        Assert.Equal("Foo", Assert.IsType<DefaultTagHelperPropertyIntermediateNode>(outerTagHelperNode.Children[2]).PropertyName);

        var vcth = outerTagHelperNode.Children[0].FindTagHelperNode();
        Assert.NotNull(vcth);

        Assert.Equal(
            "AspNetCore.test.__Generated__TagCloudViewComponentTagHelper",
            Assert.IsType<DefaultTagHelperCreateIntermediateNode>(vcth.Children[1]).TypeName);
        Assert.Equal("Foo", Assert.IsType<DefaultTagHelperPropertyIntermediateNode>(vcth.Children[2]).PropertyName);

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);

        Assert.Equal(5, @class.Children.Count);
        Assert.IsType<ViewComponentTagHelperIntermediateNode>(@class.Children.Last());
    }
}
