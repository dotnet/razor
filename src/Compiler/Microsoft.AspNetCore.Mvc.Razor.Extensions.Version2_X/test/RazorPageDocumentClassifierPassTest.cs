// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions.Version2_X;

public class RazorPageDocumentClassifierPassTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Version_2_1;

    protected override void ConfigureProjectEngine(RazorProjectEngineBuilder builder)
    {
        PageDirective.Register(builder);
    }

    protected override void ConfigureProcessor(RazorCodeDocumentProcessor processor)
    {
        processor.RunPhasesTo<IRazorIntermediateNodeLoweringPhase>();
    }

    [Fact]
    public void RazorPageDocumentClassifierPass_LogsErrorForImportedPageDirectives()
    {
        // Arrange
        var sourceSpan = new SourceSpan(
            filePath: "import.cshtml",
            absoluteIndex: 0,
            lineIndex: 0,
            characterIndex: 0,
            length: 5,
            lineCount: 0,
            endCharacterIndex: 5);

        var expectedDiagnostic = RazorExtensionsDiagnosticFactory.CreatePageDirective_CannotBeImported(sourceSpan);

        var source = TestRazorSourceDocument.Create("<p>Hello World</p>", filePath: "main.cshtml");
        var importSource = TestRazorSourceDocument.Create("@page", filePath: "import.cshtml");
        var processor = CreateAndInitializeCodeDocument(source, [importSource]);

        // Act
        processor.ExecutePass<RazorPageDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var pageDirectives = documentNode.FindDirectiveReferences(PageDirective.Directive);
        var directive = Assert.Single(pageDirectives);
        var diagnostic = Assert.Single(directive.Node.Diagnostics);
        Assert.Equal(expectedDiagnostic, diagnostic);
    }

    [Fact]
    public void RazorPageDocumentClassifierPass_LogsErrorIfDirectiveNotAtTopOfFile()
    {
        // Arrange
        var sourceSpan = new SourceSpan(
            "test.cshtml",
            absoluteIndex: 14 + Environment.NewLine.Length * 2,
            lineIndex: 2,
            characterIndex: 0,
            length: 5 + Environment.NewLine.Length,
            lineCount: 1,
            endCharacterIndex: 0);

        var expectedDiagnostic = RazorExtensionsDiagnosticFactory.CreatePageDirective_MustExistAtTheTopOfFile(sourceSpan);

        var content = """
            
            @somethingelse
            @page
            
            """;
        var processor = CreateAndInitializeCodeDocument(content);

        // Act
        processor.ExecutePass<RazorPageDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var pageDirectives = documentNode.FindDirectiveReferences(PageDirective.Directive);
        var directive = Assert.Single(pageDirectives);
        var diagnostic = Assert.Single(directive.Node.Diagnostics);
        Assert.Equal(expectedDiagnostic, diagnostic);
    }

    [Fact]
    public void RazorPageDocumentClassifierPass_DoesNotLogErrorIfCommentAndWhitespaceBeforeDirective()
    {
        // Arrange
        var content = @"
@* some comment *@

@page
";
        var processor = CreateAndInitializeCodeDocument(content);

        // Act
        processor.ExecutePass<RazorPageDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var pageDirectives = documentNode.FindDirectiveReferences(PageDirective.Directive);
        var directive = Assert.Single(pageDirectives);
        Assert.Empty(directive.Node.Diagnostics);
    }

    [Fact]
    public void RazorPageDocumentClassifierPass_SetsDocumentKind()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument("@page");

        // Act
        processor.ExecutePass<RazorPageDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        Assert.Equal("mvc.1.0.razor-page", documentNode.DocumentKind);
    }

    [Fact]
    public void RazorPageDocumentClassifierPass_NoOpsIfDocumentKindIsAlreadySet()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument("@page");

        var documentNode = processor.GetDocumentNode();
        documentNode.DocumentKind = "some-value";

        // Act
        processor.ExecutePass<RazorPageDocumentClassifierPass>();

        // Assert
        Assert.Equal("some-value", documentNode.DocumentKind);
    }

    [Fact]
    public void RazorPageDocumentClassifierPass_NoOpsIfPageDirectiveIsMalformed()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument("@page+1");

        var documentNode = processor.GetDocumentNode();
        documentNode.DocumentKind = "some-value";

        // Act
        processor.ExecutePass<RazorPageDocumentClassifierPass>();

        // Assert
        Assert.Equal("some-value", documentNode.DocumentKind);
    }

    [Fact]
    public void RazorPageDocumentClassifierPass_SetsNamespace()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument("@page+1");

        // Act
        processor.ExecutePass<RazorPageDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @namespace = documentNode.FindNamespaceNode();
        Assert.NotNull(@namespace);

        Assert.Equal("AspNetCore", @namespace.Content);
    }

    [Fact]
    public void RazorPageDocumentClassifierPass_SetsClass()
    {
        // Arrange
        var source = TestRazorSourceDocument.Create("@page", filePath: "ignored", relativePath: "Test.cshtml");
        var processor = CreateAndInitializeCodeDocument(source);

        // Act
        processor.ExecutePass<RazorPageDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);

        Assert.Equal("global::Microsoft.AspNetCore.Mvc.RazorPages.Page", @class.BaseType.BaseType.Content);
        Assert.Equal(["public"], @class.Modifiers);
        Assert.Equal("Test", @class.ClassName);
    }

    [Fact]
    public void RazorPageDocumentClassifierPass_NullFilePath_SetsClass()
    {
        // Arrange
        var source = TestRazorSourceDocument.Create("@page", filePath: null, relativePath: null);
        var processor = CreateAndInitializeCodeDocument(source);

        // Act
        processor.ExecutePass<RazorPageDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);

        Assert.Equal("global::Microsoft.AspNetCore.Mvc.RazorPages.Page", @class.BaseType.BaseType.Content);
        Assert.Equal(new[] { "public" }, @class.Modifiers);
        AssertEx.Equal("AspNetCore_c3b458108610c1a2aa6eede0a5685ede853e036732db515609b2a23ca15359e1", @class.ClassName);
    }

    [Theory]
    [InlineData("/Views/Home/Index.cshtml", "_Views_Home_Index")]
    [InlineData("/Areas/MyArea/Views/Home/About.cshtml", "_Areas_MyArea_Views_Home_About")]
    public void RazorPageDocumentClassifierPass_UsesRelativePathToGenerateTypeName(string relativePath, string expected)
    {
        // Arrange
        var source = TestRazorSourceDocument.Create("@page", filePath: "ignored", relativePath: relativePath);
        var processor = CreateAndInitializeCodeDocument(source);

        // Act
        processor.ExecutePass<RazorPageDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);

        Assert.Equal(expected, @class.ClassName);
    }

    [Fact]
    public void RazorPageDocumentClassifierPass_UsesAbsolutePath_IfRelativePathIsNotSet()
    {
        // Arrange
        var source = TestRazorSourceDocument.Create("@page", filePath: @"x::\application\Views\Home\Index.cshtml", relativePath: null);
        var processor = CreateAndInitializeCodeDocument(source);

        // Act
        processor.ExecutePass<RazorPageDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);

        Assert.Equal("x___application_Views_Home_Index", @class.ClassName);
    }

    [Fact]
    public void RazorPageDocumentClassifierPass_SanitizesClassName()
    {
        // Arrange
        var source = TestRazorSourceDocument.Create("@page", filePath: @"x:\Test.cshtml", relativePath: "path.with+invalid-chars");
        var processor = CreateAndInitializeCodeDocument(source);

        // Act
        processor.ExecutePass<RazorPageDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var @class = documentNode.FindClassNode();
        Assert.NotNull(@class);

        Assert.Equal("path_with_invalid_chars", @class.ClassName);
    }

    [Fact]
    public void RazorPageDocumentClassifierPass_SetsUpExecuteAsyncMethod()
    {
        // Arrange
        var processor = CreateAndInitializeCodeDocument("@page");

        // Act
        processor.ExecutePass<RazorPageDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var method = documentNode.FindMethodNode();
        Assert.NotNull(method);

        Assert.Equal("ExecuteAsync", method.MethodName);
        Assert.Equal("global::System.Threading.Tasks.Task", method.ReturnType);
        Assert.Equal(["public", "async", "override"], method.Modifiers);
    }

    [Fact]
    public void RazorPageDocumentClassifierPass_AddsRouteTemplateMetadata()
    {
        // Arrange
        var source = TestRazorSourceDocument.Create("@page \"some-route\"", filePath: "ignored", relativePath: "Test.cshtml");
        var processor = CreateAndInitializeCodeDocument(source);

        // Act
        processor.ExecutePass<RazorPageDocumentClassifierPass>();

        // Assert
        var documentNode = processor.GetDocumentNode();

        var method = documentNode.FindExtensionNode();
        Assert.NotNull(method);

        var attributeNode = Assert.IsType<RazorCompiledItemMetadataAttributeIntermediateNode>(method);
        Assert.Equal("RouteTemplate", attributeNode.Key);
        Assert.Equal("some-route", attributeNode.Value);
    }
}
