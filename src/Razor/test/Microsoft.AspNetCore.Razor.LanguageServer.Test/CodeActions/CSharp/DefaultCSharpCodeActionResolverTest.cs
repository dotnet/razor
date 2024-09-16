﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

public class DefaultCSharpCodeActionResolverTest(ITestOutputHelper testOutput) : LanguageServerTestBase(testOutput)
{
    private static readonly CodeAction s_defaultResolvedCodeAction = new()
    {
        Title = "ResolvedCodeAction",
        Data = JsonSerializer.SerializeToElement(new object()),
        Edit = new WorkspaceEdit()
        {
            DocumentChanges = new TextDocumentEdit[] {
                new()
                {
                    Edits = [VsLspFactory.CreateTextEdit(position: (0, 0), "Generated C# Based Edit")]
                }
            }
        }
    };

    private static readonly TextEdit s_defaultFormattedEdit = VsLspFactory.CreateTextEdit(position: (0, 0), "Remapped & Formatted Edit");
    private static readonly TextChange s_defaultFormattedChange = new TextChange(new TextSpan(0, 0), s_defaultFormattedEdit.NewText);

    private static readonly CodeAction s_defaultUnresolvedCodeAction = new CodeAction()
    {
        Title = "Unresolved Code Action"
    };

    [Fact]
    public async Task ResolveAsync_ReturnsResolvedCodeAction()
    {
        // Arrange
        CreateCodeActionResolver(out var codeActionParams, out var csharpCodeActionResolver);

        // Act
        var returnedCodeAction = await csharpCodeActionResolver.ResolveAsync(codeActionParams, s_defaultUnresolvedCodeAction, default);

        // Assert
        Assert.Equal(s_defaultResolvedCodeAction.Title, returnedCodeAction.Title);
        Assert.Equal(s_defaultResolvedCodeAction.Data, returnedCodeAction.Data);
        Assert.NotNull(returnedCodeAction.Edit?.DocumentChanges);
        Assert.Equal(1, returnedCodeAction.Edit.DocumentChanges.Value.Count());
        var returnedEdits = returnedCodeAction.Edit.DocumentChanges.Value;
        Assert.True(returnedEdits.TryGetFirst(out var textDocumentEdits));
        var returnedTextDocumentEdit = Assert.Single(textDocumentEdits[0].Edits);
        Assert.Equal(s_defaultFormattedEdit.NewText, returnedTextDocumentEdit.NewText);
        Assert.Equal(s_defaultFormattedEdit.Range, returnedTextDocumentEdit.Range);
    }

    [Fact]
    public async Task ResolveAsync_NoDocumentChanges_ReturnsOriginalCodeAction()
    {
        // Arrange
        var resolvedCodeAction = new CodeAction()
        {
            Title = "ResolvedCodeAction",
            Data = JsonSerializer.SerializeToElement(new object()),
            Edit = new WorkspaceEdit()
            {
                DocumentChanges = null
            }
        };

        var languageServer = CreateLanguageServer(resolvedCodeAction);

        CreateCodeActionResolver(out var codeActionParams, out var csharpCodeActionResolver, clientConnection: languageServer);

        // Act
        var returnedCodeAction = await csharpCodeActionResolver.ResolveAsync(codeActionParams, s_defaultUnresolvedCodeAction, default);

        // Assert
        Assert.Equal(s_defaultUnresolvedCodeAction.Title, returnedCodeAction.Title);
    }

    [Fact]
    public async Task ResolveAsync_MultipleDocumentChanges_ReturnsOriginalCodeAction()
    {
        // Arrange
        var resolvedCodeAction = new CodeAction()
        {
            Title = "ResolvedCodeAction",
            Data = JsonSerializer.SerializeToElement(new object()),
            Edit = new WorkspaceEdit()
            {
                DocumentChanges = new TextDocumentEdit[]
                {
                    new TextDocumentEdit()
                    {
                        Edits = [VsLspFactory.CreateTextEdit(position: (0, 0), "1. Generated C# Based Edit")]
                    },
                    new TextDocumentEdit()
                    {
                        Edits = [VsLspFactory.CreateTextEdit(position: (0, 0), "2. Generated C# Based Edit")]
                    }
                }
            }
        };

        var languageServer = CreateLanguageServer(resolvedCodeAction);

        CreateCodeActionResolver(out var codeActionParams, out var csharpCodeActionResolver, clientConnection: languageServer);

        // Act
        var returnedCodeAction = await csharpCodeActionResolver.ResolveAsync(codeActionParams, s_defaultUnresolvedCodeAction, default);

        // Assert
        Assert.Equal(s_defaultUnresolvedCodeAction.Title, returnedCodeAction.Title);
    }

    [Fact]
    public async Task ResolveAsync_NonTextDocumentEdit_ReturnsOriginalCodeAction()
    {
        // Arrange
        var resolvedCodeAction = new CodeAction()
        {
            Title = "ResolvedCodeAction",
            Data = JsonSerializer.SerializeToElement(new object()),
            Edit = new WorkspaceEdit()
            {
                DocumentChanges = new SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[] {
                        new CreateFile()
                        {
                            Uri = new Uri("c:/some/uri.razor")
                        }
                    }
            }
        };

        var languageServer = CreateLanguageServer(resolvedCodeAction);

        CreateCodeActionResolver(out var codeActionParams, out var csharpCodeActionResolver, clientConnection: languageServer);

        // Act
        var returnedCodeAction = await csharpCodeActionResolver.ResolveAsync(codeActionParams, s_defaultUnresolvedCodeAction, default);

        // Assert
        Assert.Equal(s_defaultUnresolvedCodeAction.Title, returnedCodeAction.Title);
    }

    private static void CreateCodeActionResolver(
        out CodeActionResolveParams codeActionParams,
        out DefaultCSharpCodeActionResolver csharpCodeActionResolver,
        IClientConnection? clientConnection = null,
        IRazorFormattingService? razorFormattingService = null)
    {
        var documentPath = "c:/Test.razor";
        var documentUri = new Uri(documentPath);
        var contents = string.Empty;
        var codeDocument = CreateCodeDocument(contents, documentPath);

        codeActionParams = new CodeActionResolveParams()
        {
            Data = new JsonElement(),
            RazorFileIdentifier = new VSTextDocumentIdentifier
            {
                Uri = documentUri
            }
        };

        clientConnection ??= CreateLanguageServer();
        razorFormattingService ??= CreateRazorFormattingService(documentUri);

        csharpCodeActionResolver = new DefaultCSharpCodeActionResolver(
            CreateDocumentContextFactory(documentUri, codeDocument),
            clientConnection,
            razorFormattingService);
    }

    private static IRazorFormattingService CreateRazorFormattingService(Uri documentUri)
    {
        var razorFormattingService = Mock.Of<IRazorFormattingService>(
                        rfs => rfs.TryGetCSharpCodeActionEditAsync(
                            It.Is<DocumentContext>(c => c.Uri == documentUri),
                            It.IsAny<ImmutableArray<TextChange>>(),
                            It.IsAny<RazorFormattingOptions>(),
                            It.IsAny<CancellationToken>()) == Task.FromResult<TextChange?>(s_defaultFormattedChange), MockBehavior.Strict);
        return razorFormattingService;
    }

    private static IClientConnection CreateLanguageServer(CodeAction? resolvedCodeAction = null)
    {
        var response = resolvedCodeAction ?? s_defaultResolvedCodeAction;

        var clientConnection = new Mock<IClientConnection>(MockBehavior.Strict);
        clientConnection
            .Setup(l => l.SendRequestAsync<RazorResolveCodeActionParams, CodeAction>(CustomMessageNames.RazorResolveCodeActionsEndpoint, It.IsAny<RazorResolveCodeActionParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        return clientConnection.Object;
    }

    private static RazorCodeDocument CreateCodeDocument(string text, string documentPath)
    {
        var projectItem = new TestRazorProjectItem(documentPath) { Content = text };
        var projectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, TestRazorProjectFileSystem.Empty, (builder) => PageDirective.Register(builder));
        var codeDocument = projectEngine.Process(projectItem);
        codeDocument.SetFileKind(FileKinds.Component);
        return codeDocument;
    }
}
