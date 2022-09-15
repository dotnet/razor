// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
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
    public class GoToDefinitionHandlerTest : HandlerTestBase
    {
        public GoToDefinitionHandlerTest()
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
            var requestInvoker = Mock.Of<LSPRequestInvoker>(MockBehavior.Strict);
            var projectionProvider = Mock.Of<LSPProjectionProvider>(MockBehavior.Strict);
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>(MockBehavior.Strict);
            var definitionHandler = new GoToDefinitionHandler(requestInvoker, new TestDocumentManager(), projectionProvider, documentMappingProvider, LoggerProvider);
            var definitionRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1)
            };

            // Act
            var result = await definitionHandler.HandleRequestAsync(definitionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

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
            var definitionHandler = new GoToDefinitionHandler(requestInvoker, DocumentManager, projectionProvider, documentMappingProvider, LoggerProvider);
            var definitionRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1)
            };

            // Act
            var result = await definitionHandler.HandleRequestAsync(definitionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_HtmlProjection_InvokesHtmlLanguageServer()
        {
            // Arrange
            var invokedLSPRequest = false;
            var invokedRemapRequest = false;
            var expectedLocation = GetLocation(5, 5, 5, 5, Uri);

            var virtualHtmlUri = new Uri("C:/path/to/file.razor__virtual.html");
            var htmlLocation = GetLocation(100, 100, 100, 100, virtualHtmlUri);
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, Location[]>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<TextDocumentPositionParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, TextDocumentPositionParams, CancellationToken>((textBuffer, method, clientName, definitionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentDefinitionName, method);
                    Assert.Equal(RazorLSPConstants.HtmlLanguageServerName, clientName);
                    invokedLSPRequest = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<Location[]>("LanguageClientName", new[] { htmlLocation })));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>(MockBehavior.Strict);
            documentMappingProvider
                .Setup(d => d.RemapLocationsAsync(It.IsAny<Location[]>(), It.IsAny<CancellationToken>()))
                .Callback<Location[], CancellationToken>((locations, token) =>
                {
                    Assert.Equal(htmlLocation, locations[0]);
                    invokedRemapRequest = true;
                })
                .Returns(Task.FromResult(Array.Empty<Location>()));

            var definitionHandler = new GoToDefinitionHandler(requestInvoker.Object, DocumentManager, projectionProvider.Object, documentMappingProvider.Object, LoggerProvider);
            var definitionRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5)
            };

            // Act
            var result = await definitionHandler.HandleRequestAsync(definitionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(invokedLSPRequest);
            Assert.True(invokedRemapRequest);

            // Actual remapping behavior is tested elsewhere.
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_InvokesCSharpLanguageServer()
        {
            // Arrange
            var invokedLSPRequest = false;
            var invokedRemapRequest = false;
            var expectedLocation = GetLocation(5, 5, 5, 5, Uri);

            var virtualCSharpUri = new Uri("C:/path/to/file.razor.g.cs");
            var csharpLocation = GetLocation(100, 100, 100, 100, virtualCSharpUri);
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, Location[]>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<TextDocumentPositionParams>(), It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, TextDocumentPositionParams, CancellationToken>((textBuffer, method, clientName, definitionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentDefinitionName, method);
                    Assert.Equal(RazorLSPConstants.RazorCSharpLanguageServerName, clientName);
                    invokedLSPRequest = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<Location[]>("LanguageClientName", new[] { csharpLocation })));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>(MockBehavior.Strict);
            documentMappingProvider
                .Setup(d => d.RemapLocationsAsync(It.IsAny<Location[]>(), It.IsAny<CancellationToken>()))
                .Callback<Location[], CancellationToken>((locations, token) =>
                {
                    Assert.Equal(csharpLocation, locations[0]);
                    invokedRemapRequest = true;
                })
                .Returns(Task.FromResult(Array.Empty<Location>()));

            var definitionHandler = new GoToDefinitionHandler(requestInvoker.Object, DocumentManager, projectionProvider.Object, documentMappingProvider.Object, LoggerProvider);
            var definitionRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5)
            };

            // Act
            var result = await definitionHandler.HandleRequestAsync(definitionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(invokedLSPRequest);
            Assert.True(invokedRemapRequest);

            // Actual remapping behavior is tested elsewhere.
        }

        private static Location GetLocation(int startLine, int startCharacter, int endLine, int endCharacter, Uri uri)
        {
            var location = new Location()
            {
                Uri = uri,
                Range = new Range()
                {
                    Start = new Position(startLine, startCharacter),
                    End = new Position(endLine, endCharacter)
                }
            };

            return location;
        }
    }
}
