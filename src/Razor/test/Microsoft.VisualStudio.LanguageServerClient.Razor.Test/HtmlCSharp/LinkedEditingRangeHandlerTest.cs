// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class LinkedEditingRangeHandlerTest : HandlerTestBase
    {
        private static readonly Uri s_uri = new Uri("C:/path/to/file.razor");

        private static readonly ILanguageClient _languageClient = Mock.Of<ILanguageClient>(MockBehavior.Strict);

        [Fact]
        public async Task HandleRequestAsync_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var requestInvoker = Mock.Of<LSPRequestInvoker>(MockBehavior.Strict);
            var projectionProvider = Mock.Of<LSPProjectionProvider>(MockBehavior.Strict);
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>(MockBehavior.Strict);
            var onLinkedEditingRangeHandler = new LinkedEditingRangeHandler(documentManager, requestInvoker, projectionProvider, documentMappingProvider, LoggerProvider);
            var onLinkedEditingRangeRequest = new LinkedEditingRangeParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = s_uri },
                Position = new Position(0, 1)
            };

            // Act
            var result = await onLinkedEditingRangeHandler.HandleRequestAsync(onLinkedEditingRangeRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_ProjectionNotFound_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(s_uri, Mock.Of<LSPDocumentSnapshot>(MockBehavior.Strict));
            var requestInvoker = Mock.Of<LSPRequestInvoker>(MockBehavior.Strict);
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict).Object;
            Mock.Get(projectionProvider).Setup(projectionProvider => projectionProvider.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), CancellationToken.None))
                .Returns(Task.FromResult<ProjectionResult>(null));
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>(MockBehavior.Strict);
            var onLinkedEditingRangeHandler = new LinkedEditingRangeHandler(documentManager, requestInvoker, projectionProvider, documentMappingProvider, LoggerProvider);
            var onLinkedEditingRangeRequest = new LinkedEditingRangeParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = s_uri },
                Position = new Position(0, 1)
            };

            // Act
            var result = await onLinkedEditingRangeHandler.HandleRequestAsync(onLinkedEditingRangeRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(s_uri, Mock.Of<LSPDocumentSnapshot>(d => d.Version == 0, MockBehavior.Strict));
            var requestInvoker = Mock.Of<LSPRequestInvoker>(MockBehavior.Strict);
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>(MockBehavior.Strict);

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = GetProjectionProvider(projectionResult);

            var onLinkedEditingRangeHandler = new LinkedEditingRangeHandler(documentManager, requestInvoker, projectionProvider, documentMappingProvider, LoggerProvider);
            var onLinkedEditingRangeRequest = new LinkedEditingRangeParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = s_uri },
                Position = new Position(10, 5)
            };

            // Act
            var result = await onLinkedEditingRangeHandler.HandleRequestAsync(onLinkedEditingRangeRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_HtmlProjection_RemapsHighlightRange()
        {
            // Arrange
            var invokerCalled = false;
            var expectedResponse = GetMatchingHTMLBracketRange(5);
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(s_uri, Mock.Of<LSPDocumentSnapshot>(d => d.Version == 0, MockBehavior.Strict));

            var htmlResponse = GetMatchingHTMLBracketRange(10);
            var requestInvoker = GetRequestInvoker<LinkedEditingRangeParams, LinkedEditingRanges>(
                htmlResponse,
                (method, clientName, highlightParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentLinkedEditingRangeName, method);
                    Assert.Equal(RazorLSPConstants.HtmlLanguageServerName, clientName);
                    invokerCalled = true;
                });

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = GetProjectionProvider(projectionResult);

            var documentMappingProvider = GetDocumentMappingProvider(expectedResponse.Ranges, 0, RazorLanguageKind.Html);

            var onLinkedEditingRangeHandler = new LinkedEditingRangeHandler(documentManager, requestInvoker, projectionProvider, documentMappingProvider, LoggerProvider);
            var onLinkedEditingRangeRequest = new LinkedEditingRangeParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = s_uri },
                Position = new Position(10, 5)
            };

            // Act
            var result = await onLinkedEditingRangeHandler.HandleRequestAsync(onLinkedEditingRangeRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(invokerCalled);
            Assert.NotNull(result);
            Assert.Collection(result.Ranges,
                r => Assert.Equal(expectedResponse.Ranges[0], r),
                r => Assert.Equal(expectedResponse.Ranges[1], r));
        }

        [Fact]
        public async Task HandleRequestAsync_VersionMismatch_ReturnsNull()
        {
            // Arrange
            var invokerCalled = false;
            var expectedResponse = GetMatchingHTMLBracketRange(5);
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(s_uri, Mock.Of<LSPDocumentSnapshot>(d => d.Version == 1, MockBehavior.Strict));

            var htmlResponse = GetMatchingHTMLBracketRange(10);
            var requestInvoker = GetRequestInvoker<LinkedEditingRangeParams, LinkedEditingRanges>(
                htmlResponse,
                (method, clientName, highlightParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentLinkedEditingRangeName, method);
                    Assert.Equal(RazorLSPConstants.HtmlLanguageServerName, clientName);
                    invokerCalled = true;
                });

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = GetProjectionProvider(projectionResult);

            var documentMappingProvider = GetDocumentMappingProvider(expectedResponse.Ranges, 0 /* Different from document version (1) */, RazorLanguageKind.Html);

            var onLinkedEditingRangeHandler = new LinkedEditingRangeHandler(documentManager, requestInvoker, projectionProvider, documentMappingProvider, LoggerProvider);
            var onLinkedEditingRangeRequest = new LinkedEditingRangeParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = s_uri },
                Position = new Position(10, 5)
            };

            // Act
            var result = await onLinkedEditingRangeHandler.HandleRequestAsync(onLinkedEditingRangeRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(invokerCalled);
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_RemapFailure_ReturnsNull()
        {
            // Arrange
            var invokerCalled = false;
            var expectedResponse = GetMatchingHTMLBracketRange(5);
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(s_uri, Mock.Of<LSPDocumentSnapshot>(d => d.Version == 0, MockBehavior.Strict));

            var htmlResponse = GetMatchingHTMLBracketRange(10);
            var requestInvoker = GetRequestInvoker<LinkedEditingRangeParams, LinkedEditingRanges>(
                htmlResponse,
                (method, clientName, highlightParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentLinkedEditingRangeName, method);
                    Assert.Equal(RazorLSPConstants.HtmlLanguageServerName, clientName);
                    invokerCalled = true;
                });

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = GetProjectionProvider(projectionResult);

            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>(MockBehavior.Strict).Object;
            Mock.Get(documentMappingProvider).Setup(p => p.MapToDocumentRangesAsync(RazorLanguageKind.Html, s_uri, It.IsAny<Range[]>(), CancellationToken.None))
                .Returns(Task.FromResult<RazorMapToDocumentRangesResponse>(null));

            var onLinkedEditingRangeHandler = new LinkedEditingRangeHandler(documentManager, requestInvoker, projectionProvider, documentMappingProvider, LoggerProvider);
            var onLinkedEditingRangeRequest = new LinkedEditingRangeParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = s_uri },
                Position = new Position(10, 5)
            };

            // Act
            var result = await onLinkedEditingRangeHandler.HandleRequestAsync(onLinkedEditingRangeRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(invokerCalled);
            Assert.Null(result);
        }

        private static LSPProjectionProvider GetProjectionProvider(ProjectionResult expectedResult)
        {
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(expectedResult));

            return projectionProvider.Object;
        }

        private static LSPRequestInvoker GetRequestInvoker<TParams, TResult>(TResult expectedResponse, Action<string, string, TParams, CancellationToken> callback)
        {
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<TParams, TResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TParams>(), It.IsAny<CancellationToken>()))
                .Callback(callback)
                .Returns(Task.FromResult(new ReinvokeResponse<TResult>(_languageClient, expectedResponse)));

            return requestInvoker.Object;
        }

        private static LSPDocumentMappingProvider GetDocumentMappingProvider(Range[] expectedRanges, int expectedVersion, RazorLanguageKind languageKind)
        {
            var remappingResult = new RazorMapToDocumentRangesResponse()
            {
                Ranges = expectedRanges,
                HostDocumentVersion = expectedVersion
            };
            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>(MockBehavior.Strict);
            documentMappingProvider.Setup(d => d.MapToDocumentRangesAsync(languageKind, s_uri, It.IsAny<Range[]>(), It.IsAny<CancellationToken>())).
                Returns(Task.FromResult(remappingResult));

            return documentMappingProvider.Object;
        }

        private static LinkedEditingRanges GetMatchingHTMLBracketRange(int line)
        {
            return new LinkedEditingRanges()
            {
                Ranges = new[] {
                    new Range()
                    {
                        Start = new Position(line, 1),
                        End = new Position(line, 10)
                    },
                    new Range()
                    {
                        Start = new Position(line, 21),
                        End = new Position(line, 30)
                    }
                }
            };
        }
    }
}
