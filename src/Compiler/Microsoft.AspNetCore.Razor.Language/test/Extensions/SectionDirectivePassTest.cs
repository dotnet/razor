﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;
using static Microsoft.AspNetCore.Razor.Language.Intermediate.IntermediateNodeAssert;

namespace Microsoft.AspNetCore.Razor.Language.Extensions;

public class SectionDirectivePassTest
{
    [Fact]
    public void Execute_SkipsDocumentWithNoClassNode()
    {
        // Arrange
        var projectEngine = CreateProjectEngine();
        var pass = new SectionDirectivePass()
        {
            Engine = projectEngine.Engine,
        };

        var sourceDocument = TestRazorSourceDocument.Create("@section Header { <p>Hello World</p> }");
        var codeDocument = RazorCodeDocument.Create(sourceDocument);

        var irDocument = new DocumentIntermediateNode();
        irDocument.Children.Add(new DirectiveIntermediateNode() { Directive = SectionDirective.Directive, });

        // Act
        pass.Execute(codeDocument, irDocument);

        // Assert
        Children(
            irDocument,
            node => Assert.IsType<DirectiveIntermediateNode>(node));
    }

    [Fact]
    public void Execute_WrapsStatementInSectionNode()
    {
        // Arrange
        var projectEngine = CreateProjectEngine();
        var pass = new SectionDirectivePass()
        {
            Engine = projectEngine.Engine,
        };

        var content = "@section Header { <p>Hello World</p> }";
        var sourceDocument = TestRazorSourceDocument.Create(content);
        var codeDocument = RazorCodeDocument.Create(sourceDocument);

        var irDocument = Lower(codeDocument, projectEngine);

        // Act
        pass.Execute(codeDocument, irDocument);

        // Assert
        Children(
            irDocument,
            node => Assert.IsType<NamespaceDeclarationIntermediateNode>(node));

        var @namespace = irDocument.Children[0];
        Children(
            @namespace,
            node => Assert.IsType<ClassDeclarationIntermediateNode>(node));

        var @class = @namespace.Children[0];
        var method = SingleChild<MethodDeclarationIntermediateNode>(@class);

        Children(
            method,
            node => Assert.IsType<DirectiveIntermediateNode>(node),
            node => Assert.IsType<SectionIntermediateNode>(node));

        var section = method.Children[1] as SectionIntermediateNode;
        Assert.Equal("Header", section.SectionName);
        Children(section, c => Html(" <p>Hello World</p> ", c));
    }

    private static RazorProjectEngine CreateProjectEngine()
    {
        return RazorProjectEngine.Create(b =>
        {
            SectionDirective.Register(b);
        });
    }

    private static DocumentIntermediateNode Lower(RazorCodeDocument codeDocument, RazorProjectEngine projectEngine)
    {
        foreach (var phase in projectEngine.Phases)
        {
            phase.Execute(codeDocument);

            if (phase is IRazorDocumentClassifierPhase)
            {
                break;
            }
        }

        var irDocument = codeDocument.GetDocumentIntermediateNode();
        Assert.NotNull(irDocument);

        return irDocument;
    }
}
