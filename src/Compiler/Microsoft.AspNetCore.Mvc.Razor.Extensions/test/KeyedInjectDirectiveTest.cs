// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

public class KeyedInjectDirectiveTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Version_3_0;

    protected override void ConfigureProjectEngine(RazorProjectEngineBuilder builder)
    {
        // Notice we're not registering the InjectDirective.Pass here so we can run it on demand.
        builder.AddDirective(InjectDirective.Directive);
        builder.AddDirective(KeyedInjectDirective.Directive);
        builder.AddDirective(ModelDirective.Directive);

        builder.Features.Add(new RazorPageDocumentClassifierPass());
        builder.Features.Add(new MvcViewDocumentClassifierPass());
    }

    protected override void ConfigureCodeDocumentProcessor(RazorCodeDocumentProcessor processor)
    {
        processor.ExecutePhasesThrough<IRazorDocumentClassifierPhase>();
    }

    [Fact]
    public void KeyedInjectDirectivePass_Execute_DefinesProperty()
    {
        // Arrange
        var codeDocument = ProjectEngine.CreateCodeDocument(@"
@keyedinject PropertyType PropertyName ""PropertyKey""
");
        var processor = CreateCodeDocumentProcessor(codeDocument);

        // Act
        processor.ExecutePass<KeyedInjectDirective.Pass>();

        // Assert
        var documentNode = processor.GetDocumentNode();
        var classNode = documentNode.GetClassNode();

        Assert.Equal(2, classNode.Children.Count);

        var node = Assert.IsType<KeyedInjectIntermediateNode>(classNode.Children[1]);
        Assert.Equal("PropertyType", node.TypeName);
        Assert.Equal("PropertyName", node.MemberName);
        Assert.Equal("\"PropertyKey\"", node.KeyName);
    }
    
    [Fact]
    public void KeyedInjectDirectivePass_Execute_DedupesPropertiesByName()
    {
        // Arrange
        var codeDocument = ProjectEngine.CreateCodeDocument(@"
@keyedinject PropertyType PropertyName ""SomeKey""
@keyedinject PropertyType2 PropertyName ""SomeKey2""
");
        var processor = CreateCodeDocumentProcessor(codeDocument);

        // Act
        processor.ExecutePass<KeyedInjectDirective.Pass>();

        // Assert
        var documentNode = processor.GetDocumentNode();
        var classNode = documentNode.GetClassNode();

        Assert.Equal(2, classNode.Children.Count);

        var node = Assert.IsType<KeyedInjectIntermediateNode>(classNode.Children[1]);
        Assert.Equal("PropertyType2", node.TypeName);
        Assert.Equal("PropertyName", node.MemberName);
        Assert.Equal("\"SomeKey2\"", node.KeyName);
    }

    [Fact]
    public void KeyedInjectDirectivePass_Execute_ExpandsTModel_WithDynamic()
    {
        // Arrange
        var codeDocument = ProjectEngine.CreateCodeDocument(@"
@keyedinject PropertyType<TModel> PropertyName ""SomeKey""
");
        var processor = CreateCodeDocumentProcessor(codeDocument);

        // Act
        processor.ExecutePass<KeyedInjectDirective.Pass>();

        // Assert
        var documentNode = processor.GetDocumentNode();
        var classNode = documentNode.GetClassNode();

        Assert.Equal(2, classNode.Children.Count);

        var node = Assert.IsType<KeyedInjectIntermediateNode>(classNode.Children[1]);
        Assert.Equal("PropertyType<dynamic>", node.TypeName);
        Assert.Equal("PropertyName", node.MemberName);
        Assert.Equal("\"SomeKey\"", node.KeyName);
    }

    [Fact]
    public void KeyedInjectDirectivePass_Execute_ExpandsTModel_WithModelTypeFirst()
    {
        // Arrange
        var codeDocument = ProjectEngine.CreateCodeDocument(@"
@model ModelType
@keyedinject PropertyType<TModel> PropertyName ""SomeKey""
");
        var processor = CreateCodeDocumentProcessor(codeDocument);

        // Act
        processor.ExecutePass<KeyedInjectDirective.Pass>();

        // Assert
        var documentNode = processor.GetDocumentNode();
        var classNode = documentNode.GetClassNode();

        Assert.Equal(2, classNode.Children.Count);

        var node = Assert.IsType<KeyedInjectIntermediateNode>(classNode.Children[1]);
        Assert.Equal("PropertyType<ModelType>", node.TypeName);
        Assert.Equal("PropertyName", node.MemberName);
        Assert.Equal("\"SomeKey\"", node.KeyName);
    }

    [Fact]
    public void KeyedInjectDirectivePass_Execute_ExpandsTModel_WithModelType()
    {
        // Arrange
        var codeDocument = ProjectEngine.CreateCodeDocument(@"
@keyedinject PropertyType<TModel> PropertyName ""SomeKey""
@model ModelType
");
        var processor = CreateCodeDocumentProcessor(codeDocument);

        // Act
        processor.ExecutePass<KeyedInjectDirective.Pass>();

        // Assert
        var documentNode = processor.GetDocumentNode();
        var classNode = documentNode.GetClassNode();

        Assert.Equal(2, classNode.Children.Count);

        var node = Assert.IsType<KeyedInjectIntermediateNode>(classNode.Children[1]);
        Assert.Equal("PropertyType<ModelType>", node.TypeName);
        Assert.Equal("PropertyName", node.MemberName);
        Assert.Equal("\"SomeKey\"", node.KeyName);
    }
}
