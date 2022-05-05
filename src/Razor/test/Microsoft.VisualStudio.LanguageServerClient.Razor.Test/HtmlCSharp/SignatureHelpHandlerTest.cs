// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Debugging;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class SignatureHelpHandlerTest : HandlerTestBase
    {
        public SignatureHelpHandlerTest()
        {
            Uri = new Uri("C:/path/to/file.razor");
            var csharpVirtualDocument = new CSharpVirtualDocumentSnapshot(
                new Uri("C:/path/to/file.razor.g.cs"),
                new TestTextBuffer(new StringTextSnapshot(string.Empty)).CurrentSnapshot,
                hostDocumentSyncVersion: 0);
            var htmlVirtualDocument = new HtmlVirtualDocumentSnapshot(
                new Uri("C:/path/to/file.razor__virtual.html"),
                new TestTextBuffer(new StringTextSnapshot(string.Empty)).CurrentSnapshot,
                hostDocumentSyncVersion: 0);
            LSPDocumentSnapshot documentSnapshot = new TestLSPDocumentSnapshot(Uri, version: 0, htmlVirtualDocument, csharpVirtualDocument);
            DocumentManager = new TestDocumentManager();
            DocumentManager.AddDocument(Uri, documentSnapshot);
        }

        private Uri Uri { get; }

        private TestDocumentManager DocumentManager { get; }

        [Fact]
        public async Task HandleRequestAsync_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var requestInvoker = Mock.Of<LSPRequestInvoker>(MockBehavior.Strict);
            var projectionProvider = Mock.Of<LSPProjectionProvider>(MockBehavior.Strict);
            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker, documentManager, projectionProvider, LoggerProvider);
            var signatureHelpRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1)
            };

            // Act
            var result = await signatureHelpHandler.HandleRequestAsync(signatureHelpRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_ProjectionNotFound_ReturnsNull()
        {
            // Arrange
            var requestInvoker = Mock.Of<LSPRequestInvoker>(MockBehavior.Strict);
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict).Object;
            Mock.Get(projectionProvider).Setup(projectionProvider => projectionProvider.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), CancellationToken.None))
                .Returns(Task.FromResult<ProjectionResult>(null));
            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker, DocumentManager, projectionProvider, LoggerProvider);
            var signatureHelpRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1)
            };

            // Act
            var result = await signatureHelpHandler.HandleRequestAsync(signatureHelpRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_HtmlProjection_InvokesHtmlLanguageServer_ReturnsItem()
        {
            // Arrange
            var called = false;
            var expectedResult = new ReinvocationResponse<SignatureHelp>("LanguageClientName", new SignatureHelp());

            var virtualHtmlUri = new Uri("C:/path/to/file.razor__virtual.html");
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, SignatureHelp>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<TextDocumentPositionParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, TextDocumentPositionParams, CancellationToken>((textBuffer, method, clientName, definitionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentSignatureHelpName, method);
                    Assert.Equal(RazorLSPConstants.HtmlLanguageServerName, clientName);
                    called = true;
                })
                .Returns(Task.FromResult(expectedResult));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker.Object, DocumentManager, projectionProvider.Object, LoggerProvider);
            var signatureHelpRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5)
            };

            // Act
            var result = await signatureHelpHandler.HandleRequestAsync(signatureHelpRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.Equal(expectedResult.Response, result);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_InvokesCSharpLanguageServer_ReturnsItem()
        {
            // Arrange
            var text = """
                @code
                {
                    void M(int a, int b, int c)
                    {
                        M(
                    }
                }
                """;
            var cursorPosition = new Position { Line = 4, Character = 10 };

            var sourceDocument = TestRazorSourceDocument.Create(text, filePath: null, relativePath: null);
            var projectEngine = RazorProjectEngine.Create(builder => { });
            var codeDocument = projectEngine.ProcessDesignTime(sourceDocument, FileKinds.Component, Array.Empty<RazorSourceDocument>(), Array.Empty<TagHelperDescriptor>());
            var virtualDocumentUri = new Uri("C:/path/to/file.razor__virtual.cs");
            var snapshot = new StringTextSnapshot(codeDocument.GetCSharpDocument().GeneratedCode);
            var virtualDocumentSnapshot = new CSharpVirtualDocumentSnapshot(virtualDocumentUri, snapshot, hostDocumentSyncVersion: 1);
            var documentUri = new Uri("C:/path/to/file.razor");
            var documentSnapshot = new TestLSPDocumentSnapshot(documentUri, version: 1, snapshotContent: text, virtualDocumentSnapshot);

            var testProjectionProvider = new TestLSPProjectionProvider();
            var sourceText = SourceText.From(text);
            var projection = await testProjectionProvider.GetProjectionAsync(
                documentSnapshot, cursorPosition, CancellationToken.None).ConfigureAwait(false);

            var csharpSourceText = codeDocument.GetCSharpSourceText();
            var files = new List<(Uri, SourceText)>();
            files.Add((virtualDocumentUri, csharpSourceText));

            var serverCapabilities = new ServerCapabilities { SignatureHelpProvider = new SignatureHelpOptions { TriggerCharacters = new string[] { "(" } } };
            var exportProvider = RoslynTestCompositions.Roslyn.ExportProviderFactory.CreateExportProvider();
            using var workspace = CSharpTestLspServerHelpers.CreateCSharpTestWorkspace(files, exportProvider);
            await using var csharpLspServer = await CSharpTestLspServerHelpers.CreateCSharpLspServerAsync(workspace, exportProvider, serverCapabilities);

            var textDocumentIdentifier = new TextDocumentIdentifier { Uri = virtualDocumentUri };
            var signatureHelpContext = new SignatureHelpContext { IsRetrigger = false, TriggerCharacter = "(", TriggerKind = SignatureHelpTriggerKind.TriggerCharacter };
            var signatureHelpParams = new SignatureHelpParams { TextDocument = textDocumentIdentifier, Position = projection.Position, Context = signatureHelpContext };

            var result = await csharpLspServer.ExecuteRequestAsync<SignatureHelpParams, SignatureHelp>(
                Methods.TextDocumentSignatureHelpName,
                signatureHelpParams, CancellationToken.None);

            var called = false;
            var expectedResult = new ReinvocationResponse<SignatureHelp>("LanguageClientName", result);

            var virtualCSharpUri = new Uri("C:/path/to/file.razor.g.cs");
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, SignatureHelp>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<TextDocumentPositionParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, TextDocumentPositionParams, CancellationToken>((textBuffer, method, clientName, definitionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentSignatureHelpName, method);
                    Assert.Equal(RazorLSPConstants.RazorCSharpLanguageServerName, clientName);
                    called = true;
                })
                .Returns(Task.FromResult(expectedResult));

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(documentUri, documentSnapshot);

            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker.Object, documentManager, testProjectionProvider, LoggerProvider);
            var signatureHelpRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
                Position = new Position(4, 10)
            };

            // Act
            var requestResult = await signatureHelpHandler.HandleRequestAsync(signatureHelpRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.Equal(expectedResult.Response, requestResult);
            Assert.Equal("void M(int a, int b, int c)", result.Signatures.First().Label);
        }

        [Fact]
        public async Task HandleRequestAsync_ReturnNullIfCSharpLanguageServerReturnsNull()
        {
            // Arrange
            var virtualCSharpUri = new Uri("C:/path/to/file.razor.g.cs");
            var requestInvoker = Mock.Of<LSPRequestInvoker>(i =>
                    i.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, SignatureHelp>(
                        It.IsAny<ITextBuffer>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<TextDocumentPositionParams>(),
                        It.IsAny<CancellationToken>()) == Task.FromResult(new ReinvocationResponse<SignatureHelp>("LanguageClient", null)), MockBehavior.Strict);

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker, DocumentManager, projectionProvider.Object, LoggerProvider);
            var signatureHelpRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5)
            };

            // Act
            var result = await signatureHelpHandler.HandleRequestAsync(signatureHelpRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }
    }
}
