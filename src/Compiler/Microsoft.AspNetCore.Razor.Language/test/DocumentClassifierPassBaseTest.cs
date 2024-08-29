﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


#nullable disable

using System;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;
using static Microsoft.AspNetCore.Razor.Language.Intermediate.IntermediateNodeAssert;

namespace Microsoft.AspNetCore.Razor.Language;

public class DocumentClassifierPassBaseTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Latest;

    public RazorEngine Engine => CreateProjectEngine().Engine;

    [Fact]
    public void Execute_HasDocumentKind_IgnoresDocument()
    {
        // Arrange
        var documentNode = new DocumentIntermediateNode()
        {
            DocumentKind = "ignore",
            Options = RazorCodeGenerationOptions.Default,
        };

        var pass = new TestDocumentClassifierPass();
        pass.Engine = Engine;

        // Act
        pass.Execute(TestRazorCodeDocument.CreateEmpty(), documentNode);

        // Assert
        Assert.Equal("ignore", documentNode.DocumentKind);
        NoChildren(documentNode);
    }

    [Fact]
    public void Execute_NoMatch_IgnoresDocument()
    {
        // Arrange
        var documentNode = new DocumentIntermediateNode()
        {
            Options = RazorCodeGenerationOptions.Default,
        };

        var pass = new TestDocumentClassifierPass()
        {
            Engine = Engine,
            ShouldMatch = false,
        };

        // Act
        pass.Execute(TestRazorCodeDocument.CreateEmpty(), documentNode);

        // Assert
        Assert.Null(documentNode.DocumentKind);
        NoChildren(documentNode);
    }

    [Fact]
    public void Execute_Match_AddsGlobalTargetExtensions()
    {
        // Arrange
        var documentNode = new DocumentIntermediateNode()
        {
            Options = RazorCodeGenerationOptions.Default,
        };

        var expected = new ICodeTargetExtension[]
        {
                new MyExtension1(),
                new MyExtension2(),
        };

        var pass = new TestDocumentClassifierPass();
        pass.Engine = RazorProjectEngine.CreateEmpty(b =>
        {
            for (var i = 0; i < expected.Length; i++)
            {
                b.AddTargetExtension(expected[i]);
            }
        }).Engine;

        ICodeTargetExtension[] extensions = null;

        pass.CodeTargetCallback = (builder) => extensions = builder.TargetExtensions.ToArray();

        // Act
        pass.Execute(TestRazorCodeDocument.CreateEmpty(), documentNode);

        // Assert
        Assert.Equal(expected, extensions);
    }

    [Fact]
    public void Execute_Match_SetsDocumentType_AndCreatesStructure()
    {
        // Arrange
        var documentNode = new DocumentIntermediateNode()
        {
            Options = RazorCodeGenerationOptions.Default,
        };

        var pass = new TestDocumentClassifierPass();
        pass.Engine = Engine;

        // Act
        pass.Execute(TestRazorCodeDocument.CreateEmpty(), documentNode);

        // Assert
        Assert.Equal("test", documentNode.DocumentKind);
        Assert.NotNull(documentNode.Target);

        var @namespace = SingleChild<NamespaceDeclarationIntermediateNode>(documentNode);
        var @class = SingleChild<ClassDeclarationIntermediateNode>(@namespace);
        var method = SingleChild<MethodDeclarationIntermediateNode>(@class);
        NoChildren(method);
    }

    [Fact]
    public void Execute_AddsUsingsToNamespace()
    {
        // Arrange
        var documentNode = new DocumentIntermediateNode()
        {
            Options = RazorCodeGenerationOptions.Default,
        };

        var builder = IntermediateNodeBuilder.Create(documentNode);
        builder.Add(new UsingDirectiveIntermediateNode());

        var pass = new TestDocumentClassifierPass();
        pass.Engine = Engine;

        // Act
        pass.Execute(TestRazorCodeDocument.CreateEmpty(), documentNode);

        // Assert
        var @namespace = SingleChild<NamespaceDeclarationIntermediateNode>(documentNode);
        Children(
            @namespace,
            n => Assert.IsType<UsingDirectiveIntermediateNode>(n),
            n => Assert.IsType<ClassDeclarationIntermediateNode>(n));
    }

    [Fact]
    public void Execute_AddsTheRestToMethod()
    {
        // Arrange
        var documentNode = new DocumentIntermediateNode()
        {
            Options = RazorCodeGenerationOptions.Default,
        };

        var builder = IntermediateNodeBuilder.Create(documentNode);
        builder.Add(new HtmlContentIntermediateNode());
        builder.Add(new CSharpCodeIntermediateNode());

        var pass = new TestDocumentClassifierPass();
        pass.Engine = Engine;

        // Act
        pass.Execute(TestRazorCodeDocument.CreateEmpty(), documentNode);

        // Assert
        var @namespace = SingleChild<NamespaceDeclarationIntermediateNode>(documentNode);
        var @class = SingleChild<ClassDeclarationIntermediateNode>(@namespace);
        var method = SingleChild<MethodDeclarationIntermediateNode>(@class);
        Children(
            method,
            n => Assert.IsType<HtmlContentIntermediateNode>(n),
            n => Assert.IsType<CSharpCodeIntermediateNode>(n));
    }

    [Fact]
    public void Execute_CanInitializeDefaults()
    {
        // Arrange
        var documentNode = new DocumentIntermediateNode()
        {
            Options = RazorCodeGenerationOptions.Default,
        };

        var builder = IntermediateNodeBuilder.Create(documentNode);
        builder.Add(new HtmlContentIntermediateNode());
        builder.Add(new CSharpCodeIntermediateNode());

        var pass = new TestDocumentClassifierPass()
        {
            Engine = Engine,
            Namespace = "TestNamespace",
            Class = "TestClass",
            Method = "TestMethod",
        };

        // Act
        pass.Execute(TestRazorCodeDocument.CreateEmpty(), documentNode);

        // Assert
        var @namespace = SingleChild<NamespaceDeclarationIntermediateNode>(documentNode);
        Assert.Equal("TestNamespace", @namespace.Content);

        var @class = SingleChild<ClassDeclarationIntermediateNode>(@namespace);
        Assert.Equal("TestClass", @class.ClassName);

        var method = SingleChild<MethodDeclarationIntermediateNode>(@class);
        Assert.Equal("TestMethod", method.MethodName);
    }

    [Fact]
    public void Execute_AddsPrimaryAnnotations()
    {
        // Arrange
        var documentNode = new DocumentIntermediateNode()
        {
            Options = RazorCodeGenerationOptions.Default,
        };

        var builder = IntermediateNodeBuilder.Create(documentNode);
        builder.Add(new HtmlContentIntermediateNode());
        builder.Add(new CSharpCodeIntermediateNode());

        var pass = new TestDocumentClassifierPass()
        {
            Engine = Engine,
            Namespace = "TestNamespace",
            Class = "TestClass",
            Method = "TestMethod",
        };

        // Act
        pass.Execute(TestRazorCodeDocument.CreateEmpty(), documentNode);

        // Assert
        var @namespace = SingleChild<NamespaceDeclarationIntermediateNode>(documentNode);
        AnnotationEquals(@namespace, CommonAnnotations.PrimaryNamespace);

        var @class = SingleChild<ClassDeclarationIntermediateNode>(@namespace);
        AnnotationEquals(@class, CommonAnnotations.PrimaryClass);

        var method = SingleChild<MethodDeclarationIntermediateNode>(@class);
        AnnotationEquals(method, CommonAnnotations.PrimaryMethod);
    }

    private class TestDocumentClassifierPass : DocumentClassifierPassBase
    {
        public override int Order => IntermediateNodePassBase.DefaultFeatureOrder;

        public bool ShouldMatch { get; set; } = true;

        public Action<CodeTargetBuilder> CodeTargetCallback { get; set; }

        public string Namespace { get; set; }

        public string Class { get; set; }

        public string Method { get; set; }

        protected override string DocumentKind => "test";

        protected override bool IsMatch(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
        {
            return ShouldMatch;
        }

        protected override void OnDocumentStructureCreated(
            RazorCodeDocument codeDocument,
            NamespaceDeclarationIntermediateNode @namespace,
            ClassDeclarationIntermediateNode @class,
            MethodDeclarationIntermediateNode method)
        {
            @namespace.Content = Namespace;
            @class.ClassName = Class;
            @method.MethodName = Method;
        }

        protected override void ConfigureTarget(CodeTargetBuilder builder)
        {
            CodeTargetCallback?.Invoke(builder);
        }
    }

    private class MyExtension1 : ICodeTargetExtension
    {
    }

    private class MyExtension2 : ICodeTargetExtension
    {
    }
}
