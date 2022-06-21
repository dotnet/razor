// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Test;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [UseExportProvider]
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

        private ServerCapabilities RenameServerCapabilities { get; } = new()
        {
            RenameProvider = true
        };

        [Fact]
        public async Task HandleRequestAsync_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var requestInvoker = new TestLSPRequestInvoker();
            var projectionProvider = TestLSPProjectionProvider.Instance;
            var documentMappingProvider = new TestLSPDocumentMappingProvider(new Dictionary<Uri, (int, RazorCodeDocument)>());
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
            var requestInvoker = new TestLSPRequestInvoker();
            var projectionProvider = TestLSPProjectionProvider.Instance;
            var documentMappingProvider = new TestLSPDocumentMappingProvider(new Dictionary<Uri, (int, RazorCodeDocument)>());
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
        public async Task HandleRequestAsync_CSharpProjection_BasicRename()
        {
            // Arrange
            var text =
                """
                @code
                {
                    void Method()
                    {
                        Method();
                    }
                }
                """;

            var cursorPosition = new Position(2, 9);
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
                csharpSourceText, csharpDocumentUri, RenameServerCapabilities).ConfigureAwait(false);

            var requestInvoker = new TestLSPRequestInvoker(csharpServer);
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(documentUri, documentSnapshot);
            var projectionProvider = TestLSPProjectionProvider.Instance;

            var documentMappingProvider = new DefaultLSPDocumentMappingProvider(requestInvoker, new Lazy<LSPDocumentManager>(() => documentManager));
            var renameHandler = new RenameHandler(requestInvoker, documentManager, projectionProvider, documentMappingProvider, LoggerProvider);
            var renameRequest = new RenameParams()
            {
                Position = cursorPosition,
                NewName = "NewName",
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
            };

            // Act
            var result = await renameHandler.HandleRequestAsync(renameRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
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
