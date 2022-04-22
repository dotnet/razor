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
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class RenameHandlerTest : HandlerTestBase
    {
        public RenameHandlerTest()
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
            var renameHandler = new RenameHandler(requestInvoker, documentManager, projectionProvider, documentMappingProvider, LoggerProvider);
            var renameRequest = new RenameParams()
            {
                Position = new Position(0, 1),
                NewName = "NewName",
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
            };

            // Act
            var result = await renameHandler.HandleRequestAsync(renameRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

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
            var renameHandler = new RenameHandler(requestInvoker, DocumentManager, projectionProvider, documentMappingProvider, LoggerProvider);
            var renameRequest = new RenameParams()
            {
                Position = new Position(0, 1),
                NewName = "NewName",
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
            };

            // Act
            var result = await renameHandler.HandleRequestAsync(renameRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_HtmlProjection_RemapsWorkspaceEdit()
        {
            // Arrange
            var called = false;
            var expectedEdit = new WorkspaceEdit();

            var requestInvoker = GetRequestInvoker<RenameParams, WorkspaceEdit>(
                new WorkspaceEdit(),
                (textBuffer, method, clientName, renameParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentRenameName, method);
                    Assert.Equal(RazorLSPConstants.HtmlLanguageServerName, clientName);
                    called = true;
                });

            var projectionProvider = GetProjectionProvider(new ProjectionResult() { LanguageKind = RazorLanguageKind.Html });
            var documentMappingProvider = GetDocumentMappingProvider(expectedEdit);

            var renameHandler = new RenameHandler(requestInvoker, DocumentManager, projectionProvider, documentMappingProvider, LoggerProvider);
            var renameRequest = new RenameParams()
            {
                Position = new Position(0, 1),
                NewName = "NewName",
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
            };

            // Act
            var result = await renameHandler.HandleRequestAsync(renameRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.Equal(expectedEdit, result);

            // Actual remapping behavior is tested in LSPDocumentMappingProvider tests.
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_RemapsWorkspaceEdit()
        {
            // Arrange
            var called = false;
            var expectedEdit = new WorkspaceEdit();

            var requestInvoker = GetRequestInvoker<RenameParams, WorkspaceEdit>(
                new WorkspaceEdit(),
                (textBuffer, method, clientName, renameParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentRenameName, method);
                    Assert.Equal(RazorLSPConstants.RazorCSharpLanguageServerName, clientName);
                    called = true;
                });

            var projectionProvider = GetProjectionProvider(new ProjectionResult() { LanguageKind = RazorLanguageKind.CSharp });
            var documentMappingProvider = GetDocumentMappingProvider(expectedEdit);

            var renameHandler = new RenameHandler(requestInvoker, DocumentManager, projectionProvider, documentMappingProvider, LoggerProvider);
            var renameRequest = new RenameParams()
            {
                Position = new Position(0, 1),
                NewName = "NewName",
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
            };

            // Act
            var result = await renameHandler.HandleRequestAsync(renameRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.Equal(expectedEdit, result);

            // Actual remapping behavior is tested in LSPDocumentMappingProvider tests.
        }

        private static LSPProjectionProvider GetProjectionProvider(ProjectionResult expectedResult)
        {
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(expectedResult));

            return projectionProvider.Object;
        }

        private LSPRequestInvoker GetRequestInvoker<TParams, TResult>(TResult expectedResponse, Action<ITextBuffer, string, string, TParams, CancellationToken> callback)
        {
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<TParams, TResult>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<TParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback(callback)
                .Returns(Task.FromResult(new ReinvocationResponse<TResult>("LanguageClientName", expectedResponse)));

            return requestInvoker.Object;
        }

        private static LSPDocumentMappingProvider GetDocumentMappingProvider(WorkspaceEdit expectedEdit)
        {
            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>(MockBehavior.Strict);
            documentMappingProvider.Setup(d => d.RemapWorkspaceEditAsync(It.IsAny<WorkspaceEdit>(), It.IsAny<CancellationToken>())).
                Returns(Task.FromResult(expectedEdit));

            return documentMappingProvider.Object;
        }
    }
}
