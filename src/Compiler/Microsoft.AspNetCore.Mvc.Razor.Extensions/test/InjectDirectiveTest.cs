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

    [Fact]
    public void InjectDirectivePass_Execute_DefinesProperty()
    {
        // Arrange
        var projectEngine = CreateProjectEngine();

        var codeDocument = projectEngine.CreateCodeDocument(@"
@inject PropertyType PropertyName
");
        var runner = RazorProjectEngineRunner.From(projectEngine, codeDocument);

        runner.RunPhasesTo<IRazorDocumentClassifierPhase>();

        // Act
        runner.ExecutePass<InjectDirective.Pass>();

        var documentNode = codeDocument.GetDocumentIntermediateNode();

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
        var projectEngine = CreateProjectEngine();

        var codeDocument = projectEngine.CreateCodeDocument(@"
@inject PropertyType PropertyName
@inject PropertyType2 PropertyName
");

        var runner = RazorProjectEngineRunner.From(projectEngine, codeDocument);

        runner.RunPhasesTo<IRazorDocumentClassifierPhase>();

        // Act
        runner.ExecutePass<InjectDirective.Pass>();

        var documentNode = codeDocument.GetDocumentIntermediateNode();

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
        var projectEngine = CreateProjectEngine();

        var codeDocument = projectEngine.CreateCodeDocument(@"
@inject PropertyType<TModel> PropertyName
");

        var runner = RazorProjectEngineRunner.From(projectEngine, codeDocument);

        runner.RunPhasesTo<IRazorDocumentClassifierPhase>();

        // Act
        runner.ExecutePass<InjectDirective.Pass>();

        var documentNode = codeDocument.GetDocumentIntermediateNode();

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
        var projectEngine = CreateProjectEngine();

        var codeDocument = projectEngine.CreateCodeDocument(@"
@model ModelType
@inject PropertyType<TModel> PropertyName
");

        var runner = RazorProjectEngineRunner.From(projectEngine, codeDocument);

        runner.RunPhasesTo<IRazorDocumentClassifierPhase>();

        // Act
        runner.ExecutePass<InjectDirective.Pass>();

        var documentNode = codeDocument.GetDocumentIntermediateNode();

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
        var projectEngine = CreateProjectEngine();

        var codeDocument = projectEngine.CreateCodeDocument(@"
@inject PropertyType<TModel> PropertyName
@model ModelType
");

        var runner = RazorProjectEngineRunner.From(projectEngine, codeDocument);

        runner.RunPhasesTo<IRazorDocumentClassifierPhase>();

        // Act
        runner.ExecutePass<InjectDirective.Pass>();

        var documentNode = codeDocument.GetDocumentIntermediateNode();

        // Assert
        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);
        Assert.Equal(2, @class.Children.Count);

        var node = Assert.IsType<InjectIntermediateNode>(@class.Children[1]);
        Assert.Equal("PropertyType<ModelType>", node.TypeName);
        Assert.Equal("PropertyName", node.MemberName);
    }
}
