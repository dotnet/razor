﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;
using static Microsoft.AspNetCore.Razor.Language.Intermediate.IntermediateNodeAssert;

namespace Microsoft.AspNetCore.Razor.Language.Extensions;

public class InheritsDirectivePassTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Latest;

    [Fact]
    public void Execute_SkipsDocumentWithNoClassNode()
    {
        // Arrange
        var engine = CreateEngine();
        var pass = new InheritsDirectivePass()
        {
            Engine = engine,
        };

        var sourceDocument = TestRazorSourceDocument.Create("@inherits Hello<World[]>");
        var codeDocument = RazorCodeDocument.Create(sourceDocument);

        var irDocument = new DocumentIntermediateNode();
        irDocument.Children.Add(new DirectiveIntermediateNode() { Directive = FunctionsDirective.Directive, });

        // Act
        pass.Execute(codeDocument, irDocument);

        // Assert
        Children(
            irDocument,
            node => Assert.IsType<DirectiveIntermediateNode>(node));
    }

    [Fact]
    public void Execute_Inherits_SetsClassDeclarationBaseType()
    {
        // Arrange
        var engine = CreateEngine();
        var pass = new InheritsDirectivePass()
        {
            Engine = engine,
        };

        var content = "@inherits Hello<World[]>";
        var sourceDocument = TestRazorSourceDocument.Create(content);
        var codeDocument = RazorCodeDocument.Create(sourceDocument);

        var irDocument = Lower(codeDocument, engine);

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

        var @class = (ClassDeclarationIntermediateNode)@namespace.Children[0];
        Assert.Equal("Hello<World[]>", @class.BaseType);
    }

    private static DocumentIntermediateNode Lower(RazorCodeDocument codeDocument, RazorEngine engine)
    {
        foreach (var phase in engine.Phases)
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
