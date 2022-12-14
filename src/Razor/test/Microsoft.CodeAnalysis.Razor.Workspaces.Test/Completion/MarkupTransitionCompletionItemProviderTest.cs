﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.Editor.Razor;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Razor.Completion;

public class MarkupTransitionCompletionItemProviderTest : TestBase
{
    private readonly MarkupTransitionCompletionItemProvider _provider;

    public MarkupTransitionCompletionItemProviderTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _provider = new MarkupTransitionCompletionItemProvider(new DefaultHtmlFactsService());
    }

    [Fact]
    public void GetCompletionItems_ReturnsEmptyCompletionItemInUnopenedMarkupContext()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree("<div>");
        var absoluteIndex = 5;
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Empty(completionItems);
    }

    [Fact]
    public void GetCompletionItems_ReturnsEmptyCompletionItemInSimpleMarkupContext()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree("<div><");
        var absoluteIndex = 6;
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Empty(completionItems);
    }

    [Fact]
    public void GetCompletionItems_ReturnsEmptyCompletionItemInNestedMarkupContext()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree("<div><span><p></p><p>< </p></span></div>");
        var absoluteIndex = 22;
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Empty(completionItems);
    }

    [Fact]
    public void GetCompletionItems_ReturnsMarkupTransitionCompletionItemInCodeBlockStartingTag()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree("@{<");
        var absoluteIndex = 3;
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Collection(completionItems, AssertRazorCompletionItem);
    }

    [Fact]
    public void GetCompletionItems_ReturnsMarkupTransitionCompletionItemInCodeBlockPartialCompletion()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree("@{<te");
        var absoluteIndex = 5;
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Collection(completionItems, AssertRazorCompletionItem);
    }

    [Fact]
    public void GetCompletionItems_ReturnsMarkupTransitionCompletionItemInIfConditional()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree("@if (true) {< }");
        var absoluteIndex = 13;
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Collection(completionItems, AssertRazorCompletionItem);
    }

    [Fact]
    public void GetCompletionItems_ReturnsMarkupTransitionCompletionItemInFunctionDirective()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree("@functions {public string GetHello(){< return \"pi\";}}", FunctionsDirective.Directive);
        var absoluteIndex = 38;
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Collection(completionItems, AssertRazorCompletionItem);
    }

    [Fact]
    public void GetCompletionItems_ReturnsEmptyCompletionItemInExpression()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree(@"@{
    SomeFunctionAcceptingMethod(() =>
    {
        string foo = ""bar"";
    });
}

@SomeFunctionAcceptingMethod(() =>
{
    <
})");
        var absoluteIndex = 121 + (Environment.NewLine.Length * 9);
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Empty(completionItems);
    }

    [Fact]
    public void GetCompletionItems_ReturnsEmptyCompletionItemInSingleLineTransitions()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree(@"@{
    @* @: Here's some Markup | <-- You shouldn't get a <text> tag completion here. *@
    @: Here's some markup <
}");
        var absoluteIndex = 114 + (Environment.NewLine.Length * 2);
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Empty(completionItems);
    }

    [Fact]
    public void GetCompletionItems_ReturnsMarkupTransitionCompletionItemInNestedCSharpBlock()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree(@"<div>
@if (true)
{
  < @* Should get text completion here *@
}
</div>");
        var absoluteIndex = 19 + (Environment.NewLine.Length * 3);
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Collection(completionItems, AssertRazorCompletionItem);
    }

    [Fact]
    public void GetCompletionItems_ReturnsEmptyCompletionItemInNestedMarkupBlock()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree(@"@if (true)
{
<div>
  < @* Shouldn't get text completion here *@
</div>
}");
        var absoluteIndex = 19 + (Environment.NewLine.Length * 3);
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Empty(completionItems);
    }

    [Fact]
    public void GetCompletionItems_ReturnsMarkupTransitionCompletionItemWithUnrelatedClosingAngleBracket()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree(@"@functions {
    public void SomeOtherMethod()
    {
        <
    }

    private bool _collapseNavMenu => true;
}", FunctionsDirective.Directive);
        var absoluteIndex = 59 + (Environment.NewLine.Length * 3);
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Collection(completionItems, AssertRazorCompletionItem);
    }

    [Fact]
    public void GetCompletionItems_ReturnsMarkupTransitionCompletionItemWithUnrelatedClosingTag()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree("@{<></>");
        var absoluteIndex = 3;
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Collection(completionItems, AssertRazorCompletionItem);
    }

    [Fact]
    public void GetCompletionItems_ReturnsEmptyCompletionItemWhenOwnerIsComplexExpression()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree("@DateTime.Now<");
        var absoluteIndex = 14;
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Empty(completionItems);
    }

    [Fact]
    public void GetCompletionItems_ReturnsEmptyCompletionItemWhenOwnerIsExplicitExpression()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree("@(something)<");
        var absoluteIndex = 13;
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Empty(completionItems);
    }

    [Fact]
    public void GetCompletionItems_ReturnsEmptyCompletionItemWithSpaceAfterStartTag()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree("@{< ");
        var absoluteIndex = 4;
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Empty(completionItems);
    }

    [Fact]
    public void GetCompletionItems_ReturnsEmptyCompletionItemWithSpaceAfterStartTagAndAttribute()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree("@{< te=\"\"");
        var absoluteIndex = 6;
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Empty(completionItems);
    }

    [Fact]
    public void GetCompletionItems_ReturnsEmptyCompletionItemWhenInsideAttributeArea()
    {
        // Arrange
        var syntaxTree = CreateSyntaxTree("<p < >");
        var absoluteIndex = 4;
        var razorCompletionContext = CreateRazorCompletionContext(absoluteIndex, syntaxTree);

        // Act
        var completionItems = _provider.GetCompletionItems(razorCompletionContext);

        // Assert
        Assert.Empty(completionItems);
    }

    private static void AssertRazorCompletionItem(RazorCompletionItem item)
    {
        Assert.Equal(SyntaxConstants.TextTagName, item.DisplayText);
        Assert.Equal(SyntaxConstants.TextTagName, item.InsertText);
        var completionDescription = item.GetMarkupTransitionCompletionDescription();
        Assert.Equal(CodeAnalysisResources.MarkupTransition_Description, completionDescription.Description);
    }

    private static RazorCompletionContext CreateRazorCompletionContext(int absoluteIndex, RazorSyntaxTree syntaxTree)
    {
        var tagHelperDocumentContext = TagHelperDocumentContext.Create(prefix: string.Empty, Array.Empty<TagHelperDescriptor>());

        var queryableChange = new SourceChange(absoluteIndex, length: 0, newText: string.Empty);
        var owner = syntaxTree.Root.LocateOwner(queryableChange);
        return new RazorCompletionContext(absoluteIndex, owner, syntaxTree, tagHelperDocumentContext);
    }

    private static RazorSyntaxTree CreateSyntaxTree(string text, params DirectiveDescriptor[] directives)
    {
        return CreateSyntaxTree(text, FileKinds.Legacy, directives);
    }

    private static RazorSyntaxTree CreateSyntaxTree(string text, string fileKind, params DirectiveDescriptor[] directives)
    {
        var sourceDocument = TestRazorSourceDocument.Create(text);
        var options = RazorParserOptions.Create(builder =>
        {
            foreach (var directive in directives)
            {
                builder.Directives.Add(directive);
            }
        }, fileKind);
        var syntaxTree = RazorSyntaxTree.Parse(sourceDocument, options);
        return syntaxTree;
    }
}
