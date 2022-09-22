// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Mef;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Test;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Threading;
using Moq;
using Xunit;
using CompletionItem = Microsoft.VisualStudio.LanguageServer.Protocol.CompletionItem;
using CompletionOptions = Microsoft.VisualStudio.LanguageServer.Protocol.CompletionOptions;
using CompletionParams = Microsoft.VisualStudio.LanguageServer.Protocol.CompletionParams;
using CompletionTriggerKind = Microsoft.VisualStudio.LanguageServer.Protocol.CompletionTriggerKind;
using Position = Microsoft.VisualStudio.LanguageServer.Protocol.Position;
using TextDocumentIdentifier = Microsoft.VisualStudio.LanguageServer.Protocol.TextDocumentIdentifier;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [UseExportProvider]
    public class CompletionResolveHandlerTest : HandlerTestBase
    {
        public CompletionResolveHandlerTest()
        {
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            JoinableTaskContext = new JoinableTaskContext();
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext

            TextStructureNavigatorSelectorService = new TestTextStructureNavigatorSelectorService();
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

        private JoinableTaskContext JoinableTaskContext { get; }

        private ITextStructureNavigatorSelectorService TextStructureNavigatorSelectorService { get; }

        private TestDocumentManager DocumentManager { get; } = new();

        private TestLSPDocumentMappingProvider DocumentMappingProvider { get; } = new();

        private FormattingOptionsProvider FormattingOptionsProvider { get; } = TestFormattingOptionsProvider.Default;

        private CompletionRequestContextCache CompletionRequestContextCache { get; } = new();

        private Uri HostDocumentUri { get; }

        private TestTextBuffer TextBuffer { get; }

        private ServerCapabilities CompletionResolveServerCapabilities { get; } = new()
        {
            CompletionProvider = new CompletionOptions
            {
                ResolveProvider = true,
                AllCommitCharacters = CompletionRules.Default.DefaultCommitCharacters.Select(c => c.ToString()).ToArray(),
                TriggerCharacters = CompletionHandler.AllTriggerCharacters.ToArray(),
            }
        };

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_RemapsComplexTextEdit()
        {
            // Arrange
            var text =
                """
                @using System
                @code
                {
                    class C
                    {
                        override 
                    }
                }
                """;

            var cursorPosition = new Position(5, 17);
            var documentUri = new Uri("C:/path/to/file.razor");
            var completionParams = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
                Context = new VSInternalCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = " ",
                    InvokeKind = VSInternalCompletionInvokeKind.Typing
                },
                Position = cursorPosition
            };

            // Act
            var (unresolvedItem, resolvedItem, textEditRemapCount) = await ExecuteCSharpCompletionResolveRequestAsync(
                documentUri, text, completionParams, itemLabel: "Equals(object obj)").ConfigureAwait(false);

            // Assert
            Assert.True(unresolvedItem.VsResolveTextEditOnCommit);
            Assert.Null(unresolvedItem.TextEdit);
            Assert.NotNull(resolvedItem.TextEdit);
            Assert.Equal(1, textEditRemapCount);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_DoNotRemapNullTextEdit()
        {
            // Arrange
            var text =
                """
                @using System
                @DateTime.
                """;

            var cursorPosition = new Position(1, 10);
            var documentUri = new Uri("C:/path/to/file.razor");
            var completionParams = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
                Context = new VSInternalCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = ".",
                    InvokeKind = VSInternalCompletionInvokeKind.Typing
                },
                Position = cursorPosition
            };

            // Act
            var (unresolvedItem, resolvedItem, textEditRemapCount) = await ExecuteCSharpCompletionResolveRequestAsync(
                documentUri, text, completionParams, itemLabel: "Now").ConfigureAwait(false);

            // Assert
            Assert.Null(unresolvedItem.TextEdit);
            Assert.Null(resolvedItem.TextEdit);
            Assert.Equal(0, textEditRemapCount);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_PopulatesDescription()
        {
            // Arrange
            var text =
                """
                @using System
                @DateTime.
                """;

            var cursorPosition = new Position(1, 10);
            var documentUri = new Uri("C:/path/to/file.razor");
            var completionParams = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
                Context = new VSInternalCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = ".",
                    InvokeKind = VSInternalCompletionInvokeKind.Typing
                },
                Position = cursorPosition
            };

            // Act
            var (unresolvedItem, resolvedItem, _) = await ExecuteCSharpCompletionResolveRequestAsync(
                documentUri, text, completionParams, itemLabel: "Now").ConfigureAwait(false);

            // Assert
            Assert.Null(unresolvedItem.Description);
            Assert.NotNull(resolvedItem.Description);
        }

        [Fact]
        public async Task HandleRequestAsync_HtmlProjection_InvokesHtmlLanguageServer()
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

        private async Task<CompletionResolveResponse> ExecuteCSharpCompletionResolveRequestAsync(
            Uri documentUri,
            string text,
            CompletionParams completionParams,
            string itemLabel)
        {
            var csharpDocumentUri = new Uri("C:/path/to/file.razor__virtual.cs");
            var codeDocument = CreateCodeDocument(text, documentUri.AbsolutePath);
            var csharpSourceText = codeDocument.GetCSharpSourceText();

            var csharpDocumentSnapshot = CreateCSharpVirtualDocumentSnapshot(codeDocument, csharpDocumentUri.AbsoluteUri);
            var razorSourceText = codeDocument.GetSourceText();
            var documentSnapshot = new TestLSPDocumentSnapshot(
                documentUri,
                version: 1,
                snapshotContent: razorSourceText.ToString(),
                csharpDocumentSnapshot);

            var uriToCodeDocumentMap = new Dictionary<Uri, (int hostDocumentVersion, RazorCodeDocument codeDocument)>
            {
                { documentUri, (hostDocumentVersion: 1, codeDocument) }
            };
            var mappingProvider = new TestLSPDocumentMappingProvider(uriToCodeDocumentMap);
            var razorSpanMappingService = new TestRazorLSPSpanMappingService(mappingProvider, documentUri, razorSourceText, csharpSourceText);

            await using var csharpServer = await CSharpTestLspServerHelpers.CreateCSharpLspServerAsync(
                csharpSourceText, csharpDocumentUri, CompletionResolveServerCapabilities, razorSpanMappingService).ConfigureAwait(false);

            await csharpServer.OpenDocumentAsync(csharpDocumentUri, csharpSourceText.ToString()).ConfigureAwait(false);

            var requestInvoker = new TestLSPRequestInvoker(csharpServer);
            var documentManager = new TestDocumentManager(csharpServer);
            documentManager.AddDocument(documentUri, documentSnapshot);
            var projectionProvider = TestLSPProjectionProvider.Instance;

            // Execute completion request
            var completionHandler = new CompletionHandler(
                JoinableTaskContext,
                requestInvoker,
                documentManager,
                projectionProvider,
                TextStructureNavigatorSelectorService,
                CompletionRequestContextCache,
                FormattingOptionsProvider,
                LoggerProvider);
            var completionResult = await completionHandler.HandleRequestAsync(
                completionParams, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            var completionList = Assert.IsType<OptimizedVSCompletionList>(completionResult.Value.Value);
            var unresolvedCompletionItem = completionList.Items.Where(c => c.Label == itemLabel).Single();

            // Execute resolve request
            var resolveHandler = new CompletionResolveHandler(
                requestInvoker, documentManager, mappingProvider, FormattingOptionsProvider, CompletionRequestContextCache, LoggerProvider);
            var resolvedCompletionItem = await resolveHandler.HandleRequestAsync(
                unresolvedCompletionItem, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            var vsUnresolvedCompletionItem = Assert.IsType<VSInternalCompletionItem>(unresolvedCompletionItem);
            var vsResolvedCompletionItem = Assert.IsType<VSInternalCompletionItem>(resolvedCompletionItem);
            return new CompletionResolveResponse(vsUnresolvedCompletionItem, vsResolvedCompletionItem, mappingProvider.TextEditRemapCount);
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

        private record CompletionResolveResponse(VSInternalCompletionItem UnresolvedItem, VSInternalCompletionItem ResolvedItem, int TextEditRemapCount);
    }
}
