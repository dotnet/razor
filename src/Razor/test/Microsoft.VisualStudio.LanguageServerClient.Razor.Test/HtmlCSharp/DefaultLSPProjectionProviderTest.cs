// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class DefaultLSPProjectionProviderTest : HandlerTestBase
    {
        public DefaultLSPProjectionProviderTest()
        {
            var htmlUri = new Uri("file:///some/folder/to/file.razor__virtual.html");
            HtmlVirtualDocumentSnapshot = new HtmlVirtualDocumentSnapshot(htmlUri, new StringTextSnapshot(string.Empty), 1);

            var csharpUri = new Uri("file:///some/folder/to/file.razor__virtual.cs");
            CSharpVirtualDocumentSnapshot = new CSharpVirtualDocumentSnapshot(csharpUri, new StringTextSnapshot(string.Empty), 1);

            var uri = new Uri("file:///some/folder/to/file.razor");
            DocumentSnapshot = new TestLSPDocumentSnapshot(uri, version: 0, "Some Content", HtmlVirtualDocumentSnapshot, CSharpVirtualDocumentSnapshot);
        }

        private LSPDocumentSnapshot DocumentSnapshot { get; }

        private HtmlVirtualDocumentSnapshot HtmlVirtualDocumentSnapshot { get; }

        private CSharpVirtualDocumentSnapshot CSharpVirtualDocumentSnapshot { get; }

        [Fact]
        public async Task GetProjectionAsync_RazorProjection_ReturnsNull()
        {
            // Arrange
            var response = new RazorLanguageQueryResponse()
            {
                Kind = RazorLanguageKind.Razor
            };
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<RazorLanguageQueryParams, RazorLanguageQueryResponse>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    RazorLSPConstants.RazorLanguageServerName,
                    It.IsAny<Func<JToken, bool>>(),
                    It.IsAny<RazorLanguageQueryParams>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new ReinvocationResponse<RazorLanguageQueryResponse>("LanguageClient", response)));

            var documentSynchronizer = new Mock<LSPDocumentSynchronizer>(MockBehavior.Strict);

            var projectionProvider = new DefaultLSPProjectionProvider(requestInvoker.Object, documentSynchronizer.Object, Mock.Of<RazorLogger>(MockBehavior.Strict), LoggerProvider);

            // Act
            var result = await projectionProvider.GetProjectionAsync(DocumentSnapshot, new Position(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetProjectionAsync_HtmlProjection_Synchronizes_ReturnsProjection()
        {
            // Arrange
            var expectedPosition = new Position(0, 0);
            var response = new RazorLanguageQueryResponse()
            {
                Kind = RazorLanguageKind.Html,
                HostDocumentVersion = 1,
                Position = new Position(expectedPosition.Line, expectedPosition.Character)
            };
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<RazorLanguageQueryParams, RazorLanguageQueryResponse>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    RazorLSPConstants.RazorLanguageServerName,
                    It.IsAny<Func<JToken, bool>>(),
                    It.IsAny<RazorLanguageQueryParams>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new ReinvocationResponse<RazorLanguageQueryResponse>("LanguageClient", response)));

            var documentSynchronizer = new Mock<LSPDocumentSynchronizer>(MockBehavior.Strict);
            documentSynchronizer
                .Setup(d => d.TrySynchronizeVirtualDocumentAsync(DocumentSnapshot.Version, HtmlVirtualDocumentSnapshot, true, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            var projectionProvider = new DefaultLSPProjectionProvider(requestInvoker.Object, documentSynchronizer.Object, Mock.Of<RazorLogger>(MockBehavior.Strict), LoggerProvider);

            // Act
            var result = await projectionProvider.GetProjectionAsync(DocumentSnapshot, new Position(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(HtmlVirtualDocumentSnapshot.Uri, result.Uri);
            Assert.Equal(RazorLanguageKind.Html, result.LanguageKind);
            Assert.Equal(expectedPosition, result.Position);
        }

        [Fact]
        public async Task GetProjectionAsync_CSharpProjection_Synchronizes_ReturnsProjection()
        {
            // Arrange
            var expectedPosition = new Position(0, 0);
            var response = new RazorLanguageQueryResponse()
            {
                Kind = RazorLanguageKind.CSharp,
                HostDocumentVersion = 1,
                Position = new Position(expectedPosition.Line, expectedPosition.Character)
            };
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<RazorLanguageQueryParams, RazorLanguageQueryResponse>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    RazorLSPConstants.RazorLanguageServerName,
                    It.IsAny<Func<JToken, bool>>(),
                    It.IsAny<RazorLanguageQueryParams>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new ReinvocationResponse<RazorLanguageQueryResponse>("LanguageClient", response)));

            var documentSynchronizer = new Mock<LSPDocumentSynchronizer>(MockBehavior.Strict);
            documentSynchronizer
                .Setup(d => d.TrySynchronizeVirtualDocumentAsync(DocumentSnapshot.Version, CSharpVirtualDocumentSnapshot, true, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            var projectionProvider = new DefaultLSPProjectionProvider(requestInvoker.Object, documentSynchronizer.Object, Mock.Of<RazorLogger>(MockBehavior.Strict), LoggerProvider);

            // Act
            var result = await projectionProvider.GetProjectionAsync(DocumentSnapshot, new Position(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(CSharpVirtualDocumentSnapshot.Uri, result.Uri);
            Assert.Equal(RazorLanguageKind.CSharp, result.LanguageKind);
            Assert.Equal(expectedPosition, result.Position);
        }

        [Fact]
        public async Task GetProjectionAsync_UndefinedHostDocumentVersionResponse_ReturnsProjection()
        {
            // Arrange
            var expectedPosition = new Position(0, 0);
            var response = new RazorLanguageQueryResponse()
            {
                Kind = RazorLanguageKind.Html,
                HostDocumentVersion = null,
                Position = new Position(expectedPosition.Line, expectedPosition.Character)
            };
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<RazorLanguageQueryParams, RazorLanguageQueryResponse>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    RazorLSPConstants.RazorLanguageServerName,
                    It.IsAny<Func<JToken, bool>>(),
                    It.IsAny<RazorLanguageQueryParams>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new ReinvocationResponse<RazorLanguageQueryResponse>("LanguageClient", response)));

            var documentSynchronizer = new Mock<LSPDocumentSynchronizer>(MockBehavior.Strict);
            documentSynchronizer
                .Setup(d => d.TrySynchronizeVirtualDocumentAsync(DocumentSnapshot.Version, HtmlVirtualDocumentSnapshot, true, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            var logger = new Mock<RazorLogger>(MockBehavior.Strict);
            logger.Setup(l => l.LogVerbose(It.IsAny<string>())).Verifiable();
            var projectionProvider = new DefaultLSPProjectionProvider(requestInvoker.Object, documentSynchronizer.Object, logger.Object, LoggerProvider);

            // Act
            var result = await projectionProvider.GetProjectionAsync(DocumentSnapshot, new Position(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(HtmlVirtualDocumentSnapshot.Uri, result.Uri);
            Assert.Equal(RazorLanguageKind.Html, result.LanguageKind);
            Assert.Equal(expectedPosition, result.Position);
        }

        [Fact]
        public async Task GetProjectionForCompletionAsync_CSharpProjection_ReturnsProjection()
        {
            // Arrange
            var expectedPosition = new Position(0, 0);
            var response = new RazorLanguageQueryResponse()
            {
                Kind = RazorLanguageKind.CSharp,
                HostDocumentVersion = 1,
                Position = new Position(expectedPosition.Line, expectedPosition.Character)
            };
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<RazorLanguageQueryParams, RazorLanguageQueryResponse>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    RazorLSPConstants.RazorLanguageServerName,
                    It.IsAny<Func<JToken, bool>>(),
                    It.IsAny<RazorLanguageQueryParams>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new ReinvocationResponse<RazorLanguageQueryResponse>("LanguageClient", response)));

            var documentSynchronizer = new Mock<LSPDocumentSynchronizer>(MockBehavior.Strict);
            documentSynchronizer
                .Setup(d => d.TrySynchronizeVirtualDocumentAsync(DocumentSnapshot.Version, CSharpVirtualDocumentSnapshot, false, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            var projectionProvider = new DefaultLSPProjectionProvider(requestInvoker.Object, documentSynchronizer.Object, Mock.Of<RazorLogger>(MockBehavior.Strict), LoggerProvider);

            // Act
            var result = await projectionProvider.GetProjectionForCompletionAsync(DocumentSnapshot, new Position(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(CSharpVirtualDocumentSnapshot.Uri, result.Uri);
            Assert.Equal(RazorLanguageKind.CSharp, result.LanguageKind);
            Assert.Equal(expectedPosition, result.Position);
        }

        [Fact]
        public async Task GetProjectionAsync_SynchronizationFails_ReturnsNull()
        {
            // Arrange
            var expectedPosition = new Position(0, 0);
            var response = new RazorLanguageQueryResponse()
            {
                Kind = RazorLanguageKind.CSharp,
                HostDocumentVersion = 1,
                Position = new Position(expectedPosition.Line, expectedPosition.Character)
            };
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<RazorLanguageQueryParams, RazorLanguageQueryResponse>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    RazorLSPConstants.RazorLanguageServerName,
                    It.IsAny<Func<JToken, bool>>(),
                    It.IsAny<RazorLanguageQueryParams>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new ReinvocationResponse<RazorLanguageQueryResponse>("LanguageClient", response)));

            var documentSynchronizer = new Mock<LSPDocumentSynchronizer>(MockBehavior.Strict);
            documentSynchronizer
                .Setup(d => d.TrySynchronizeVirtualDocumentAsync(DocumentSnapshot.Version, CSharpVirtualDocumentSnapshot, true, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            var projectionProvider = new DefaultLSPProjectionProvider(requestInvoker.Object, documentSynchronizer.Object, Mock.Of<RazorLogger>(MockBehavior.Strict), LoggerProvider);

            // Act
            var result = await projectionProvider.GetProjectionAsync(DocumentSnapshot, new Position(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }
    }
}
