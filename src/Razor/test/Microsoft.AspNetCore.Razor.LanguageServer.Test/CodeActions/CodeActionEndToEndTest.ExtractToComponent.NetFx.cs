// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

public partial class CodeActionEndToEndTest : SingleServerDelegatingEndpointTestBase
{
    private ExtractToComponentCodeActionResolver[] CreateExtractComponentCodeActionResolver(
    string filePath,
    RazorCodeDocument codeDocument,
    IClientConnection clientConnection)
    {
        var projectManager = new StrictMock<IDocumentVersionCache>();
        int? version = 1;
        projectManager.Setup(x => x.TryGetDocumentVersion(It.IsAny<IDocumentSnapshot>(), out version)).Returns(true);

        return [
                new ExtractToComponentCodeActionResolver(
                    new GenerateMethodResolverDocumentContextFactory(filePath, codeDocument), // We can use the same factory here
                    TestLanguageServerFeatureOptions.Instance,
                    clientConnection,
                    projectManager.Object)
            ];
    }

    [Fact]
    public async Task Handle_ExtractComponent_SingleElement_ReturnsResult()
    {
        var input = """
            <[||]div id="a">
                <h1>Div a title</h1>
                <Book Title="To Kill a Mockingbird" Author="Harper Lee" Year="Long ago" />
                <p>Div a par</p>
            </div>
            <div id="shouldSkip">
                <Movie Title="Aftersun" Director="Charlotte Wells" Year="2022" />
            </div>
            """;

        var expectedRazorComponent = """
            <div id="a">
                <h1>Div a title</h1>
                <Book Title="To Kill a Mockingbird" Author="Harper Lee" Year="Long ago" />
                <p>Div a par</p>
            </div>
            """;

        await ValidateExtractComponentCodeActionAsync(
            input,
            expectedRazorComponent,
            ExtractToComponentTitle,
            razorCodeActionProviders: [new ExtractToComponentCodeActionProvider(LoggerFactory)],
            codeActionResolversCreator: CreateExtractComponentCodeActionResolver);
    }

    [Fact]
    public async Task Handle_ExtractComponent_SiblingElement_ReturnsResult()
    {
        var input = """
            <[|div id="a">
                <h1>Div a title</h1>
                <Book Title="To Kill a Mockingbird" Author="Harper Lee" Year="Long ago" />
                <p>Div a par</p>
            </div>
            <div id="b">
                <Movie Title="Aftersun" Director="Charlotte Wells" Year="2022" />
            </div|]>
            """;

        var expectedRazorComponent = """
            <div id="a">
                <h1>Div a title</h1>
                <Book Title="To Kill a Mockingbird" Author="Harper Lee" Year="Long ago" />
                <p>Div a par</p>
            </div>
            <div id="b">
                <Movie Title="Aftersun" Director="Charlotte Wells" Year="2022" />
            </div>
            """;

        await ValidateExtractComponentCodeActionAsync(
            input,
            expectedRazorComponent,
            ExtractToComponentTitle,
            razorCodeActionProviders: [new ExtractToComponentCodeActionProvider(LoggerFactory)],
            codeActionResolversCreator: CreateExtractComponentCodeActionResolver);
    }

    [Fact]
    public async Task Handle_ExtractComponent_StartNodeContainsEndNode_ReturnsResult()
    {
        var input = """
            <[|div id="parent">
                <div>
                    <div>
                        <div>
                            <p>Deeply nested par</p|]>
                        </div>
                    </div>
                </div>
            </div>
            """;

        var expectedRazorComponent = """
            <div id="parent">
                <div>
                    <div>
                        <div>
                            <p>Deeply nested par</p>
                        </div>
                    </div>
                </div>
            </div>
            """;

        await ValidateExtractComponentCodeActionAsync(
            input,
            expectedRazorComponent,
            ExtractToComponentTitle,
            razorCodeActionProviders: [new ExtractToComponentCodeActionProvider(LoggerFactory)],
            codeActionResolversCreator: CreateExtractComponentCodeActionResolver);
    }

    [Fact]
    public async Task Handle_ExtractComponent_EndNodeContainsStartNode_ReturnsResult()
    {
        var input = """
            <div id="parent">
                <div>
                    <div>
                        <div>
                            <[|p>Deeply nested par</p>
                        </div>
                    </div>
                </div>
            </div|]>
            """;

        var expectedRazorComponent = """
            <div id="parent">
                <div>
                    <div>
                        <div>
                            <p>Deeply nested par</p>
                        </div>
                    </div>
                </div>
            </div>
            """;

        await ValidateExtractComponentCodeActionAsync(
            input,
            expectedRazorComponent,
            ExtractToComponentTitle,
            razorCodeActionProviders: [new ExtractToComponentCodeActionProvider(LoggerFactory)],
            codeActionResolversCreator: CreateExtractComponentCodeActionResolver);
    }

    [Fact]
    public async Task Handle_ExtractComponent_IndentedNode_ReturnsResult()
    {
        var input = """
            <div id="parent">
                <div>
                    <[||]div>
                        <div>
                            <p>Deeply nested par</p>
                        </div>
                    </div>
                </div>
            </div>
            """;

        var expectedRazorComponent = """
            <div>
                <div>
                    <p>Deeply nested par</p>
                </div>
            </div>
            """;

        await ValidateExtractComponentCodeActionAsync(
            input,
            expectedRazorComponent,
            ExtractToComponentTitle,
            razorCodeActionProviders: [new ExtractToComponentCodeActionProvider(LoggerFactory)],
            codeActionResolversCreator: CreateExtractComponentCodeActionResolver);
    }

    [Fact]
    public async Task Handle_ExtractComponent_IndentedSiblingNodes_ReturnsResult()
    {
        var input = """
            <div id="parent">
                <div>
                    <div>
                        <div>
                            <[|div>
                                <p>Deeply nested par</p>
                            </div>
                            <div>
                                <p>Deeply nested par</p>
                            </div>
                            <div>
                                <p>Deeply nested par</p>
                            </div>
                            <div>
                                <p>Deeply nested par</p>
                            </div|]>
                        </div>
                    </div>
                </div>
            </div>
            """;

        var expectedRazorComponent = """
            <div>
                <p>Deeply nested par</p>
            </div>
            <div>
                <p>Deeply nested par</p>
            </div>
            <div>
                <p>Deeply nested par</p>
            </div>
            <div>
                <p>Deeply nested par</p>
            </div>
            """;

        await ValidateExtractComponentCodeActionAsync(
            input,
            expectedRazorComponent,
            ExtractToComponentTitle,
            razorCodeActionProviders: [new ExtractToComponentCodeActionProvider(LoggerFactory)],
            codeActionResolversCreator: CreateExtractComponentCodeActionResolver);
    }

    [Fact]
    public async Task Handle_ExtractComponent_IndentedStartNodeContainsEndNode_ReturnsResult()
    {
        var input = """
            <div id="parent">
                <div>
                    <[|div>
                        <div>
                            <p>Deeply nested par</p|]>
                        </div>
                    </div>
                </div>
            </div>
            """;

        var expectedRazorComponent = """
            <div>
                <div>
                    <p>Deeply nested par</p>
                </div>
            </div>
            """;

        await ValidateExtractComponentCodeActionAsync(
            input,
            expectedRazorComponent,
            ExtractToComponentTitle,
            razorCodeActionProviders: [new ExtractToComponentCodeActionProvider(LoggerFactory)],
            codeActionResolversCreator: CreateExtractComponentCodeActionResolver);
    }

    [Fact]
    public async Task Handle_ExtractComponent_IndentedEndNodeContainsStartNode_ReturnsResult()
    {
        var input = """
            <div id="parent">
                <div>
                    <div>
                        <div>
                            <[|p>Deeply nested par</p>
                        </div>
                    </div|]>
                </div>
            </div>
            """;

        var expectedRazorComponent = """
            <div>
                <div>
                    <p>Deeply nested par</p>
                </div>
            </div>
            """;

        await ValidateExtractComponentCodeActionAsync(
            input,
            expectedRazorComponent,
            ExtractToComponentTitle,
            razorCodeActionProviders: [new ExtractToComponentCodeActionProvider(LoggerFactory)],
            codeActionResolversCreator: CreateExtractComponentCodeActionResolver);
    }

    private async Task ValidateExtractComponentCodeActionAsync(
        string input,
        string? expected,
        string codeAction,
        int childActionIndex = 0,
        IEnumerable<(string filePath, string contents)>? additionalRazorDocuments = null,
        IRazorCodeActionProvider[]? razorCodeActionProviders = null,
        Func<string, RazorCodeDocument, IClientConnection, IRazorCodeActionResolver[]>? codeActionResolversCreator = null,
        Diagnostic[]? diagnostics = null)
    {
        TestFileMarkupParser.GetSpan(input, out input, out var textSpan);

        var razorFilePath = "C:/path/Test.razor";
        var componentFilePath = "C:/path/Component.razor";
        var codeDocument = CreateCodeDocument(input, filePath: razorFilePath);
        var sourceText = codeDocument.GetSourceText();
        var uri = new Uri(razorFilePath);
        var languageServer = await CreateLanguageServerAsync(codeDocument, razorFilePath, additionalRazorDocuments);

        //var projectManager = CreateProjectSnapshotManager();

        //await projectManager.UpdateAsync(updater =>
        //{
        //    updater.ProjectAdded(new(
        //        projectFilePath: "C:/path/to/project.csproj",
        //        intermediateOutputPath: "C:/path/to/obj",
        //        razorConfiguration: RazorConfiguration.Default,
        //        rootNamespace: "project"));
        //});

        //var componentSearchEngine = new DefaultRazorComponentSearchEngine(projectManager, LoggerFactory);
        //var componentDefinitionService = new RazorComponentDe

        var documentContext = CreateDocumentContext(uri, codeDocument);
        var requestContext = new RazorRequestContext(documentContext, null!, "lsp/method", uri: null);

        var result = await GetCodeActionsAsync(
            uri,
            textSpan,
            sourceText,
            requestContext,
            languageServer,
            razorCodeActionProviders,
            diagnostics);

        Assert.NotEmpty(result);
        var codeActionToRun = GetCodeActionToRun(codeAction, childActionIndex, result);

        if (expected is null)
        {
            Assert.Null(codeActionToRun);
            return;
        }

        Assert.NotNull(codeActionToRun);

        var changes = await GetEditsAsync(
            codeActionToRun,
            requestContext,
            languageServer,
            codeActionResolversCreator?.Invoke(razorFilePath, codeDocument, languageServer) ?? []);

        var edits = changes.Where(change => change.TextDocument.Uri.AbsolutePath == componentFilePath).Single();
        var actual = edits.Edits.Select(edit => edit.NewText).Single();

        AssertEx.EqualOrDiff(expected, actual);
    }
}
