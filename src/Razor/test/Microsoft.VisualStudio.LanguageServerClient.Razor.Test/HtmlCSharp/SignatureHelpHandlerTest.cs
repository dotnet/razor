﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
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
        }

        private Uri Uri { get; }
        private readonly ILanguageClient _languageClient = Mock.Of<ILanguageClient>(MockBehavior.Strict);

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
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(MockBehavior.Strict));
            var requestInvoker = Mock.Of<LSPRequestInvoker>(MockBehavior.Strict);
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict).Object;
            Mock.Get(projectionProvider).Setup(projectionProvider => projectionProvider.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), CancellationToken.None))
                .Returns(Task.FromResult<ProjectionResult>(null));
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
        public async Task HandleRequestAsync_HtmlProjection_InvokesHtmlLanguageServer_ReturnsItem()
        {
            // Arrange
            var called = false;
            var expectedResult = new ReinvokeResponse<SignatureHelp>(_languageClient, new SignatureHelp());
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(MockBehavior.Strict));

            var virtualHtmlUri = new Uri("C:/path/to/file.razor__virtual.html");
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, SignatureHelp>(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<TextDocumentPositionParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, TextDocumentPositionParams, CancellationToken>((method, clientName, definitionParams, ct) =>
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

            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker.Object, documentManager, projectionProvider.Object, LoggerProvider);
            var signatureHelpRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5)
            };

            // Act
            var result = await signatureHelpHandler.HandleRequestAsync(signatureHelpRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.Equal(expectedResult.Result, result);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_InvokesCSharpLanguageServer_ReturnsItem()
        {
            // Arrange
            var called = false;
            var expectedResult = new ReinvokeResponse<SignatureHelp>(_languageClient, new SignatureHelp());
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(MockBehavior.Strict));

            var virtualCSharpUri = new Uri("C:/path/to/file.razor.g.cs");
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, SignatureHelp>(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<TextDocumentPositionParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, TextDocumentPositionParams, CancellationToken>((method, clientName, definitionParams, ct) =>
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

            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker.Object, documentManager, projectionProvider.Object, LoggerProvider);
            var signatureHelpRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5)
            };

            // Act
            var result = await signatureHelpHandler.HandleRequestAsync(signatureHelpRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.Equal(expectedResult.Result, result);
        }

        [Fact]
        public async Task HandleRequestAsync_ReturnNullIfCSharpLanguageServerReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(d => d.Version == 123, MockBehavior.Strict));

            var virtualCSharpUri = new Uri("C:/path/to/file.razor.g.cs");
            var requestInvoker = Mock.Of<LSPRequestInvoker>(i =>
                    i.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, SignatureHelp>(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<TextDocumentPositionParams>(),
                        It.IsAny<CancellationToken>()) == Task.FromResult(new ReinvokeResponse<SignatureHelp>(null, null)), MockBehavior.Strict);

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker, documentManager, projectionProvider.Object, LoggerProvider);
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
