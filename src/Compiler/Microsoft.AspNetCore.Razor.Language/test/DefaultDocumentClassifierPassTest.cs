﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


#nullable disable

using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;
using static Microsoft.AspNetCore.Razor.Language.Intermediate.IntermediateNodeAssert;

namespace Microsoft.AspNetCore.Razor.Language;

// We're purposely lean on tests here because the functionality is well covered by
// integration tests, and is mostly implemented by the base class.
public class DefaultDocumentClassifierPassTest : RazorProjectEngineTestBase
{
    protected override RazorLanguageVersion Version => RazorLanguageVersion.Latest;

    [Fact]
    public void Execute_IgnoresDocumentsWithDocumentKind()
    {
        // Arrange
        var documentNode = new DocumentIntermediateNode()
        {
            DocumentKind = "ignore",
            Options = RazorCodeGenerationOptions.Default,
        };

        var pass = new DefaultDocumentClassifierPass();
        pass.Engine = CreateProjectEngine().Engine;

        // Act
        pass.Execute(TestRazorCodeDocument.CreateEmpty(), documentNode);

        // Assert
        Assert.Equal("ignore", documentNode.DocumentKind);
        NoChildren(documentNode);
    }

    [Fact]
    public void Execute_CreatesClassStructure()
    {
        // Arrange
        var documentNode = new DocumentIntermediateNode()
        {
            Options = RazorCodeGenerationOptions.Default,
        };

        var pass = new DefaultDocumentClassifierPass();
        pass.Engine = CreateProjectEngine().Engine;

        // Act
        pass.Execute(TestRazorCodeDocument.CreateEmpty(), documentNode);

        // Assert
        Assert.Equal("default", documentNode.DocumentKind);

        var @namespace = SingleChild<NamespaceDeclarationIntermediateNode>(documentNode);
        var @class = SingleChild<ClassDeclarationIntermediateNode>(@namespace);
        var method = SingleChild<MethodDeclarationIntermediateNode>(@class);
        NoChildren(method);
    }
}
