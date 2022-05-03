// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Moq;
using Xunit;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class UriPresentationHandlerTest : HandlerTestBase
    {
        public UriPresentationHandlerTest()
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
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>(MockBehavior.Strict);
            var handler = new UriPresentationHandler(requestInvoker, documentManager, projectionProvider, documentMappingProvider, LoggerProvider);
            var request = new VSInternalUriPresentationParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Range = new Range
                {
                    Start = new Position(0, 1),
                    End = new Position(0, 2)
                }
            };

            // Act
            var result = await handler.HandleRequestAsync(request, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

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
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>(MockBehavior.Strict);
            var handler = new UriPresentationHandler(requestInvoker, DocumentManager, projectionProvider, documentMappingProvider, LoggerProvider);
            var request = new VSInternalUriPresentationParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Range = new Range
                {
                    Start = new Position(0, 1),
                    End = new Position(0, 2)
                }
            };

            // Act
            var result = await handler.HandleRequestAsync(request, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_HtmlProjection_InvokesHtmlLanguageServer()
        {
            // Arrange
            var called = false;

            var lspResponse = new WorkspaceEdit();

            var request = new VSInternalUriPresentationParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Range = new Range
                {
                    Start = new Position(0, 1),
                    End = new Position(0, 2)
                }
            };

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<VSInternalUriPresentationParams, WorkspaceEdit>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<VSInternalUriPresentationParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, VSInternalUriPresentationParams, CancellationToken>((textBuffer, method, clientName, hoverParams, ct) =>
                {
                    Assert.Equal(VSInternalMethods.TextDocumentUriPresentationName, method);
                    Assert.Equal(RazorLSPConstants.HtmlLanguageServerName, clientName);
                    called = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<WorkspaceEdit>("LanguageClientName", lspResponse)));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var remappingResult = new RazorMapToDocumentRangesResponse()
            {
                Ranges = new[] {
                    new Range()
                    {
                        Start = new Position(0, 0),
                        End = new Position(0, 1)
                    }
                },
                HostDocumentVersion = 0
            };
            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>(MockBehavior.Strict);
            documentMappingProvider.Setup(d => d.RemapWorkspaceEditAsync(It.IsAny<WorkspaceEdit>(), It.IsAny<CancellationToken>())).
                Returns(Task.FromResult(lspResponse));

            var handler = new UriPresentationHandler(requestInvoker.Object, DocumentManager, projectionProvider.Object, documentMappingProvider.Object, LoggerProvider);

            // Act
            var result = await handler.HandleRequestAsync(request, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_InvokesCSharpLanguageServer()
        {
            // Arrange
            var called = false;

            var lspResponse = new WorkspaceEdit();

            var request = new VSInternalUriPresentationParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Range = new Range
                {
                    Start = new Position(0, 1),
                    End = new Position(0, 2)
                }
            };

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<VSInternalUriPresentationParams, WorkspaceEdit>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<VSInternalUriPresentationParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, VSInternalUriPresentationParams, CancellationToken>((textBuffer, method, clientName, hoverParams, ct) =>
                {
                    Assert.Equal(VSInternalMethods.TextDocumentUriPresentationName, method);
                    Assert.Equal(RazorLSPConstants.RazorCSharpLanguageServerName, clientName);
                    called = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<WorkspaceEdit>("LanguageClientName", lspResponse)));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var remappingResult = new RazorMapToDocumentRangesResponse()
            {
                Ranges = new[] {
                    new Range()
                    {
                        Start = new Position(0, 0),
                        End = new Position(0, 1)
                    }
                },
                HostDocumentVersion = 0
            };
            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>(MockBehavior.Strict);
            documentMappingProvider.Setup(d => d.RemapWorkspaceEditAsync(It.IsAny<WorkspaceEdit>(), It.IsAny<CancellationToken>())).
                Returns(Task.FromResult(lspResponse));

            var handler = new UriPresentationHandler(requestInvoker.Object, DocumentManager, projectionProvider.Object, documentMappingProvider.Object, LoggerProvider);

            // Act
            var result = await handler.HandleRequestAsync(request, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
        }
    }
}
