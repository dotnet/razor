// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

public class InjectDirectiveTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Version_3_0;

    protected override void ConfigureProjectEngine(RazorProjectEngineBuilder builder)
    {
        // Notice we're not registering the InjectDirective.Pass here so we can run it on demand.
        builder.AddDirective(InjectDirective.Directive);
        builder.AddDirective(ModelDirective.Directive);

        builder.Features.Add(new RazorPageDocumentClassifierPass());
        builder.Features.Add(new MvcViewDocumentClassifierPass());
    }

    protected override void ConfigureProcessor(RazorCodeDocumentProcessor processor)
    {
        processor.RunPhasesTo<IRazorDocumentClassifierPhase>();
    }

    [Fact]
    public void InjectDirectivePass_Execute_DefinesProperty()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument(@"
@inject PropertyType PropertyName
");

        // Act
        processor.ExecutePass<InjectDirective.Pass>();

        var documentNode = processor.GetDocumentNode();

        // Assert
        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);
        Assert.Equal(2, @class.Children.Count);

        var node = Assert.IsType<InjectIntermediateNode>(@class.Children[1]);
        Assert.Equal("PropertyType", node.TypeName);
        Assert.Equal("PropertyName", node.MemberName);
    }

    [Fact]
    public void InjectDirectivePass_Execute_DedupesPropertiesByName()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument(@"
@inject PropertyType PropertyName
@inject PropertyType2 PropertyName
");

        // Act
        processor.ExecutePass<InjectDirective.Pass>();

        var documentNode = processor.GetDocumentNode();

        // Assert
        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);
        Assert.Equal(2, @class.Children.Count);

        var node = Assert.IsType<InjectIntermediateNode>(@class.Children[1]);
        Assert.Equal("PropertyType2", node.TypeName);
        Assert.Equal("PropertyName", node.MemberName);
    }

    [Fact]
    public void InjectDirectivePass_Execute_ExpandsTModel_WithDynamic()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument(@"
@inject PropertyType<TModel> PropertyName
");

        // Act
        processor.ExecutePass<InjectDirective.Pass>();

        var documentNode = processor.GetDocumentNode();

        // Assert
        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);
        Assert.Equal(2, @class.Children.Count);

        var node = Assert.IsType<InjectIntermediateNode>(@class.Children[1]);
        Assert.Equal("PropertyType<dynamic>", node.TypeName);
        Assert.Equal("PropertyName", node.MemberName);
    }

    [Fact]
    public void InjectDirectivePass_Execute_ExpandsTModel_WithModelTypeFirst()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument(@"
@model ModelType
@inject PropertyType<TModel> PropertyName
");

        // Act
        processor.ExecutePass<InjectDirective.Pass>();

        var documentNode = processor.GetDocumentNode();

        // Assert
        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);
        Assert.Equal(2, @class.Children.Count);

        var node = Assert.IsType<InjectIntermediateNode>(@class.Children[1]);
        Assert.Equal("PropertyType<ModelType>", node.TypeName);
        Assert.Equal("PropertyName", node.MemberName);
    }

    [Fact]
    public void InjectDirectivePass_Execute_ExpandsTModel_WithModelType()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument(@"
@inject PropertyType<TModel> PropertyName
@model ModelType
");

        // Act
        processor.ExecutePass<InjectDirective.Pass>();

        var documentNode = processor.GetDocumentNode();

        // Assert
        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);
        Assert.Equal(2, @class.Children.Count);

        var node = Assert.IsType<InjectIntermediateNode>(@class.Children[1]);
        Assert.Equal("PropertyType<ModelType>", node.TypeName);
        Assert.Equal("PropertyName", node.MemberName);
    }
}
