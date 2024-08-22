// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.CodeActions.Razor;

public class ExtractToComponentCodeActionProviderTest(ITestOutputHelper testOutput) : LanguageServerTestBase(testOutput)
{
    [Fact]
    public async Task Handle_InvalidFileKind()
    {
        // Arrange
        var documentPath = "c:/Test.razor";
        var contents = """
            @page "/"

            <PageTitle>Home</PageTitle>

            <div id="parent">
                <div>
                    <h1>Div a title</h1>
                    <p>Div $$a par</p>
                </div>
                <div>
                    <h1>Div b title</h1>
                    <p>Div b par</p>
                </div>
            </div>

            <h1>Hello, world!</h1>

            Welcome to your new app.
            """;
        TestFileMarkupParser.GetPosition(contents, out contents, out var cursorPosition);

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = new Uri(documentPath) },
            Range = VsLspFactory.DefaultRange,
            Context = new VSInternalCodeActionContext()
        };

        var location = new SourceLocation(cursorPosition, -1, -1);
        var context = CreateRazorCodeActionContext(request, location, documentPath, contents);
        context.CodeDocument.SetFileKind(FileKinds.Legacy);

        var provider = new ExtractToComponentCodeActionProvider(LoggerFactory);

        // Act
        var commandOrCodeActionContainer = await provider.ProvideAsync(context, default);

        // Assert
        Assert.Empty(commandOrCodeActionContainer);
    }

    [Fact]
    public async Task Handle_SinglePointSelection_ReturnsNotEmpty()
    {
        // Arrange
        var documentPath = "c:/Test.razor";
        var contents = """
            @page "/"

            <PageTitle>Home</PageTitle>

            <div id="parent">
                <$$div>
                    <h1>Div a title</h1>
                    <p>Div a par</p>
                </div>
                <div>
                    <h1>Div b title</h1>
                    <p>Div b par</p>
                </div>
            </div>

            <h1>Hello, world!</h1>

            Welcome to your new app.
            """;
        TestFileMarkupParser.GetPosition(contents, out contents, out var cursorPosition);

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = new Uri(documentPath) },
            Range = VsLspFactory.DefaultRange,
            Context = new VSInternalCodeActionContext()
        };

        var location = new SourceLocation(cursorPosition, -1, -1);
        var context = CreateRazorCodeActionContext(request, location, documentPath, contents, supportsFileCreation: true);

        var provider = new ExtractToComponentCodeActionProvider(LoggerFactory);

        // Act
        var commandOrCodeActionContainer = await provider.ProvideAsync(context, default);

        // Assert
        Assert.NotEmpty(commandOrCodeActionContainer);
    }

    [Fact]
    public async Task Handle_MultiPointSelection_ReturnsNotEmpty()
    {
        // Arrange
        var documentPath = "c:/Test.razor";
        var contents = """
            @page "/"

            <PageTitle>Home</PageTitle>

            <div id="parent">
                [|<div>
                    $$<h1>Div a title</h1>
                    <p>Div a par</p>
                </div>
                <div>
                    <h1>Div b title</h1>
                    <p>Div b par</p>
                </div|]>
            </div>

            <h1>Hello, world!</h1>

            Welcome to your new app.
            """;
        TestFileMarkupParser.GetPositionAndSpan(contents, out contents, out var cursorPosition, out var selectionSpan);

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = new Uri(documentPath) },
            Range = VsLspFactory.DefaultRange,
            Context = new VSInternalCodeActionContext()
        };

        var location = new SourceLocation(cursorPosition, -1, -1);
        var context = CreateRazorCodeActionContext(request, location, documentPath, contents);

        var lineSpan = context.SourceText.GetLinePositionSpan(selectionSpan);
        request.Range = VsLspFactory.CreateRange(lineSpan);

        var provider = new ExtractToComponentCodeActionProvider(LoggerFactory);

        // Act
        var commandOrCodeActionContainer = await provider.ProvideAsync(context, default);

        // Assert
        Assert.NotEmpty(commandOrCodeActionContainer);
        var codeAction = Assert.Single(commandOrCodeActionContainer);
        var razorCodeActionResolutionParams = ((JsonElement)codeAction.Data!).Deserialize<RazorCodeActionResolutionParams>();
        Assert.NotNull(razorCodeActionResolutionParams);
        var actionParams = ((JsonElement)razorCodeActionResolutionParams.Data).Deserialize<ExtractToComponentCodeActionParams>();
        Assert.NotNull(actionParams);
    }

    [Fact]
    public async Task Handle_MultiPointSelection_WithEndAfterElement_ReturnsCurrentElement()
    {
        // Arrange
        var documentPath = "c:/Test.razor";
        var contents = """
            @page "/"
            @namespace MarketApp.Pages.Product.Home

            namespace MarketApp.Pages.Product.Home

            <PageTitle>Home</PageTitle>

            <div id="parent">
                [|$$<div>
                    <h1>Div a title</h1>
                    <p>Div a par</p>
                </div>
                <div>
                    <h1>Div b title</h1>
                    <p>Div b par</p>
                </div>|]
            </div>

            <h1>Hello, world!</h1>

            Welcome to your new app.
            """;
        TestFileMarkupParser.GetPositionAndSpan(contents, out contents, out var cursorPosition, out var selectionSpan);

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = new Uri(documentPath) },
            Range = VsLspFactory.DefaultRange,
            Context = new VSInternalCodeActionContext()
        };

        var location = new SourceLocation(cursorPosition, -1, -1);
        var context = CreateRazorCodeActionContext(request, location, documentPath, contents);

        var lineSpan = context.SourceText.GetLinePositionSpan(selectionSpan);
        request.Range = VsLspFactory.CreateRange(lineSpan);

        var provider = new ExtractToComponentCodeActionProvider(LoggerFactory);

        // Act
        var commandOrCodeActionContainer = await provider.ProvideAsync(context, default);

        // Assert
        Assert.NotEmpty(commandOrCodeActionContainer);
        var codeAction = Assert.Single(commandOrCodeActionContainer);
        var razorCodeActionResolutionParams = ((JsonElement)codeAction.Data!).Deserialize<RazorCodeActionResolutionParams>();
        Assert.NotNull(razorCodeActionResolutionParams);
        var actionParams = ((JsonElement)razorCodeActionResolutionParams.Data).Deserialize<ExtractToComponentCodeActionParams>();
        Assert.NotNull(actionParams);
        Assert.Equal(selectionSpan.Start, actionParams.ExtractStart);
        Assert.Equal(selectionSpan.End, actionParams.ExtractEnd);
    }

    [Fact]
    public async Task Handle_InProperMarkup_ReturnsEmpty()
    {
        // Arrange
        var documentPath = "c:/Test.razor";
        var contents = """
            @page "/"
            
            <PageTitle>Home</PageTitle>
            
            <div id="parent">
                <div>
                    <h1>Div a title</h1>
                    <p>Div $$a par</p>
                </div>
                <div>
                    <h1>Div b title</h1>
                    <p>Div b par</p>
                </div>
            </div>

            <h1>Hello, world!</h1>

            Welcome to your new app.
            """;
        TestFileMarkupParser.GetPosition(contents, out contents, out var cursorPosition);

        var request = new VSCodeActionParams()
        {
            TextDocument = new VSTextDocumentIdentifier { Uri = new Uri(documentPath) },
            Range = new Range(),
            Context = new VSInternalCodeActionContext()
        };

        var location = new SourceLocation(cursorPosition, -1, -1);
        var context = CreateRazorCodeActionContext(request, location, documentPath, contents);

        var provider = new ExtractToComponentCodeActionProvider(LoggerFactory);

        // Act
        var commandOrCodeActionContainer = await provider.ProvideAsync(context, default);

        // Assert
        Assert.Empty(commandOrCodeActionContainer);
    }

    private static RazorCodeActionContext CreateRazorCodeActionContext(VSCodeActionParams request, SourceLocation location, string filePath, string text, bool supportsFileCreation = true)
        => CreateRazorCodeActionContext(request, location, filePath, text, relativePath: filePath, supportsFileCreation: supportsFileCreation);

    private static RazorCodeActionContext CreateRazorCodeActionContext(VSCodeActionParams request, SourceLocation location, string filePath, string text, string? relativePath, bool supportsFileCreation = true)
    {
        var sourceDocument = RazorSourceDocument.Create(text, RazorSourceDocumentProperties.Create(filePath, relativePath));
        var options = RazorParserOptions.Create(o =>
        {
            o.Directives.Add(ComponentCodeDirective.Directive);
            o.Directives.Add(FunctionsDirective.Directive);
        });
        var syntaxTree = RazorSyntaxTree.Parse(sourceDocument, options);

        var codeDocument = TestRazorCodeDocument.Create(sourceDocument, imports: default);
        codeDocument.SetFileKind(FileKinds.Component);
        codeDocument.SetCodeGenerationOptions(RazorCodeGenerationOptions.Create(o =>
        {
            o.RootNamespace = "ExtractToComponentTest";
        }));
        codeDocument.SetSyntaxTree(syntaxTree);

        var documentSnapshot = Mock.Of<IDocumentSnapshot>(document =>
            document.GetGeneratedOutputAsync() == Task.FromResult(codeDocument) &&
            document.GetTextAsync() == Task.FromResult(codeDocument.Source.Text), MockBehavior.Strict);

        var sourceText = SourceText.From(text);

        var context = new RazorCodeActionContext(request, documentSnapshot, codeDocument, location, sourceText, supportsFileCreation, SupportsCodeActionResolve: true);

        return context;
    }
}
