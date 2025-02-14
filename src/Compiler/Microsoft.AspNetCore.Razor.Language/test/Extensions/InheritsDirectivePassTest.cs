// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        var projectEngine = CreateProjectEngine();
        var codeDocument = projectEngine.CreateCodeDocument("@inherits Hello<World[]>");
        var runner = RazorProjectEngineRunner.From(projectEngine, codeDocument);

        var documentNode = new DocumentIntermediateNode();
        documentNode.Children.Add(new DirectiveIntermediateNode() { Directive = FunctionsDirective.Directive, });

        // Act
        runner.ExecutePass<InheritsDirectivePass>(documentNode);

        // Assert
        Children(
            documentNode,
            node => Assert.IsType<DirectiveIntermediateNode>(node));
    }

    [Fact]
    public void Execute_Inherits_SetsClassDeclarationBaseType()
    {
        // Arrange
        var projectEngine = CreateProjectEngine();
        var codeDocument = projectEngine.CreateCodeDocument("@inherits Hello<World[]>");
        var runner = RazorProjectEngineRunner.From(projectEngine, codeDocument);

        runner.RunPhasesTo<IRazorDocumentClassifierPhase>();

        // Act
        runner.ExecutePass<InheritsDirectivePass>();

        var documentNode = codeDocument.GetDocumentIntermediateNode();

        // Assert
        Children(
            documentNode,
            node => Assert.IsType<NamespaceDeclarationIntermediateNode>(node));

        var @namespace = documentNode.Children[0];
        Children(
            @namespace,
            node => Assert.IsType<ClassDeclarationIntermediateNode>(node));

        var @class = (ClassDeclarationIntermediateNode)@namespace.Children[0];
        Assert.Equal("Hello<World[]>", @class.BaseType.BaseType.Content);
    }
}
