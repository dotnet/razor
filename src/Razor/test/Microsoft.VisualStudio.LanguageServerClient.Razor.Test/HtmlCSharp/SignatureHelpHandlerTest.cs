// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
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
            var called = false;
            var expectedResult = new ReinvocationResponse<SignatureHelp>("LanguageClientName", new SignatureHelp());

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

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
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
