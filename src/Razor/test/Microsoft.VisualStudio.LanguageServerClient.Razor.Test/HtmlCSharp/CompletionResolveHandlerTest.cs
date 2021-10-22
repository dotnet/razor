// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class CompletionResolveHandlerTest : HandlerTestBase
    {
        private TestDocumentManager DocumentManager { get; } = new TestDocumentManager();

        private TestDocumentMappingProvider DocumentMappingProvider { get; } = new TestDocumentMappingProvider();

        private TestFormattingOptionsProvider FormattingOptionsProvider { get; } = new TestFormattingOptionsProvider();

        private CompletionRequestContextCache CompletionRequestContextCache { get; } = new CompletionRequestContextCache();

        private Uri HostDocumentUri { get; }

        private TestTextBuffer TextBuffer { get; }

        public CompletionResolveHandlerTest()
        {
            HostDocumentUri = new Uri("C:/path/to/file.razor");
            TextBuffer = new TestTextBuffer(new StringTextSnapshot(string.Empty));
            DocumentManager.AddDocument(
                HostDocumentUri,
                new TestLSPDocumentSnapshot(
                    HostDocumentUri,
                    version: 0,
                    new CSharpVirtualDocumentSnapshot(new Uri("C:/path/to/file.razor.g.cs"), TextBuffer.CurrentSnapshot, hostDocumentSyncVersion: 0),
                    new HtmlVirtualDocumentSnapshot(new Uri("C:/path/to/file.razor__virtual.html"), TextBuffer.CurrentSnapshot, hostDocumentSyncVersion: 0)));
        }

        [Fact]
        public async Task HandleRequestAsync_NonNullOriginalInsertText_DoesNotRemapTextEdit()
        {
            // Arrange
            var originalEdit = new TextEdit() { NewText = "original" };
            var mappedEdit = new TextEdit() { NewText = "mapped" };
            DocumentMappingProvider.AddMapping(originalEdit, mappedEdit);

            var requestedCompletionItem = new CompletionItem()
            {
                InsertText = "DateTime",
            };
            AssociateRequest(LanguageServerKind.CSharp, requestedCompletionItem, CompletionRequestContextCache, LoggerProvider);
            var resolvedCompletionItem = new CompletionItem()
            {
                TextEdit = originalEdit,
            };
            var requestInvoker = CreateRequestInvoker((method, languageServerName, completionItem) => resolvedCompletionItem);
            var handler = new CompletionResolveHandler(requestInvoker, DocumentManager, DocumentMappingProvider, FormattingOptionsProvider, CompletionRequestContextCache, LoggerProvider);

            // Act
            var result = await handler.HandleRequestAsync(requestedCompletionItem, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Same(originalEdit, result.TextEdit);
        }

        [Fact]
        public async Task HandleRequestAsync_NonNullOriginalTextEdit_DoesNotRemapTextEdit()
        {
            // Arrange
            var originalEdit = new TextEdit() { NewText = "original" };
            var mappedEdit = new TextEdit() { NewText = "mapped" };
            DocumentMappingProvider.AddMapping(originalEdit, mappedEdit);

            var requestedCompletionItem = new CompletionItem()
            {
                TextEdit = originalEdit,
            };
            AssociateRequest(LanguageServerKind.CSharp, requestedCompletionItem, CompletionRequestContextCache, LoggerProvider);
            var resolvedCompletionItem = new CompletionItem()
            {
                InsertText = "DateTime",
                TextEdit = originalEdit,
            };
            var requestInvoker = CreateRequestInvoker((method, languageServerName, completionItem) => resolvedCompletionItem);
            var handler = new CompletionResolveHandler(requestInvoker, DocumentManager, DocumentMappingProvider, FormattingOptionsProvider, CompletionRequestContextCache, LoggerProvider);

            // Act
            var result = await handler.HandleRequestAsync(requestedCompletionItem, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Same(originalEdit, result.TextEdit);
        }

        [Fact]
        public async Task HandleRequestAsync_ResolvedNullTextEdit_Noops()
        {
            // Arrange
            var requestedCompletionItem = new CompletionItem();
            AssociateRequest(LanguageServerKind.CSharp, requestedCompletionItem, CompletionRequestContextCache, LoggerProvider);
            var resolvedCompletionItem = new CompletionItem()
            {
                InsertText = "DateTime",
            };
            var requestInvoker = CreateRequestInvoker((method, languageServerName, completionItem) => resolvedCompletionItem);
            var handler = new CompletionResolveHandler(requestInvoker, DocumentManager, DocumentMappingProvider, FormattingOptionsProvider, CompletionRequestContextCache, LoggerProvider);

            // Act & Assert
            var result = await handler.HandleRequestAsync(requestedCompletionItem, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);
        }

        [Fact]
        public async Task HandleRequestAsync_NullInsertTextAndTextEdit_RemapsResolvedTextEdit()
        {
            // Arrange
            var originalEdit = new TextEdit() { NewText = "original" };
            var mappedEdit = new TextEdit() { NewText = "mapped" };
            DocumentMappingProvider.AddMapping(originalEdit, mappedEdit);
            var requestedCompletionItem = new CompletionItem();
            AssociateRequest(LanguageServerKind.CSharp, requestedCompletionItem, CompletionRequestContextCache, LoggerProvider);
            var resolvedCompletionItem = new CompletionItem()
            {
                TextEdit = originalEdit,
            };
            var requestInvoker = CreateRequestInvoker((method, languageServerName, completionItem) => resolvedCompletionItem);
            var handler = new CompletionResolveHandler(requestInvoker, DocumentManager, DocumentMappingProvider, FormattingOptionsProvider, CompletionRequestContextCache, LoggerProvider);

            // Act
            var result = await handler.HandleRequestAsync(requestedCompletionItem, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Same(mappedEdit, result.TextEdit);
        }

        [Fact]
        public async Task HandleRequestAsync_NullInsertTextAndTextEdit_RemapsAdditionalTextEdits()
        {
            // Arrange
            var originalEdit = new TextEdit() { NewText = "original" };
            var mappedEdit = new TextEdit() { NewText = "mapped" };
            DocumentMappingProvider.AddMapping(originalEdit, mappedEdit);
            var requestedCompletionItem = new CompletionItem();
            AssociateRequest(LanguageServerKind.CSharp, requestedCompletionItem, CompletionRequestContextCache, LoggerProvider);
            var resolvedCompletionItem = new CompletionItem()
            {
                AdditionalTextEdits = new[] { originalEdit },
            };
            var requestInvoker = CreateRequestInvoker((method, languageServerName, completionItem) => resolvedCompletionItem);
            var handler = new CompletionResolveHandler(requestInvoker, DocumentManager, DocumentMappingProvider, FormattingOptionsProvider, CompletionRequestContextCache, LoggerProvider);

            // Act
            var result = await handler.HandleRequestAsync(requestedCompletionItem, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Equal(new[] { mappedEdit }, result.AdditionalTextEdits);
        }

        [Fact]
        public async Task HandleRequestAsync_InvokesCSharpLanguageServer()
        {
            // Arrange
            var called = false;
            var originalData = new object();
            var request = new CompletionItem()
            {
                InsertText = "DateTime",
            };
            AssociateRequest(LanguageServerKind.CSharp, request, CompletionRequestContextCache, originalData);
            var expectedResponse = new CompletionItem()
            {
                InsertText = "DateTime",
                Data = originalData,
                Detail = "Some documentation"
            };
            var requestInvoker = CreateRequestInvoker((method, languageServerName, completionItem) =>
            {
                Assert.Equal(Methods.TextDocumentCompletionResolveName, method);
                Assert.Equal(RazorLSPConstants.RazorCSharpLanguageServerName, languageServerName);
                Assert.Same(originalData, completionItem.Data);
                called = true;

                return expectedResponse;
            });

            var handler = new CompletionResolveHandler(requestInvoker, DocumentManager, DocumentMappingProvider, FormattingOptionsProvider, CompletionRequestContextCache, LoggerProvider);

            // Act
            var result = await handler.HandleRequestAsync(request, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.Same(expectedResponse, result);
        }

        [Fact]
        public async Task HandleRequestAsync_InvokesHtmlLanguageServer()
        {
            // Arrange
            var called = false;
            var originalData = new object();
            var request = new CompletionItem()
            {
                InsertText = "strong",
            };
            AssociateRequest(LanguageServerKind.Html, request, CompletionRequestContextCache, originalData);
            var expectedResponse = new CompletionItem()
            {
                InsertText = "strong",
                Data = originalData,
                Detail = "Some documentation"
            };
            var requestInvoker = CreateRequestInvoker((method, languageServerName, completionItem) =>
            {
                Assert.Equal(Methods.TextDocumentCompletionResolveName, method);
                Assert.Equal(RazorLSPConstants.HtmlLanguageServerName, languageServerName);
                Assert.Same(originalData, completionItem.Data);
                called = true;
                return expectedResponse;
            });

            var handler = new CompletionResolveHandler(requestInvoker, DocumentManager, DocumentMappingProvider, FormattingOptionsProvider, CompletionRequestContextCache, LoggerProvider);

            // Act
            var result = await handler.HandleRequestAsync(request, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.Same(expectedResponse, result);
        }

        private LSPRequestInvoker CreateRequestInvoker(Func<string, string, CompletionItem, CompletionItem> reinvokeCallback)
        {
            CompletionItem response = null;
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionItem, CompletionItem>(
                    TextBuffer,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CompletionItem>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionItem, CancellationToken>(
                    (textBuffer, method, languageServerName, completionItem, ct) => response = reinvokeCallback(method, languageServerName, completionItem))
                .Returns(() => Task.FromResult(new ReinvocationResponse<CompletionItem>(languageClientName: "TestLanguageClient", response)));

            return requestInvoker.Object;
        }

        private static void AssociateRequest(LanguageServerKind requestKind, CompletionItem item, CompletionRequestContextCache cache, object originalData = null)
        {
            var documentUri = new Uri("C:/path/to/file.razor");
            var projectedUri = new Uri("C:/path/to/file.razor.g.xyz");
            var requestContext = new CompletionRequestContext(documentUri, projectedUri, requestKind);

            var resultId = cache.Set(requestContext);
            var data = new CompletionResolveData()
            {
                ResultId = resultId,
                OriginalData = originalData,
            };
            item.Data = data;
        }

        private class TestFormattingOptionsProvider : FormattingOptionsProvider
        {
            public override FormattingOptions GetOptions(Uri lspDocumentUri) => null;
        }

        private class TestDocumentMappingProvider : LSPDocumentMappingProvider
        {
            private readonly Dictionary<TextEdit, TextEdit> _mappings = new Dictionary<TextEdit, TextEdit>();

            public void AddMapping(TextEdit original, TextEdit mapping)
            {
                _mappings[original] = mapping;
            }

            public override Task<TextEdit[]> RemapFormattedTextEditsAsync(Uri uri, TextEdit[] edits, FormattingOptions options, bool containsSnippet, CancellationToken cancellationToken)
            {
                var newEdits = new List<TextEdit>();
                for (var i = 0; i < edits.Length; i++)
                {
                    if (_mappings.TryGetValue(edits[i], out var mappedEdit))
                    {
                        newEdits.Add(mappedEdit);
                    }
                }

                return Task.FromResult(newEdits.ToArray());
            }

            public override Task<RazorMapToDocumentRangesResponse> MapToDocumentRangesAsync(RazorLanguageKind languageKind, Uri razorDocumentUri, LanguageServer.Protocol.Range[] projectedRanges, CancellationToken cancellationToken) => throw new NotImplementedException();

            public override Task<RazorMapToDocumentRangesResponse> MapToDocumentRangesAsync(RazorLanguageKind languageKind, Uri razorDocumentUri, LanguageServer.Protocol.Range[] projectedRanges, LanguageServerMappingBehavior mappingBehavior, CancellationToken cancellationToken) => throw new NotImplementedException();

            public override Task<Location[]> RemapLocationsAsync(Location[] locations, CancellationToken cancellationToken) => throw new NotImplementedException();

            public override Task<TextEdit[]> RemapTextEditsAsync(Uri uri, TextEdit[] edits, CancellationToken cancellationToken) => throw new NotImplementedException();

            public override Task<WorkspaceEdit> RemapWorkspaceEditAsync(WorkspaceEdit workspaceEdit, CancellationToken cancellationToken) => throw new NotImplementedException();
        }
    }
}
