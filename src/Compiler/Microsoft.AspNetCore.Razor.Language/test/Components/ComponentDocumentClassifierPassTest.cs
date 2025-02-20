// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.Components;

public class ComponentDocumentClassifierPassTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Latest;

    protected override void ConfigureCodeDocumentProcessor(RazorCodeDocumentProcessor processor)
    {
        processor.ExecutePhasesThrough<IRazorIntermediateNodeLoweringPhase>();
    }

    [Fact]
    public void Execute_SetsDocumentKind()
    {
        // Arrange
        var source = TestRazorSourceDocument.Create("some-content", filePath: "Test.razor");
        var codeDocument = ProjectEngine.CreateCodeDocument(source, FileKinds.Component);
        var processor = CreateCodeDocumentProcessor(codeDocument);

        // Act
        processor.ExecutePass<ComponentDocumentClassifierPass>(() => new(Version));

        // Assert
        var documentNode = processor.GetDocumentNode();

        Assert.Equal(ComponentDocumentClassifierPass.ComponentDocumentKind, documentNode.DocumentKind);
    }

    [Fact]
    public void ComponentDocumentClassifierPass_SetsNamespace()
    {
        // Arrange
        var projectEngine = CreateProjectEngine(builder =>
        {
            builder.SetRootNamespace("MyApp");
        });

        var source = TestRazorSourceDocument.Create("some-content", filePath: "/MyApp/Test.razor", relativePath: "Test.razor");
        var codeDocument = projectEngine.CreateCodeDocument(source, FileKinds.Component);
        var processor = CreateCodeDocumentProcessor(projectEngine, codeDocument);

        // Act
        processor.ExecutePass<ComponentDocumentClassifierPass>(() => new(Version));

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @namespace = documentNode.FindNamespaceNode();
        Assert.NotNull(@namespace);

        Assert.Equal("MyApp", @namespace.Content);
    }

    [Fact]
    public void ComponentDocumentClassifierPass_SetsClass()
    {
        // Arrange
        var projectEngine = CreateProjectEngine(builder =>
        {
            builder.SetRootNamespace("MyApp");
        });

        var source = TestRazorSourceDocument.Create("some-content", filePath: "/MyApp/Test.razor", relativePath: "Test.razor");
        var codeDocument = projectEngine.CreateCodeDocument(source, FileKinds.Component);
        var processor = CreateCodeDocumentProcessor(projectEngine, codeDocument);

        // Act
        processor.ExecutePass<ComponentDocumentClassifierPass>(() => new(Version));

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);

        Assert.Equal($"global::{ComponentsApi.ComponentBase.FullTypeName}", @class.BaseType.BaseType.Content);
        Assert.Equal(["public", "partial"], @class.Modifiers);
        Assert.Equal("Test", @class.ClassName);
    }

    [Fact]
    public void ComponentDocumentClassifierPass_UsesRelativePathToGenerateTypeNameAndNamespace()
    {
        // Arrange
        var projectEngine = CreateProjectEngine(builder =>
        {
            builder.SetRootNamespace("MyApp");
        });

        var relativePath = "/Pages/Announcements/Banner.razor";
        var source = TestRazorSourceDocument.Create("some-content", filePath: $"/MyApp{relativePath}", relativePath: relativePath);
        var codeDocument = projectEngine.CreateCodeDocument(source, FileKinds.Component);
        var processor = CreateCodeDocumentProcessor(projectEngine, codeDocument);

        // Act
        processor.ExecutePass<ComponentDocumentClassifierPass>(() => new(Version));

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);

        var @namespace = documentNode.FindNamespaceNode();
        Assert.NotNull(@namespace);

        Assert.Equal("Banner", @class.ClassName);
        Assert.Equal("MyApp.Pages.Announcements", @namespace.Content);
    }

    [Fact]
    public void ComponentDocumentClassifierPass_SanitizesClassName()
    {
        // Arrange
        var projectEngine = CreateProjectEngine(builder =>
        {
            builder.SetRootNamespace("My.+App");
        });

        var source = TestRazorSourceDocument.Create("some-content", filePath: @"x:\My.+App\path.with+invalid-chars.razor", relativePath: "path.with+invalid-chars.razor");
        var codeDocument = projectEngine.CreateCodeDocument(source, FileKinds.Component);
        var processor = CreateCodeDocumentProcessor(projectEngine, codeDocument);

        // Act
        processor.ExecutePass<ComponentDocumentClassifierPass>(() => new(Version));

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);

        var @namespace = documentNode.FindNamespaceNode();
        Assert.NotNull(@namespace);

        Assert.Equal("path_with_invalid_chars", @class.ClassName);
        Assert.Equal("My._App", @namespace.Content);
    }

    [Fact]
    public void ComponentDocumentClassifierPass_SetsUpMainMethod()
    {
        // Arrange
        var source = TestRazorSourceDocument.Create("some-content", filePath: "Test.razor");
        var codeDocument = ProjectEngine.CreateCodeDocument(source, FileKinds.Component);
        var processor = CreateCodeDocumentProcessor(codeDocument);

        // Act
        processor.ExecutePass<ComponentDocumentClassifierPass>(() => new(Version));

        // Assert
        var documentNode = processor.GetDocumentNode();
        var method = documentNode.FindMethodNode();
        Assert.NotNull(method);

        Assert.Equal(ComponentsApi.ComponentBase.BuildRenderTree, method.MethodName);
        Assert.Equal("void", method.ReturnType);
        Assert.Equal(["protected", "override"], method.Modifiers);
    }
}
