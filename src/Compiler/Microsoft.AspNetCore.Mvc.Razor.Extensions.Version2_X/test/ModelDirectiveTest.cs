// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions.Version2_X;

public class ModelDirectiveTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Version_2_1;

    protected override void ConfigureProjectEngine(RazorProjectEngineBuilder builder)
    {
        // Notice we're not registering the ModelDirective.Pass here so we can run it on demand.
        builder.AddDirective(ModelDirective.Directive);

        // There's some special interaction with the inherits directive
        InheritsDirective.Register(builder);

        builder.Features.Add(new RazorPageDocumentClassifierPass());
        builder.Features.Add(new MvcViewDocumentClassifierPass());
    }

    protected override void ConfigureProcessor(RazorCodeDocumentProcessor processor)
    {
        processor.RunPhasesTo<IRazorDocumentClassifierPhase>();

        // InheritsDirectivePass needs to run before ModelDirective.
        processor.ExecutePass<InheritsDirectivePass>();
    }

    [Fact]
    public void ModelDirective_GetModelType_GetsTypeFromFirstWellFormedDirective()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument(@"
@model Type1
@model Type2
@model
");

        var documentNode = processor.GetDocumentNode();

        // Act
        var result = ModelDirective.GetModelType(documentNode);

        // Assert
        Assert.Equal("Type1", result);
    }

    [Fact]
    public void ModelDirective_GetModelType_DefaultsToDynamic()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument(@" ");

        var documentNode = processor.GetDocumentNode();

        // Act
        var result = ModelDirective.GetModelType(documentNode);

        // Assert
        Assert.Equal("dynamic", result);
    }

    [Fact]
    public void ModelDirectivePass_Execute_ReplacesTModelInBaseType()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument(@"
@inherits BaseType<TModel>
@model Type1
");

        // Act
        processor.ExecutePass<ModelDirective.Pass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);
        var baseType = @class.BaseType;

        Assert.Equal("BaseType", baseType.BaseType.Content);
        Assert.NotNull(baseType.BaseType.Source);

        Assert.NotNull(baseType.ModelType);
        Assert.Equal("Type1", baseType.ModelType.Content);
        Assert.NotNull(baseType.ModelType.Source);
    }

    [Fact]
    public void ModelDirectivePass_Execute_ReplacesTModelInBaseType_DifferentOrdering()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument(@"
@model Type1
@inherits BaseType<TModel>
@model Type2
");

        // Act
        processor.ExecutePass<ModelDirective.Pass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);
        var baseType = @class.BaseType;

        Assert.Equal("BaseType", baseType.BaseType.Content);
        Assert.NotNull(baseType.BaseType.Source);

        Assert.NotNull(baseType.ModelType);
        Assert.Equal("Type1", baseType.ModelType.Content);
        Assert.NotNull(baseType.ModelType.Source);
    }

    [Fact]
    public void ModelDirectivePass_Execute_NoOpWithoutTModel()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument(@"
@inherits BaseType
@model Type1
");

        // Act
        processor.ExecutePass<ModelDirective.Pass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);
        var baseType = @class.BaseType;

        Assert.Equal("BaseType", baseType.BaseType.Content);
        Assert.NotNull(baseType.BaseType.Source);

        // ISSUE: https://github.com/dotnet/razor/issues/10987 we don't issue a warning or emit anything for the unused model
        Assert.Null(baseType.ModelType);
    }

    [Fact]
    public void ModelDirectivePass_Execute_ReplacesTModelInBaseType_DefaultDynamic()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument(@"
@inherits BaseType<TModel>
");

        // Act
        processor.ExecutePass<ModelDirective.Pass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);
        var baseType = @class.BaseType;

        Assert.Equal("BaseType", baseType.BaseType.Content);
        Assert.NotNull(baseType.BaseType.Source);

        Assert.NotNull(baseType.ModelType);
        Assert.Equal("dynamic", baseType.ModelType.Content);
        Assert.Null(baseType.ModelType.Source);
    }

    [Fact]
    public void ModelDirectivePass_DesignTime_AddsTModelUsingDirective()
    {
        // Arrange
        var processor = CreateAndInitializeDesignTimeCodeDocument(@"
@inherits BaseType<TModel>
");

        // Act
        processor.ExecutePass<ModelDirective.Pass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);
        var baseType = @class.BaseType;

        Assert.Equal("BaseType", baseType.BaseType.Content);
        Assert.Null(baseType.BaseType.Source);

        Assert.NotNull(baseType.ModelType);
        Assert.Equal("dynamic", baseType.ModelType.Content);
        Assert.Null(baseType.ModelType.Source);

        var @namespace = documentNode.FindNamespaceNode();
        Assert.NotNull(@namespace);
        var usingNode = Assert.IsType<UsingDirectiveIntermediateNode>(@namespace.Children[0]);
        Assert.Equal($"TModel = global::{typeof(object).FullName}", usingNode.Content);
    }

    [Fact]
    public void ModelDirectivePass_DesignTime_WithModel_AddsTModelUsingDirective()
    {
        // Arrange
        var processor = CreateAndInitializeDesignTimeCodeDocument(@"
@inherits BaseType<TModel>
@model SomeType
");

        // Act
        processor.ExecutePass<ModelDirective.Pass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);
        var baseType = @class.BaseType;

        Assert.Equal("BaseType", baseType.BaseType.Content);
        Assert.Null(baseType.BaseType.Source);

        Assert.NotNull(baseType.ModelType);
        Assert.Equal("SomeType", baseType.ModelType.Content);
        Assert.Null(baseType.ModelType.Source);

        var @namespace = documentNode.FindNamespaceNode();
        Assert.NotNull(@namespace);
        var usingNode = Assert.IsType<UsingDirectiveIntermediateNode>(@namespace.Children[0]);
        Assert.Equal($"TModel = global::System.Object", usingNode.Content);
    }
}
