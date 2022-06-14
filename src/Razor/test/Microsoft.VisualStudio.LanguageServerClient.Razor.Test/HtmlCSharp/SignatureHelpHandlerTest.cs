// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Test;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [UseExportProvider]
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
            DefaultDocumentManager = new TestDocumentManager();
            DefaultDocumentManager.AddDocument(Uri, documentSnapshot);
        }

        private Uri Uri { get; }

        private TestDocumentManager DefaultDocumentManager { get; }

        private ServerCapabilities SignatureHelpServerCapabilities { get; } = new()
        {
            SignatureHelpProvider = new SignatureHelpOptions
            {
                TriggerCharacters = new string[] { "(", "," }
            }
        };

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
            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker, DefaultDocumentManager, projectionProvider, LoggerProvider);
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

            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker.Object, DefaultDocumentManager, projectionProvider.Object, LoggerProvider);
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

            var documentUri = new Uri("C:/path/to/file.razor");
            var csharpDocumentUri = new Uri("C:/path/to/file.razor__virtual.cs");
            var codeDocument = CreateCodeDocument(text, documentUri.AbsoluteUri);
            var csharpSourceText = codeDocument.GetCSharpSourceText();

            var csharpDocumentSnapshot = CreateCSharpVirtualDocumentSnapshot(codeDocument, csharpDocumentUri.AbsoluteUri);
            var documentSnapshot = new TestLSPDocumentSnapshot(
                documentUri,
                version: 1,
                snapshotContent: codeDocument.GetSourceText().ToString(),
                csharpDocumentSnapshot);

            await using var csharpServer = await CSharpTestLspServerHelpers.CreateCSharpLspServerAsync(
                csharpSourceText, csharpDocumentUri, SignatureHelpServerCapabilities).ConfigureAwait(false);

            var requestInvoker = new TestLSPRequestInvoker(csharpServer);
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(documentUri, documentSnapshot);

            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker, documentManager, TestLSPProjectionProvider.Instance, LoggerProvider);
            var signatureHelpRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
                Position = cursorPosition
            };

            // Act
            var requestResult = await signatureHelpHandler.HandleRequestAsync(
                signatureHelpRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Equal("void M(int a, int b, int c)", requestResult.Signatures.First().Label);
        }

        [Fact]
        public async Task HandleRequestAsync_ReturnNullIfCSharpLanguageServerReturnsNull()
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
            var cursorPosition = new Position { Line = 2, Character = 0 };

            var documentUri = new Uri("C:/path/to/file.razor");
            var csharpDocumentUri = new Uri("C:/path/to/file.razor__virtual.cs");
            var codeDocument = CreateCodeDocument(text, documentUri.AbsoluteUri);
            var csharpSourceText = codeDocument.GetCSharpSourceText();

            var csharpDocumentSnapshot = CreateCSharpVirtualDocumentSnapshot(codeDocument, csharpDocumentUri.AbsoluteUri);
            var documentSnapshot = new TestLSPDocumentSnapshot(
                documentUri,
                version: 1,
                snapshotContent: codeDocument.GetSourceText().ToString(),
                csharpDocumentSnapshot);

            await using var csharpServer = await CSharpTestLspServerHelpers.CreateCSharpLspServerAsync(
                csharpSourceText, csharpDocumentUri, SignatureHelpServerCapabilities).ConfigureAwait(false);

            var requestInvoker = new TestLSPRequestInvoker(csharpServer);
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(documentUri, documentSnapshot);

            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker, documentManager, TestLSPProjectionProvider.Instance, LoggerProvider);
            var signatureHelpRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
                Position = cursorPosition
            };

            // Act
            var requestResult = await signatureHelpHandler.HandleRequestAsync(
                signatureHelpRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(requestResult);
        }
    }
}
