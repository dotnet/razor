// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
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
    public class OnAutoInsertHandlerTest : HandlerTestBase
    {
        public OnAutoInsertHandlerTest()
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
        public async Task HandleRequestAsync_UnknownTriggerCharacter_DoesNotInvokeServer()
        {
            // Arrange
            var invokedServer = false;
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<VSInternalDocumentOnAutoInsertParams, VSInternalDocumentOnAutoInsertResponseItem>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<VSInternalDocumentOnAutoInsertParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, VSInternalDocumentOnAutoInsertParams, CancellationToken>((textBuffer, method, clientName, formattingParams, ct) => invokedServer = true)
                .Returns(Task.FromResult(new ReinvocationResponse<VSInternalDocumentOnAutoInsertResponseItem>("LanguageClientName", new VSInternalDocumentOnAutoInsertResponseItem() { TextEdit = new TextEdit() })));

            var projectionProvider = Mock.Of<LSPProjectionProvider>(MockBehavior.Strict);
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>(MockBehavior.Strict);

            var handler = new OnAutoInsertHandler(DocumentManager, requestInvoker.Object, projectionProvider, documentMappingProvider, LoggerProvider);
            var request = new VSInternalDocumentOnAutoInsertParams()
            {
                Character = "?",
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Options = new FormattingOptions()
                {
                    OtherOptions = new Dictionary<string, object>()
                },
                Position = new Position(0, 0)
            };

            // Act
            var response = await handler.HandleRequestAsync(request, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.False(invokedServer);
            Assert.Null(response);
        }

        [Fact]
        public async Task HandleRequestAsync_DocumentNotFound_DoesNotInvokeServer()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var invokedServer = false;
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<VSInternalDocumentOnAutoInsertParams, VSInternalDocumentOnAutoInsertResponseItem>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<VSInternalDocumentOnAutoInsertParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, VSInternalDocumentOnAutoInsertParams, CancellationToken>((textBuffer, method, clientName, formattingParams, ct) => invokedServer = true)
                .Returns(Task.FromResult(new ReinvocationResponse<VSInternalDocumentOnAutoInsertResponseItem>("LanguageClientName", new VSInternalDocumentOnAutoInsertResponseItem() { TextEdit = new TextEdit() })));

            var projectionProvider = Mock.Of<LSPProjectionProvider>(MockBehavior.Strict);
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>(MockBehavior.Strict);

            var handler = new OnAutoInsertHandler(documentManager, requestInvoker.Object, projectionProvider, documentMappingProvider, LoggerProvider);
            var request = new VSInternalDocumentOnAutoInsertParams()
            {
                Character = ">",
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Options = new FormattingOptions()
                {
                    OtherOptions = new Dictionary<string, object>()
                },
                Position = new Position(0, 0)
            };

            // Act
            var response = await handler.HandleRequestAsync(request, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.False(invokedServer);
            Assert.Null(response);
        }

        [Fact]
        public async Task HandleRequestAsync_RazorProjection_DoesNotInvokeServer()
        {
            // Arrange
            var invokedServer = false;
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<VSInternalDocumentOnAutoInsertParams, VSInternalDocumentOnAutoInsertResponseItem>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<VSInternalDocumentOnAutoInsertParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, VSInternalDocumentOnAutoInsertParams, CancellationToken>((textBuffer, method, clientName, formattingParams, ct) => invokedServer = true)
                .Returns(Task.FromResult(new ReinvocationResponse<VSInternalDocumentOnAutoInsertResponseItem>("LanguageClientName", new VSInternalDocumentOnAutoInsertResponseItem() { TextEdit = new TextEdit() })));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Razor,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>(MockBehavior.Strict);

            var handler = new OnAutoInsertHandler(DocumentManager, requestInvoker.Object, projectionProvider.Object, documentMappingProvider, LoggerProvider);
            var request = new VSInternalDocumentOnAutoInsertParams()
            {
                Character = ">",
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Options = new FormattingOptions()
                {
                    OtherOptions = new Dictionary<string, object>()
                },
                Position = new Position(0, 0)
            };

            // Act
            var response = await handler.HandleRequestAsync(request, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.False(invokedServer);
            Assert.Null(response);
        }

        [Fact]
        public async Task HandleRequestAsync_InvokesHTMLServer_RemapsEdits()
        {
            // Arrange
            var invokedServer = false;
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<VSInternalDocumentOnAutoInsertParams, VSInternalDocumentOnAutoInsertResponseItem>(
                    It.IsAny<ITextBuffer>(),
                    VSInternalMethods.OnAutoInsertName,
                    It.IsAny<string>(),
                    It.IsAny<VSInternalDocumentOnAutoInsertParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, VSInternalDocumentOnAutoInsertParams, CancellationToken>((textBuffer, method, serverContentType, formattingParams, ct) => invokedServer = true)
                .Returns(Task.FromResult(new ReinvocationResponse<VSInternalDocumentOnAutoInsertResponseItem>(
                    "LanguageClientName",
                    new VSInternalDocumentOnAutoInsertResponseItem() { TextEdit = new TextEdit() { Range = new Range(), NewText = "sometext" }, TextEditFormat = InsertTextFormat.Snippet })));

            var projectionUri = new Uri(Uri.AbsoluteUri + "__virtual.html");
            var projectionResult = new ProjectionResult()
            {
                Uri = projectionUri,
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>(MockBehavior.Strict);
            var handler = new OnAutoInsertHandler(DocumentManager, requestInvoker.Object, projectionProvider.Object, documentMappingProvider.Object, LoggerProvider);
            var request = new VSInternalDocumentOnAutoInsertParams()
            {
                Character = "=",
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Options = new FormattingOptions()
                {
                    OtherOptions = new Dictionary<string, object>()
                },
                Position = new Position(1, 4)
            };

            // Act
            var response = await handler.HandleRequestAsync(request, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(invokedServer);
            Assert.NotNull(response);
        }

        [Fact]
        public async Task HandleRequestAsync_InvokesCSharpServer_RemapsEdits()
        {
            // Arrange
            var invokedServer = false;
            var mappedTextEdits = false;
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<VSInternalDocumentOnAutoInsertParams, VSInternalDocumentOnAutoInsertResponseItem>(
                    It.IsAny<ITextBuffer>(),
                    VSInternalMethods.OnAutoInsertName,
                    It.IsAny<string>(),
                    It.IsAny<VSInternalDocumentOnAutoInsertParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, VSInternalDocumentOnAutoInsertParams, CancellationToken>((textBuffer, method, clientName, formattingParams, ct) => invokedServer = true)
                .Returns(Task.FromResult(new ReinvocationResponse<VSInternalDocumentOnAutoInsertResponseItem>("LanguageClientName", new VSInternalDocumentOnAutoInsertResponseItem() { TextEdit = new TextEdit() { Range = new Range(), NewText = "sometext" }, TextEditFormat = InsertTextFormat.Snippet })));

            var projectionUri = new Uri(Uri.AbsoluteUri + "__virtual.html");
            var projectionResult = new ProjectionResult()
            {
                Uri = projectionUri,
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>(MockBehavior.Strict);
            documentMappingProvider
                .Setup(d => d.RemapFormattedTextEditsAsync(projectionUri, It.IsAny<TextEdit[]>(), It.IsAny<FormattingOptions>(), /*containsSnippet*/ true, It.IsAny<CancellationToken>()))
                .Callback(() => mappedTextEdits = true)
                .Returns(Task.FromResult(new[] { new TextEdit() { NewText = "mapped-sometext" } }));

            var handler = new OnAutoInsertHandler(DocumentManager, requestInvoker.Object, projectionProvider.Object, documentMappingProvider.Object, LoggerProvider);
            var request = new VSInternalDocumentOnAutoInsertParams()
            {
                Character = "/",
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Options = new FormattingOptions()
                {
                    OtherOptions = new Dictionary<string, object>()
                },
                Position = new Position(1, 4)
            };

            // Act
            var response = await handler.HandleRequestAsync(request, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(invokedServer);
            Assert.True(mappedTextEdits);
            Assert.NotNull(response);
        }

        private class TestDocumentManager : LSPDocumentManager
        {
            private readonly Dictionary<Uri, LSPDocumentSnapshot> _documents = new();

            public override bool TryGetDocument(Uri uri, out LSPDocumentSnapshot lspDocumentSnapshot)
            {
                return _documents.TryGetValue(uri, out lspDocumentSnapshot);
            }

            public void AddDocument(Uri uri, LSPDocumentSnapshot documentSnapshot)
            {
                _documents.Add(uri, documentSnapshot);
            }
        }
    }
}
