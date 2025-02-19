// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions.Version2_X;

public class AssemblyAttributeInjectionPassTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Version_2_1;

    [Fact]
    public void Execute_NoOps_IfNamespaceNodeIsMissing()
    {
        // Arrange
        var projectEngine = CreateProjectEngine(); ;
        var codeDocument = projectEngine.CreateEmptyCodeDocument();
        var documentNode = new DocumentIntermediateNode() { Options = codeDocument.CodeGenerationOptions };

        // Act
        projectEngine.ExecutePass<AssemblyAttributeInjectionPass>(codeDocument, documentNode);

        // Assert
        Assert.Empty(documentNode.Children);
    }

    [Fact]
    public void Execute_NoOps_IfNamespaceNodeHasEmptyContent()
    {
        // Arrange
        var projectEngine = CreateProjectEngine(); ;
        var codeDocument = projectEngine.CreateEmptyCodeDocument();
        var documentNode = new DocumentIntermediateNode() { Options = codeDocument.CodeGenerationOptions };

        var builder = IntermediateNodeBuilder.Create(documentNode);
        var @namespace = new NamespaceDeclarationIntermediateNode()
        {
            Content = string.Empty,
            Annotations =
            {
                [CommonAnnotations.PrimaryNamespace] = CommonAnnotations.PrimaryNamespace
            }
        };

        builder.Push(@namespace);

        // Act
        projectEngine.ExecutePass<AssemblyAttributeInjectionPass>(codeDocument, documentNode);

        // Assert
        var node = Assert.Single(documentNode.Children);
        Assert.Same(@namespace, node);
    }

    [Fact]
    public void Execute_NoOps_IfClassNameNodeIsMissing()
    {
        // Arrange
        var projectEngine = CreateProjectEngine(); ;
        var codeDocument = projectEngine.CreateEmptyCodeDocument();
        var documentNode = new DocumentIntermediateNode() { Options = codeDocument.CodeGenerationOptions };

        var builder = IntermediateNodeBuilder.Create(documentNode);
        var @namespace = new NamespaceDeclarationIntermediateNode() { Content = "SomeNamespace" };
        builder.Push(@namespace);

        // Act
        projectEngine.ExecutePass<AssemblyAttributeInjectionPass>(codeDocument, documentNode);

        // Assert
        var node = Assert.Single(documentNode.Children);
        Assert.Same(@namespace, node);
    }

    [Fact]
    public void Execute_NoOps_IfClassNameIsEmpty()
    {
        // Arrange
        var projectEngine = CreateProjectEngine(); ;
        var codeDocument = projectEngine.CreateEmptyCodeDocument();
        var documentNode = new DocumentIntermediateNode() { Options = codeDocument.CodeGenerationOptions };

        var builder = IntermediateNodeBuilder.Create(documentNode);

        var @namespace = new NamespaceDeclarationIntermediateNode()
        {
            Content = "SomeNamespace",
            Annotations =
            {
                [CommonAnnotations.PrimaryNamespace] = CommonAnnotations.PrimaryNamespace
            }
        };

        builder.Push(@namespace);

        builder.Add(new ClassDeclarationIntermediateNode
        {
            Annotations =
            {
                [CommonAnnotations.PrimaryClass] = CommonAnnotations.PrimaryClass,
            },
        });

        // Act
        projectEngine.ExecutePass<AssemblyAttributeInjectionPass>(codeDocument, documentNode);

        // Assert
        var node = Assert.Single(documentNode.Children);
        Assert.Same(@namespace, node);
    }

    [Fact]
    public void Execute_NoOps_IfDocumentIsNotViewOrPage()
    {
        // Arrange
        var projectEngine = CreateProjectEngine(); ;
        var codeDocument = projectEngine.CreateEmptyCodeDocument();
        var documentNode = new DocumentIntermediateNode()
        {
            DocumentKind = "Default",
            Options = codeDocument.CodeGenerationOptions
        };

        var builder = IntermediateNodeBuilder.Create(documentNode);
        var @namespace = new NamespaceDeclarationIntermediateNode() { Content = "SomeNamespace" };
        builder.Push(@namespace);

        var @class = new ClassDeclarationIntermediateNode
        {
            ClassName = "SomeName",
            Annotations =
            {
                [CommonAnnotations.PrimaryClass] = CommonAnnotations.PrimaryClass,
            }
        };

        builder.Add(@class);

        // Act
        projectEngine.ExecutePass<AssemblyAttributeInjectionPass>(codeDocument, documentNode);

        // Assert
        var node = Assert.Single(documentNode.Children);
        Assert.Same(@namespace, node);
    }

    [Fact]
    public void Execute_NoOps_ForDesignTime()
    {
        // Arrange
        var projectEngine = CreateProjectEngine();
        var source = TestRazorSourceDocument.Create("test", RazorSourceDocumentProperties.Create(filePath: null, relativePath: "/Views/Index.cshtml"));
        var codeDocument = projectEngine.CreateDesignTimeCodeDocument(source);

        var documentNode = new DocumentIntermediateNode()
        {
            DocumentKind = MvcViewDocumentClassifierPass.MvcViewDocumentKind,
            Options = codeDocument.CodeGenerationOptions
        };

        var builder = IntermediateNodeBuilder.Create(documentNode);
        var @namespace = new NamespaceDeclarationIntermediateNode
        {
            Content = "SomeNamespace",
            Annotations =
            {
                [CommonAnnotations.PrimaryNamespace] = CommonAnnotations.PrimaryNamespace
            }
        };

        builder.Push(@namespace);

        var @class = new ClassDeclarationIntermediateNode
        {
            ClassName = "SomeName",
            Annotations =
            {
                [CommonAnnotations.PrimaryClass] = CommonAnnotations.PrimaryClass,
            }
        };

        builder.Add(@class);

        // Act
        projectEngine.ExecutePass<AssemblyAttributeInjectionPass>(codeDocument, documentNode);

        // Assert
        var node = Assert.Single(documentNode.Children);
        Assert.Same(@namespace, node);
    }

    [Fact]
    public void Execute_AddsRazorViewAttribute_ToViews()
    {
        // Arrange
        var projectEngine = CreateProjectEngine();
        var source = TestRazorSourceDocument.Create("test", RazorSourceDocumentProperties.Create(filePath: null, relativePath: "/Views/Index.cshtml"));
        var codeDocument = projectEngine.CreateCodeDocument(source);

        var expectedAttribute = "[assembly:global::Microsoft.AspNetCore.Mvc.Razor.Compilation.RazorViewAttribute(@\"/Views/Index.cshtml\", typeof(SomeNamespace.SomeName))]";

        var documentNode = new DocumentIntermediateNode()
        {
            DocumentKind = MvcViewDocumentClassifierPass.MvcViewDocumentKind,
            Options = codeDocument.CodeGenerationOptions
        };

        var builder = IntermediateNodeBuilder.Create(documentNode);

        var @namespace = new NamespaceDeclarationIntermediateNode
        {
            Content = "SomeNamespace",
            Annotations =
            {
                [CommonAnnotations.PrimaryNamespace] = CommonAnnotations.PrimaryNamespace
            }
        };

        builder.Push(@namespace);
        var @class = new ClassDeclarationIntermediateNode
        {
            ClassName = "SomeName",
            Annotations =
            {
                [CommonAnnotations.PrimaryClass] = CommonAnnotations.PrimaryClass,
            }
        };

        builder.Add(@class);

        // Act
        projectEngine.ExecutePass<AssemblyAttributeInjectionPass>(codeDocument, documentNode);

        // Assert
        Assert.Collection(documentNode.Children,
            node =>
            {
                var csharpCode = Assert.IsType<CSharpCodeIntermediateNode>(node);
                var token = Assert.IsAssignableFrom<IntermediateToken>(Assert.Single(csharpCode.Children));
                Assert.Equal(TokenKind.CSharp, token.Kind);
                Assert.Equal(expectedAttribute, token.Content);
            },
            node => Assert.Same(@namespace, node));
    }

    [Fact]
    public void Execute_EscapesViewPathWhenAddingAttributeToViews()
    {
        // Arrange
        var projectEngine = CreateProjectEngine();
        var source = TestRazorSourceDocument.Create("test", RazorSourceDocumentProperties.Create(filePath: null, relativePath: "\\test\\\"Index.cshtml"));
        var codeDocument = projectEngine.CreateCodeDocument(source);

        var expectedAttribute = "[assembly:global::Microsoft.AspNetCore.Mvc.Razor.Compilation.RazorViewAttribute(@\"/test/\"\"Index.cshtml\", typeof(SomeNamespace.SomeName))]";

        var documentNode = new DocumentIntermediateNode()
        {
            DocumentKind = MvcViewDocumentClassifierPass.MvcViewDocumentKind,
            Options = codeDocument.CodeGenerationOptions
        };

        var builder = IntermediateNodeBuilder.Create(documentNode);

        var @namespace = new NamespaceDeclarationIntermediateNode
        {
            Content = "SomeNamespace",
            Annotations =
            {
                [CommonAnnotations.PrimaryNamespace] = CommonAnnotations.PrimaryNamespace
            }
        };

        builder.Push(@namespace);

        var @class = new ClassDeclarationIntermediateNode
        {
            ClassName = "SomeName",
            Annotations =
            {
                [CommonAnnotations.PrimaryClass] = CommonAnnotations.PrimaryClass,
            }
        };

        builder.Add(@class);

        // Act
        projectEngine.ExecutePass<AssemblyAttributeInjectionPass>(codeDocument, documentNode);

        // Assert
        Assert.Collection(documentNode.Children,
            node =>
            {
                var csharpCode = Assert.IsType<CSharpCodeIntermediateNode>(node);
                var token = Assert.IsAssignableFrom<IntermediateToken>(Assert.Single(csharpCode.Children));
                Assert.Equal(TokenKind.CSharp, token.Kind);
                Assert.Equal(expectedAttribute, token.Content);
            },
            node => Assert.Same(@namespace, node));
    }

    [Fact]
    public void Execute_AddsRazorPagettribute_ToPage()
    {
        // Arrange
        var projectEngine = CreateProjectEngine();
        var source = TestRazorSourceDocument.Create("test", RazorSourceDocumentProperties.Create(filePath: null, relativePath: "/Views/Index.cshtml"));
        var codeDocument = projectEngine.CreateCodeDocument(source);

        var expectedAttribute = "[assembly:global::Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure.RazorPageAttribute(@\"/Views/Index.cshtml\", typeof(SomeNamespace.SomeName), null)]";

        var documentNode = new DocumentIntermediateNode()
        {
            DocumentKind = RazorPageDocumentClassifierPass.RazorPageDocumentKind,
            Options = codeDocument.CodeGenerationOptions
        };

        var builder = IntermediateNodeBuilder.Create(documentNode);

        var pageDirective = new DirectiveIntermediateNode
        {
            Directive = PageDirective.Directive
        };

        builder.Add(pageDirective);

        var @namespace = new NamespaceDeclarationIntermediateNode
        {
            Content = "SomeNamespace",
            Annotations =
            {
                [CommonAnnotations.PrimaryNamespace] = CommonAnnotations.PrimaryNamespace
            }
        };

        builder.Push(@namespace);

        var @class = new ClassDeclarationIntermediateNode
        {
            ClassName = "SomeName",
            Annotations =
            {
                [CommonAnnotations.PrimaryClass] = CommonAnnotations.PrimaryClass,
            }
        };

        builder.Add(@class);

        // Act
        projectEngine.ExecutePass<AssemblyAttributeInjectionPass>(codeDocument, documentNode);

        // Assert
        Assert.Collection(documentNode.Children,
            node => Assert.Same(pageDirective, node),
            node =>
            {
                var csharpCode = Assert.IsType<CSharpCodeIntermediateNode>(node);
                var token = Assert.IsAssignableFrom<IntermediateToken>(Assert.Single(csharpCode.Children));
                Assert.Equal(TokenKind.CSharp, token.Kind);
                Assert.Equal(expectedAttribute, token.Content);
            },
            node => Assert.Same(@namespace, node));
    }

    [Fact]
    public void Execute_EscapesViewPathAndRouteWhenAddingAttributeToPage()
    {
        // Arrange
        var projectEngine = CreateProjectEngine();
        var source = TestRazorSourceDocument.Create("test", RazorSourceDocumentProperties.Create(filePath: null, relativePath: "test\\\"Index.cshtml"));
        var codeDocument = projectEngine.CreateCodeDocument(source);

        var expectedAttribute = "[assembly:global::Microsoft.AspNetCore.Mvc.Razor.Compilation.RazorViewAttribute(@\"/test/\"\"Index.cshtml\", typeof(SomeNamespace.SomeName))]";

        var documentNode = new DocumentIntermediateNode()
        {
            DocumentKind = MvcViewDocumentClassifierPass.MvcViewDocumentKind,
            Options = codeDocument.CodeGenerationOptions
        };

        var builder = IntermediateNodeBuilder.Create(documentNode);

        var @namespace = new NamespaceDeclarationIntermediateNode
        {
            Content = "SomeNamespace",
            Annotations =
            {
                [CommonAnnotations.PrimaryNamespace] = CommonAnnotations.PrimaryNamespace
            }
        };
        builder.Push(@namespace);

        var @class = new ClassDeclarationIntermediateNode
        {
            ClassName = "SomeName",
            Annotations =
            {
                [CommonAnnotations.PrimaryClass] = CommonAnnotations.PrimaryClass,
            }
        };

        builder.Add(@class);

        // Act
        projectEngine.ExecutePass<AssemblyAttributeInjectionPass>(codeDocument, documentNode);

        // Assert
        Assert.Collection(documentNode.Children,
            node =>
            {
                var csharpCode = Assert.IsType<CSharpCodeIntermediateNode>(node);
                var token = Assert.IsAssignableFrom<IntermediateToken>(Assert.Single(csharpCode.Children));
                Assert.Equal(TokenKind.CSharp, token.Kind);
                Assert.Equal(expectedAttribute, token.Content);
            },
            node => Assert.Same(@namespace, node));
    }
}
