﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

public class ModelDirectiveTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Version_3_0;

    [Fact]
    public void ModelDirective_GetModelType_GetsTypeFromFirstWellFormedDirective()
    {
        // Arrange
        var codeDocument = CreateDocument(@"
@model Type1
@model Type2
@model
");

        var engine = CreateRuntimeEngine();

        var irDocument = CreateIRDocument(engine, codeDocument);

        // Act
        var result = ModelDirective.GetModelType(irDocument);

        // Assert
        Assert.Equal("Type1", result);
    }

    [Fact]
    public void ModelDirective_GetModelType_DefaultsToDynamic()
    {
        // Arrange
        var codeDocument = CreateDocument(@" ");

        var engine = CreateRuntimeEngine();

        var irDocument = CreateIRDocument(engine, codeDocument);

        // Act
        var result = ModelDirective.GetModelType(irDocument);

        // Assert
        Assert.Equal("dynamic", result);
    }

    [Fact]
    public void ModelDirectivePass_Execute_ReplacesTModelInBaseType()
    {
        // Arrange
        var codeDocument = CreateDocument(@"
@inherits BaseType<TModel>
@model Type1
");

        var engine = CreateRuntimeEngine();
        var pass = new ModelDirective.Pass()
        {
            Engine = engine,
        };

        var irDocument = CreateIRDocument(engine, codeDocument);

        // Act
        pass.Execute(codeDocument, irDocument);

        // Assert
        var @class = FindClassNode(irDocument);
        Assert.NotNull(@class);
        Assert.Equal("BaseType<Type1>", @class.BaseType);
    }

    [Fact]
    public void ModelDirectivePass_Execute_ReplacesTModelInBaseType_DifferentOrdering()
    {
        // Arrange
        var codeDocument = CreateDocument(@"
@model Type1
@inherits BaseType<TModel>
@model Type2
");

        var engine = CreateRuntimeEngine();
        var pass = new ModelDirective.Pass()
        {
            Engine = engine,
        };

        var irDocument = CreateIRDocument(engine, codeDocument);

        // Act
        pass.Execute(codeDocument, irDocument);

        // Assert
        var @class = FindClassNode(irDocument);
        Assert.NotNull(@class);
        Assert.Equal("BaseType<Type1>", @class.BaseType);
    }

    [Fact]
    public void ModelDirectivePass_Execute_NoOpWithoutTModel()
    {
        // Arrange
        var codeDocument = CreateDocument(@"
@inherits BaseType
@model Type1
");

        var engine = CreateRuntimeEngine();
        var pass = new ModelDirective.Pass()
        {
            Engine = engine,
        };

        var irDocument = CreateIRDocument(engine, codeDocument);

        // Act
        pass.Execute(codeDocument, irDocument);

        // Assert
        var @class = FindClassNode(irDocument);
        Assert.NotNull(@class);
        Assert.Equal("BaseType", @class.BaseType);
    }

    [Fact]
    public void ModelDirectivePass_Execute_ReplacesTModelInBaseType_DefaultDynamic()
    {
        // Arrange
        var codeDocument = CreateDocument(@"
@inherits BaseType<TModel>
");

        var engine = CreateRuntimeEngine();
        var pass = new ModelDirective.Pass()
        {
            Engine = engine,
        };

        var irDocument = CreateIRDocument(engine, codeDocument);

        // Act
        pass.Execute(codeDocument, irDocument);

        // Assert
        var @class = FindClassNode(irDocument);
        Assert.NotNull(@class);
        Assert.Equal("BaseType<dynamic>", @class.BaseType);
    }

    [Fact]
    public void ModelDirectivePass_DesignTime_AddsTModelUsingDirective()
    {
        // Arrange
        var codeDocument = CreateDocument(@"
@inherits BaseType<TModel>
");

        var engine = CreateDesignTimeEngine();
        var pass = new ModelDirective.Pass()
        {
            Engine = engine,
        };

        var irDocument = CreateIRDocument(engine, codeDocument);

        // Act
        pass.Execute(codeDocument, irDocument);

        // Assert
        var @class = FindClassNode(irDocument);
        Assert.NotNull(@class);
        Assert.Equal("BaseType<dynamic>", @class.BaseType);

        var @namespace = FindNamespaceNode(irDocument);
        var usingNode = Assert.IsType<UsingDirectiveIntermediateNode>(@namespace.Children[0]);
        Assert.Equal($"TModel = global::{typeof(object).FullName}", usingNode.Content);
    }

    [Fact]
    public void ModelDirectivePass_DesignTime_WithModel_AddsTModelUsingDirective()
    {
        // Arrange
        var codeDocument = CreateDocument(@"
@inherits BaseType<TModel>
@model SomeType
");

        var engine = CreateDesignTimeEngine();
        var pass = new ModelDirective.Pass()
        {
            Engine = engine,
        };

        var irDocument = CreateIRDocument(engine, codeDocument);

        // Act
        pass.Execute(codeDocument, irDocument);

        // Assert
        var @class = FindClassNode(irDocument);
        Assert.NotNull(@class);
        Assert.Equal("BaseType<SomeType>", @class.BaseType);

        var @namespace = FindNamespaceNode(irDocument);
        var usingNode = Assert.IsType<UsingDirectiveIntermediateNode>(@namespace.Children[0]);
        Assert.Equal($"TModel = global::System.Object", usingNode.Content);
    }

    private RazorCodeDocument CreateDocument(string content)
    {
        var source = RazorSourceDocument.Create(content, "test.cshtml");
        return RazorCodeDocument.Create(source);
    }

    private ClassDeclarationIntermediateNode FindClassNode(IntermediateNode node)
    {
        var visitor = new ClassNodeVisitor();
        visitor.Visit(node);
        return visitor.Node;
    }

    private NamespaceDeclarationIntermediateNode FindNamespaceNode(IntermediateNode node)
    {
        var visitor = new NamespaceNodeVisitor();
        visitor.Visit(node);
        return visitor.Node;
    }

    private RazorEngine CreateRuntimeEngine()
    {
        return CreateEngineCore(designTime: false);
    }

    private RazorEngine CreateDesignTimeEngine()
    {
        return CreateEngineCore(designTime: true);
    }

    private RazorEngine CreateEngineCore(bool designTime = false)
    {
        return CreateProjectEngine(b =>
        {
                // Notice we're not registering the ModelDirective.Pass here so we can run it on demand.
                b.AddDirective(ModelDirective.Directive);

            b.Features.Add(new RazorPageDocumentClassifierPass());
            b.Features.Add(new MvcViewDocumentClassifierPass());
            b.Features.Add(new DesignTimeOptionsFeature(designTime));
        }).Engine;
    }

    private DocumentIntermediateNode CreateIRDocument(RazorEngine engine, RazorCodeDocument codeDocument)
    {
        for (var i = 0; i < engine.Phases.Count; i++)
        {
            var phase = engine.Phases[i];
            phase.Execute(codeDocument);

            if (phase is IRazorDocumentClassifierPhase)
            {
                break;
            }
        }

        // InheritsDirectivePass needs to run before ModelDirective.
        var pass = new InheritsDirectivePass()
        {
            Engine = engine
        };
        pass.Execute(codeDocument, codeDocument.GetDocumentIntermediateNode());

        return codeDocument.GetDocumentIntermediateNode();
    }

    private class ClassNodeVisitor : IntermediateNodeWalker
    {
        public ClassDeclarationIntermediateNode Node { get; set; }

        public override void VisitClassDeclaration(ClassDeclarationIntermediateNode node)
        {
            Node = node;
        }
    }

    private class NamespaceNodeVisitor : IntermediateNodeWalker
    {
        public NamespaceDeclarationIntermediateNode Node { get; set; }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationIntermediateNode node)
        {
            Node = node;
        }
    }

    private class DesignTimeOptionsFeature : IConfigureRazorParserOptionsFeature, IConfigureRazorCodeGenerationOptionsFeature
    {
        private readonly bool _designTime;

        public DesignTimeOptionsFeature(bool designTime)
        {
            _designTime = designTime;
        }

        public int Order { get; }

        public RazorEngine Engine { get; set; }

        public void Configure(RazorParserOptionsBuilder options)
        {
            options.SetDesignTime(_designTime);
        }

        public void Configure(RazorCodeGenerationOptionsBuilder options)
        {
            options.SetDesignTime(_designTime);
        }
    }
}
