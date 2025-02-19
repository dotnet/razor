// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions.Version2_X;

public class MvcViewDocumentClassifierPassTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Version_2_1;

    protected override void ConfigureProcessor(RazorCodeDocumentProcessor processor)
    {
        processor.RunPhasesTo<IRazorIntermediateNodeLoweringPhase>();
    }

    [Fact]
    public void MvcViewDocumentClassifierPass_SetsDocumentKind()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument("some-content");

        // Act
        processor.ExecutePass<MvcViewDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        Assert.Equal("mvc.1.0.view", documentNode.DocumentKind);
    }

    [Fact]
    public void MvcViewDocumentClassifierPass_NoOpsIfDocumentKindIsAlreadySet()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument("some-content");

        var documentNode = processor.GetDocumentNode();
        documentNode.DocumentKind = "some-value";

        // Act
        processor.ExecutePass<MvcViewDocumentClassifierPass>();

        // Assert
        Assert.Equal("some-value", documentNode.DocumentKind);
    }

    [Fact]
    public void MvcViewDocumentClassifierPass_SetsNamespace()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument("some-content");

        // Act
        processor.ExecutePass<MvcViewDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @namespace = documentNode.FindNamespaceNode();
        Assert.NotNull(@namespace);
        Assert.Equal("AspNetCore", @namespace.Content);
    }

    [Fact]
    public void MvcViewDocumentClassifierPass_SetsClass()
    {
        // Arrange
        var source = TestRazorSourceDocument.Create("some-content", filePath: "ignored", relativePath: "Test.cshtml");
        var processor = CreateAndInitializeCodeDocument(source);

        // Act
        processor.ExecutePass<MvcViewDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);

        var baseNode = Assert.IsType<BaseTypeWithModel>(@class.BaseType);
        Assert.Equal("global::Microsoft.AspNetCore.Mvc.Razor.RazorPage", baseNode.BaseType.Content);
        Assert.NotNull(baseNode.ModelType);
        Assert.Equal("TModel", baseNode.ModelType.Content);
        Assert.Equal(["public"], @class.Modifiers);
        Assert.Equal("Test", @class.ClassName);
    }

    [Fact]
    public void MvcViewDocumentClassifierPass_NullFilePath_SetsClass()
    {
        // Arrange
        var source = TestRazorSourceDocument.Create("some-content", filePath: null, relativePath: null);
        var processor = CreateAndInitializeCodeDocument(source);

        // Act
        processor.ExecutePass<MvcViewDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);

        var baseNode = Assert.IsType<BaseTypeWithModel>(@class.BaseType);
        Assert.Equal("global::Microsoft.AspNetCore.Mvc.Razor.RazorPage", baseNode.BaseType.Content);
        Assert.NotNull(baseNode.ModelType);
        Assert.Equal("TModel", baseNode.ModelType.Content);
        Assert.Equal(["public"], @class.Modifiers);
        AssertEx.Equal("AspNetCore_ec563e63d931b806184cb02f79875e4f3b21d1ca043ad06699424459128b58c0", @class.ClassName);
    }

    [Theory]
    [InlineData("/Views/Home/Index.cshtml", "_Views_Home_Index")]
    [InlineData("/Areas/MyArea/Views/Home/About.cshtml", "_Areas_MyArea_Views_Home_About")]
    public void MvcViewDocumentClassifierPass_UsesRelativePathToGenerateTypeName(string relativePath, string expected)
    {
        // Arrange
        var source = TestRazorSourceDocument.Create("some-content", filePath: "ignored", relativePath: relativePath);
        var processor = CreateAndInitializeCodeDocument(source);

        // Act
        processor.ExecutePass<MvcViewDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);

        Assert.Equal(expected, @class.ClassName);
    }

    [Fact]
    public void MvcViewDocumentClassifierPass_UsesAbsolutePath_IfRelativePathIsNotSet()
    {
        // Arrange
        var source = TestRazorSourceDocument.Create("some-content", filePath: @"x::\application\Views\Home\Index.cshtml", relativePath: null);
        var processor = CreateAndInitializeCodeDocument(source);

        // Act
        processor.ExecutePass<MvcViewDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);

        Assert.Equal("x___application_Views_Home_Index", @class.ClassName);
    }

    [Fact]
    public void MvcViewDocumentClassifierPass_SanitizesClassName()
    {
        // Arrange
        var source = TestRazorSourceDocument.Create("@page", filePath: @"x:\Test.cshtml", relativePath: "path.with+invalid-chars");
        var processor = CreateAndInitializeCodeDocument(source);

        // Act
        processor.ExecutePass<MvcViewDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);

        Assert.Equal("path_with_invalid_chars", @class.ClassName);
    }

    [Fact]
    public void MvcViewDocumentClassifierPass_SetsUpExecuteAsyncMethod()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument("some-content");

        // Act
        processor.ExecutePass<MvcViewDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var method = documentNode.FindMethodNode();
        Assert.NotNull(method);

        Assert.Equal("ExecuteAsync", method.MethodName);
        Assert.Equal("global::System.Threading.Tasks.Task", method.ReturnType);
        Assert.Equal(["public", "async", "override"], method.Modifiers);
    }
}
