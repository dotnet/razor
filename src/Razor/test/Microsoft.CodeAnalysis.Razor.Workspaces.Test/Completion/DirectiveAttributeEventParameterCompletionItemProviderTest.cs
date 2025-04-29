// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.IntegrationTests;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Razor.Completion;

public class DirectiveAttributeEventParameterCompletionItemProviderTest : RazorToolingIntegrationTestBase
{
    private readonly DirectiveAttributeEventParameterCompletionItemProvider _provider;

    internal override string FileKind => FileKinds.Component;
    internal override bool UseTwoPhaseCompilation => true;

    public DirectiveAttributeEventParameterCompletionItemProviderTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        // Most of these completions rely on stuff in the web namespace.
        ImportItems.Add(CreateProjectItem(
            "_Imports.razor",
            "@using Microsoft.AspNetCore.Components.Web"));

        _provider = new DirectiveAttributeEventParameterCompletionItemProvider();
    }

    private RazorCodeDocument GetCodeDocument(string content)
    {
        var result = CompileToCSharp(content, throwOnFailure: false);
        return result.CodeDocument;
    }

    [Fact]
    public void GetCompletionItems_OnEmptyDirectiveAttributeEventParameter_ReturnsCompletions()
    {
        // Arrange
        var context = CreateRazorCompletionContext("""
            <input @bind="str" @bind:event="$$" />

            @code {
                private string? str;
            }
            """);

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        AssertContains(completions, "oninput");
        AssertContains(completions, "onchange");
        AssertContains(completions, "onblur");
    }

    [Fact]
    public void GetCompletionItems_OnNonEmptyDirectiveAttributeEventParameter_ReturnsCompletions()
    {
        // Arrange
        var context = CreateRazorCompletionContext("""
            <input @bind="str" @bind:event="onin$$put" />

            @code {
                private string? str;
            }
            """);

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        AssertContains(completions, "oninput");
        AssertContains(completions, "onchange");
        AssertContains(completions, "onblur");
    }

    [Fact]
    public void GetCompletionItems_OnEmptyButNotClosedDirectiveAttributeEventParameter_ReturnsCompletions()
    {
        // Arrange
        var context = CreateRazorCompletionContext("""
            <input @bind="str" @bind:event="$$ />

            @code {
                private string? str;
            }
            """);

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        AssertContains(completions, "oninput");
        AssertContains(completions, "onchange");
        AssertContains(completions, "onblur");
    }

    [Fact]
    public void GetCompletionItems_OnNonEmptyAndNotClosedDirectiveAttributeEventParameter_ReturnsCompletions()
    {
        // Arrange
        var context = CreateRazorCompletionContext("""
            <input @bind="str" @bind:event="oninput$$ />

            @code {
                private string? str;
            }
            """);

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        AssertContains(completions, "oninput");
        AssertContains(completions, "onchange");
        AssertContains(completions, "onblur");
    }

    [Fact]
    public void GetCompletionItems_BeforeDirectiveAttributeEventParameterAttributePrefix_DoesNotReturnCompletions()
    {
        // Arrange
        var context = CreateRazorCompletionContext("""
            <input @bind="str" @bind:event=$$"oninput" />

            @code {
                private string? str;
            }
            """);

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        Assert.Empty(completions);
    }

    [Fact]
    public void GetCompletionItems_AfterDirectiveAttributeEventParameterAttributeSuffix_DoesNotReturnCompletions()
    {
        // Arrange
        var context = CreateRazorCompletionContext("""
            <input @bind="str" @bind:event="oninput"$$ />

            @code {
                private string? str;
            }
            """);

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        Assert.Empty(completions);
    }

    [Fact]
    public void GetCompletionItems_OnHtmlEventAttribute_DoesNotReturnCompletions()
    {
        // Arrange
        var context = CreateRazorCompletionContext("""
            <input @bind="str" event="oni$$nput" />

            @code {
                private string? str;
            }
            """);

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        Assert.Empty(completions);
    }

    [Fact]
    public void GetCompletionItems_OnBindDirectiveAttribute_DoesNotReturnCompletions()
    {
        // Arrange
        var context = CreateRazorCompletionContext("""
            <input @bind="s$$tr" />

            @code {
                private string? str;
            }
            """);

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        Assert.Empty(completions);
    }

    private static void AssertContains(IReadOnlyList<RazorCompletionItem> completions, string insertText)
    {
        Assert.Contains(completions, completion => insertText == completion.InsertText &&
            insertText == completion.DisplayText &&
            RazorCompletionItemKind.DirectiveAttributeParameterEventValue == completion.Kind);
    }

    private RazorCompletionContext CreateRazorCompletionContext(string documentContent)
    {
        documentContent = ExtractCaretPosition(documentContent, out var absoluteIndex);

        var codeDocument = GetCodeDocument(documentContent);
        var syntaxTree = codeDocument.GetSyntaxTree();
        var tagHelperDocumentContext = codeDocument.GetTagHelperContext();

        var owner = syntaxTree.Root.FindInnermostNode(absoluteIndex);
        owner = AbstractRazorCompletionFactsService.AdjustSyntaxNodeForWordBoundary(owner, absoluteIndex);
        return new RazorCompletionContext(absoluteIndex, owner, syntaxTree, tagHelperDocumentContext);

        static string ExtractCaretPosition(string document, out int caretPosition)
        {
            var index = document.IndexOf("$$", StringComparison.Ordinal);
            if (index == -1)
            {
                caretPosition = -1;
                return document;
            }

            caretPosition = index;
            return document.Remove(index, 2);
        }
    }
}
