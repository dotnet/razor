// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
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
        }

        private Uri Uri { get; }

        private readonly ILanguageClient _languageClient = Mock.Of<ILanguageClient>(MockBehavior.Strict);

        [Fact]
        public async Task HandleRequestAsync_UnknownTriggerCharacter_DoesNotInvokeServer()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(s => s.Uri == Uri, MockBehavior.Strict));

            var invokedServer = false;
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<VSInternalDocumentOnAutoInsertParams, VSInternalDocumentOnAutoInsertResponseItem>(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<VSInternalDocumentOnAutoInsertParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, VSInternalDocumentOnAutoInsertParams, CancellationToken>((method, clientName, formattingParams, ct) => invokedServer = true)
                .Returns(Task.FromResult(new ReinvokeResponse<VSInternalDocumentOnAutoInsertResponseItem>(languageClient: _languageClient, new VSInternalDocumentOnAutoInsertResponseItem() { TextEdit = new TextEdit() })));

            var projectionProvider = Mock.Of<LSPProjectionProvider>(MockBehavior.Strict);
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>(MockBehavior.Strict);

            var handler = new OnAutoInsertHandler(documentManager, requestInvoker.Object, projectionProvider, documentMappingProvider, LoggerProvider);
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
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<VSInternalDocumentOnAutoInsertParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, VSInternalDocumentOnAutoInsertParams, CancellationToken>((method, clientName, formattingParams, ct) => invokedServer = true)
                .Returns(Task.FromResult(new ReinvokeResponse<VSInternalDocumentOnAutoInsertResponseItem>(languageClient: _languageClient, new VSInternalDocumentOnAutoInsertResponseItem() { TextEdit = new TextEdit() })));

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
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(s => s.Uri == Uri, MockBehavior.Strict));

            var invokedServer = false;
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<VSInternalDocumentOnAutoInsertParams, VSInternalDocumentOnAutoInsertResponseItem>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<VSInternalDocumentOnAutoInsertParams>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, VSInternalDocumentOnAutoInsertParams, CancellationToken>((method, clientName, formattingParams, ct) => invokedServer = true)
                .Returns(Task.FromResult(new ReinvokeResponse<VSInternalDocumentOnAutoInsertResponseItem>(languageClient: _languageClient, new VSInternalDocumentOnAutoInsertResponseItem() { TextEdit = new TextEdit() })));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Razor,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>(MockBehavior.Strict);

            var handler = new OnAutoInsertHandler(documentManager, requestInvoker.Object, projectionProvider.Object, documentMappingProvider, LoggerProvider);
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

        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/33677")]
        public async Task HandleRequestAsync_InvokesHTMLServer_RemapsEdits()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(s => s.Uri == Uri && s.Snapshot == Mock.Of<ITextSnapshot>(MockBehavior.Strict), MockBehavior.Strict));

            var invokedServer = false;
            var mappedTextEdits = false;
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<VSInternalDocumentOnAutoInsertParams, VSInternalDocumentOnAutoInsertResponseItem>(
                    VSInternalMethods.OnAutoInsertName,
                    It.IsAny<string>(),
                    It.IsAny<VSInternalDocumentOnAutoInsertParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, VSInternalDocumentOnAutoInsertParams, CancellationToken>((method, serverContentType, formattingParams, ct) => invokedServer = true)
                .Returns(Task.FromResult(new ReinvokeResponse<VSInternalDocumentOnAutoInsertResponseItem>(
                    languageClient: _languageClient,
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
            documentMappingProvider
                .Setup(d => d.RemapFormattedTextEditsAsync(projectionUri, It.IsAny<TextEdit[]>(), It.IsAny<FormattingOptions>(), /*containsSnippet*/ true, It.IsAny<CancellationToken>()))
                .Callback(() => mappedTextEdits = true)
                .Returns(Task.FromResult(new[] { new TextEdit() }));

            var handler = new OnAutoInsertHandler(documentManager, requestInvoker.Object, projectionProvider.Object, documentMappingProvider.Object, LoggerProvider);
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
            Assert.True(mappedTextEdits);
            Assert.NotNull(response);
        }

        [Fact]
        public async Task HandleRequestAsync_InvokesCSharpServer_RemapsEdits()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(s => s.Uri == Uri && s.Snapshot == Mock.Of<ITextSnapshot>(MockBehavior.Strict), MockBehavior.Strict));

            var invokedServer = false;
            var mappedTextEdits = false;
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<VSInternalDocumentOnAutoInsertParams, VSInternalDocumentOnAutoInsertResponseItem>(
                    VSInternalMethods.OnAutoInsertName,
                    It.IsAny<string>(),
                    It.IsAny<VSInternalDocumentOnAutoInsertParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, VSInternalDocumentOnAutoInsertParams, CancellationToken>((method, clientName, formattingParams, ct) => invokedServer = true)
                .Returns(Task.FromResult(new ReinvokeResponse<VSInternalDocumentOnAutoInsertResponseItem>(languageClient: _languageClient, new VSInternalDocumentOnAutoInsertResponseItem() { TextEdit = new TextEdit() { Range = new Range(), NewText = "sometext" }, TextEditFormat = InsertTextFormat.Snippet })));

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

            var handler = new OnAutoInsertHandler(documentManager, requestInvoker.Object, projectionProvider.Object, documentMappingProvider.Object, LoggerProvider);
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
            private readonly Dictionary<Uri, LSPDocumentSnapshot> _documents = new Dictionary<Uri, LSPDocumentSnapshot>();

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
