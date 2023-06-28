﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions.Version1_X;

public class MvcViewDocumentClassifierPassTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Version_1_1;

    [Fact]
    public void MvcViewDocumentClassifierPass_SetsDocumentKind()
    {
        // Arrange
        var codeDocument = RazorCodeDocument.Create(RazorSourceDocument.Create("some-content", "Test.cshtml"));

        var projectEngine = CreateProjectEngine();
        var irDocument = CreateIRDocument(projectEngine, codeDocument);
        var pass = new MvcViewDocumentClassifierPass
        {
            Engine = projectEngine.Engine
        };

        // Act
        pass.Execute(codeDocument, irDocument);

        // Assert
        Assert.Equal("mvc.1.0.view", irDocument.DocumentKind);
    }

    [Fact]
    public void MvcViewDocumentClassifierPass_NoOpsIfDocumentKindIsAlreadySet()
    {
        // Arrange
        var codeDocument = RazorCodeDocument.Create(RazorSourceDocument.Create("some-content", "Test.cshtml"));

        var projectEngine = CreateProjectEngine();
        var irDocument = CreateIRDocument(projectEngine, codeDocument);
        irDocument.DocumentKind = "some-value";
        var pass = new MvcViewDocumentClassifierPass
        {
            Engine = projectEngine.Engine
        };

        // Act
        pass.Execute(codeDocument, irDocument);

        // Assert
        Assert.Equal("some-value", irDocument.DocumentKind);
    }

    [Fact]
    public void MvcViewDocumentClassifierPass_SetsNamespace()
    {
        // Arrange
        var codeDocument = RazorCodeDocument.Create(RazorSourceDocument.Create("some-content", "Test.cshtml"));

        var projectEngine = CreateProjectEngine();
        var irDocument = CreateIRDocument(projectEngine, codeDocument);
        var pass = new MvcViewDocumentClassifierPass
        {
            Engine = projectEngine.Engine
        };

        // Act
        pass.Execute(codeDocument, irDocument);
        var visitor = new Visitor();
        visitor.Visit(irDocument);

        // Assert
        Assert.Equal("AspNetCore", visitor.Namespace.Content);
    }

    [Fact]
    public void MvcViewDocumentClassifierPass_SetsClass()
    {
        // Arrange
        var properties = new RazorSourceDocumentProperties(filePath: "ignored", relativePath: "Test.cshtml");
        var codeDocument = RazorCodeDocument.Create(RazorSourceDocument.Create("some-content", properties));

        var projectEngine = CreateProjectEngine();
        var irDocument = CreateIRDocument(projectEngine, codeDocument);
        var pass = new MvcViewDocumentClassifierPass
        {
            Engine = projectEngine.Engine
        };

        // Act
        pass.Execute(codeDocument, irDocument);
        var visitor = new Visitor();
        visitor.Visit(irDocument);

        // Assert
        Assert.Equal("global::Microsoft.AspNetCore.Mvc.Razor.RazorPage<TModel>", visitor.Class.BaseType);
        Assert.Equal(new[] { "public" }, visitor.Class.Modifiers);
        Assert.Equal("Test", visitor.Class.ClassName);
    }

    [Fact]
    public void MvcViewDocumentClassifierPass_NullFilePath_SetsClass()
    {
        // Arrange
        var properties = new RazorSourceDocumentProperties(filePath: null, relativePath: null);
        var codeDocument = RazorCodeDocument.Create(RazorSourceDocument.Create("some-content", properties));

        var projectEngine = CreateProjectEngine();
        var irDocument = CreateIRDocument(projectEngine, codeDocument);
        var pass = new MvcViewDocumentClassifierPass
        {
            Engine = projectEngine.Engine
        };

        // Act
        pass.Execute(codeDocument, irDocument);
        var visitor = new Visitor();
        visitor.Visit(irDocument);

        // Assert
        Assert.Equal("global::Microsoft.AspNetCore.Mvc.Razor.RazorPage<TModel>", visitor.Class.BaseType);
        Assert.Equal(new[] { "public" }, visitor.Class.Modifiers);
        Assert.Equal("AspNetCore_0a8cac771ca188eacc57e2c96c31f5611925c5ecedccb16b8c236d6c0d325112", visitor.Class.ClassName);
    }

    [Theory]
    [InlineData("/Views/Home/Index.cshtml", "_Views_Home_Index")]
    [InlineData("/Areas/MyArea/Views/Home/About.cshtml", "_Areas_MyArea_Views_Home_About")]
    public void MvcViewDocumentClassifierPass_UsesRelativePathToGenerateTypeName(string relativePath, string expected)
    {
        // Arrange
        var properties = new RazorSourceDocumentProperties(filePath: "ignored", relativePath: relativePath);
        var codeDocument = RazorCodeDocument.Create(RazorSourceDocument.Create("some-content", properties));

        var projectEngine = CreateProjectEngine();
        var irDocument = CreateIRDocument(projectEngine, codeDocument);
        var pass = new MvcViewDocumentClassifierPass
        {
            Engine = projectEngine.Engine
        };

        // Act
        pass.Execute(codeDocument, irDocument);
        var visitor = new Visitor();
        visitor.Visit(irDocument);

        // Assert
        Assert.Equal(expected, visitor.Class.ClassName);
    }

    [Fact]
    public void MvcViewDocumentClassifierPass_UsesAbsolutePath_IfRelativePathIsNotSet()
    {
        // Arrange
        var properties = new RazorSourceDocumentProperties(filePath: @"x::\application\Views\Home\Index.cshtml", relativePath: null);
        var codeDocument = RazorCodeDocument.Create(RazorSourceDocument.Create("some-content", properties));

        var projectEngine = CreateProjectEngine();
        var irDocument = CreateIRDocument(projectEngine, codeDocument);
        var pass = new MvcViewDocumentClassifierPass
        {
            Engine = projectEngine.Engine
        };

        // Act
        pass.Execute(codeDocument, irDocument);
        var visitor = new Visitor();
        visitor.Visit(irDocument);

        // Assert
        Assert.Equal("x___application_Views_Home_Index", visitor.Class.ClassName);
    }

    [Fact]
    public void MvcViewDocumentClassifierPass_SanitizesClassName()
    {
        // Arrange
        var properties = new RazorSourceDocumentProperties(filePath: @"x:\Test.cshtml", relativePath: "path.with+invalid-chars");
        var codeDocument = RazorCodeDocument.Create(RazorSourceDocument.Create("@page", properties));

        var projectEngine = CreateProjectEngine();
        var irDocument = CreateIRDocument(projectEngine, codeDocument);
        var pass = new MvcViewDocumentClassifierPass
        {
            Engine = projectEngine.Engine
        };

        // Act
        pass.Execute(codeDocument, irDocument);
        var visitor = new Visitor();
        visitor.Visit(irDocument);

        // Assert
        Assert.Equal("path_with_invalid_chars", visitor.Class.ClassName);
    }

    [Fact]
    public void MvcViewDocumentClassifierPass_SetsUpExecuteAsyncMethod()
    {
        // Arrange
        var codeDocument = RazorCodeDocument.Create(RazorSourceDocument.Create("some-content", "Test.cshtml"));

        var projectEngine = CreateProjectEngine();
        var irDocument = CreateIRDocument(projectEngine, codeDocument);
        var pass = new MvcViewDocumentClassifierPass
        {
            Engine = projectEngine.Engine
        };

        // Act
        pass.Execute(codeDocument, irDocument);
        var visitor = new Visitor();
        visitor.Visit(irDocument);

        // Assert
        Assert.Equal("ExecuteAsync", visitor.Method.MethodName);
        Assert.Equal("global::System.Threading.Tasks.Task", visitor.Method.ReturnType);
        Assert.Equal(new[] { "public", "async", "override" }, visitor.Method.Modifiers);
    }

    private static DocumentIntermediateNode CreateIRDocument(RazorProjectEngine engine, RazorCodeDocument codeDocument)
    {
        for (var i = 0; i < engine.Phases.Count; i++)
        {
            var phase = engine.Phases[i];
            phase.Execute(codeDocument);

            if (phase is IRazorIntermediateNodeLoweringPhase)
            {
                break;
            }
        }

        return codeDocument.GetDocumentIntermediateNode();
    }

    private class Visitor : IntermediateNodeWalker
    {
        public NamespaceDeclarationIntermediateNode Namespace { get; private set; }

        public ClassDeclarationIntermediateNode Class { get; private set; }

        public MethodDeclarationIntermediateNode Method { get; private set; }

        public override void VisitMethodDeclaration(MethodDeclarationIntermediateNode node)
        {
            Method = node;
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationIntermediateNode node)
        {
            Namespace = node;
            base.VisitNamespaceDeclaration(node);
        }

        public override void VisitClassDeclaration(ClassDeclarationIntermediateNode node)
        {
            Class = node;
            base.VisitClassDeclaration(node);
        }
    }
}
