// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Threading;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class CompletionHandlerTest : HandlerTestBase
    {
        public CompletionHandlerTest()
        {
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            JoinableTaskContext = new JoinableTaskContext();
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext

            var navigatorSelector = BuildNavigatorSelector(new TextExtent(new SnapshotSpan(new StringTextSnapshot("@{ }"), new Span(0, 0)), isSignificant: false));
            TextStructureNavigatorSelectorService = navigatorSelector;

            Uri = new Uri("C:/path/to/file.razor");

            CompletionRequestContextCache = new CompletionRequestContextCache();
            FormattingOptionsProvider = TestFormattingOptionsProvider.Default;

            TextBuffer = new TestTextBuffer(new StringTextSnapshot(string.Empty));
            CSharpVirtualDocumentSnapshot = new CSharpVirtualDocumentSnapshot(new Uri("C:/path/to/file.razor.g.cs"), TextBuffer.CurrentSnapshot, 0);
            HtmlVirtualDocumentSnapshot = new HtmlVirtualDocumentSnapshot(new Uri("C:/path/to/file.razor__virtual.html"), TextBuffer.CurrentSnapshot, 0);
        }

        private JoinableTaskContext JoinableTaskContext { get; }

        private ITextStructureNavigatorSelectorService TextStructureNavigatorSelectorService { get; }

        private CompletionRequestContextCache CompletionRequestContextCache { get; }

        private FormattingOptionsProvider FormattingOptionsProvider { get; }

        private TestTextBuffer TextBuffer { get; }

        private CSharpVirtualDocumentSnapshot CSharpVirtualDocumentSnapshot { get; }

        private HtmlVirtualDocumentSnapshot HtmlVirtualDocumentSnapshot { get; }

        private Uri Uri { get; }

        private readonly string _languageClient = "languageClient";

        [Fact]
        public async Task HandleRequestAsync_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var requestInvoker = Mock.Of<LSPRequestInvoker>(MockBehavior.Strict);
            var projectionProvider = Mock.Of<LSPProjectionProvider>(MockBehavior.Strict);
            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker, documentManager, projectionProvider, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext() { TriggerKind = CompletionTriggerKind.Invoked },
                Position = new Position(0, 1)
            };

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_ProjectionNotFound_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot));
            var requestInvoker = Mock.Of<LSPRequestInvoker>(MockBehavior.Strict);
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict).Object;
            Mock.Get(projectionProvider).Setup(projectionProvider => projectionProvider.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), CancellationToken.None))
                .Returns(Task.FromResult<ProjectionResult>(null));
            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker, documentManager, projectionProvider, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext() { TriggerKind = CompletionTriggerKind.Invoked },
                Position = new Position(0, 1)
            };

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_WithRestrictChildren_RestrictsChildren()
        {
            // Arrange
            var called = false;
            var expectedItems = new CompletionList
            {
                Items = new[] {
                    new CompletionItem() { InsertText = "Sample" },
                    new CompletionItem(){ Label = "div" },
                    new CompletionItem(){ Label = "img" },
                    new CompletionItem(){ Label = "p" },
                }
            };
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new VSInternalCompletionContext() { TriggerKind = CompletionTriggerKind.TriggerCharacter, TriggerCharacter = "<", InvokeKind = VSInternalCompletionInvokeKind.Typing },
                Position = new Position(1, 0),
            };

            var documentManager = new TestDocumentManager();
            var documentSnapshot = new TestLSPDocumentSnapshot(
                new Uri("C:/path/file.razor"),
                version: 0,
                snapshotContent: @"<Restricted>
<
</Restricted>",
                HtmlVirtualDocumentSnapshot);
            documentManager.AddDocument(Uri, documentSnapshot);

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(TextBuffer, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CompletionParams>(), It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, clientName, completionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentCompletionName, method);
                    Assert.Equal(RazorLSPConstants.HtmlLanguageServerName, clientName);
                    var vsCompletionContext = Assert.IsType<VSInternalCompletionContext>(completionParams.Context);
                    Assert.Equal(VSInternalCompletionInvokeKind.Typing, vsCompletionContext.InvokeKind);
                    called = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, expectedItems)));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.Collection(((CompletionList)result.Value).Items,
                (i) => Assert.Equal("p", i.Label),
                (i) => Assert.Equal("div", i.Label));

        }

        [Fact]
        public async Task HandleRequestAsync_HtmlProjection_InvokesHtmlLanguageServer()
        {
            // Arrange
            var called = false;
            var expectedItem = new CompletionItem() { InsertText = "Sample" };
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new VSInternalCompletionContext() { TriggerKind = CompletionTriggerKind.TriggerCharacter, TriggerCharacter = "<", InvokeKind = VSInternalCompletionInvokeKind.Typing },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, HtmlVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(TextBuffer, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CompletionParams>(), It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, clientName, completionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentCompletionName, method);
                    Assert.Equal(RazorLSPConstants.HtmlLanguageServerName, clientName);
                    var vsCompletionContext = Assert.IsType<VSInternalCompletionContext>(completionParams.Context);
                    Assert.Equal(VSInternalCompletionInvokeKind.Typing, vsCompletionContext.InvokeKind);
                    called = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, new[] { expectedItem })));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            var item = Assert.Single(((CompletionList)result.Value).Items);
            Assert.Equal(expectedItem.InsertText, item.InsertText);
        }

        [Fact]
        public async Task HandleRequestAsync_247Typing_InvokesLanguageServerWithExplicit()
        {
            // Arrange
            var languageKind = RazorLanguageKind.CSharp;
            var called = false;
            var expectedItem = new CompletionItem() { Label = "Sampel", InsertText = "Sample" };
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new VSInternalCompletionContext() { TriggerKind = CompletionTriggerKind.Invoked, InvokeKind = VSInternalCompletionInvokeKind.Typing },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, HtmlVirtualDocumentSnapshot, CSharpVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(
                    It.IsAny<ITextBuffer>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CompletionParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, clientName, completionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentCompletionName, method);
                    var vsCompletionContext = Assert.IsType<VSInternalCompletionContext>(completionParams.Context);
                    Assert.Equal(VSInternalCompletionInvokeKind.Explicit, vsCompletionContext.InvokeKind);
                    called = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, new[] { expectedItem })));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = languageKind
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            var item = Assert.Single(((CompletionList)result.Value).Items);
            Assert.Equal(expectedItem.InsertText, item.InsertText);
        }

        [Fact]
        public async Task HandleRequestAsync_NoCompletions()
        {
            // Arrange
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new VSInternalCompletionContext() { TriggerKind = CompletionTriggerKind.TriggerCharacter, TriggerCharacter = "<", InvokeKind = VSInternalCompletionInvokeKind.Typing },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, HtmlVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(TextBuffer, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CompletionParams>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(string.Empty, null)))
                .Verifiable();

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            requestInvoker.VerifyAll();
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_InvokesCSharpLanguageServer()
        {
            // Arrange
            var called = false;
            var expectedItem = new CompletionItem() { InsertText = "DateTime", Label = "DateTime" };
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new VSInternalCompletionContext() { TriggerKind = CompletionTriggerKind.Invoked, InvokeKind = VSInternalCompletionInvokeKind.Explicit },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(TextBuffer, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CompletionParams>(), It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, clientName, completionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentCompletionName, method);
                    Assert.Equal(RazorLSPConstants.RazorCSharpLanguageServerName, clientName);
                    var vsCompletionContext = Assert.IsType<VSInternalCompletionContext>(completionParams.Context);
                    Assert.Equal(VSInternalCompletionInvokeKind.Explicit, vsCompletionContext.InvokeKind);
                    called = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, new[] { expectedItem })));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            var item = ((CompletionList)result.Value).Items.First();
            Assert.Equal(expectedItem.InsertText, item.InsertText);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_DoNotReturnKeywordsWithoutAtAsync()
        {
            // Arrange
            var called = false;
            var expectedItem = new CompletionItem() { InsertText = "DateTime", Label = "DateTime" };
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext() { TriggerKind = CompletionTriggerKind.Invoked },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(
                    TextBuffer,
                    Methods.TextDocumentCompletionName,
                    RazorLSPConstants.RazorCSharpLanguageServerName,
                    It.IsAny<CompletionParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, _, completionParams, ct) => called = true)
                .Returns(Task.FromResult(
                    new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(
                        _languageClient,
                    new CompletionList
                    {
                        Items = new[] { expectedItem }
                    })));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.True(result.HasValue);
            var _ = result.Value.Match<SumType<CompletionItem[], CompletionList>>(
                array => throw new NotImplementedException(),
                list =>
                {
                    Assert.Collection(list.Items,
                        item => Assert.Equal("DateTime", item.Label)
                    );

                    return list;
                });
        }

        [Fact]
        public void HandleRequestAsync_CSharpProjection_RewriteCompletionContext()
        {
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = "@",
                },
                Position = new Position(0, 1),
            };

            var rewrittenContext = CompletionHandler.RewriteContext(completionRequest.Context, RazorLanguageKind.CSharp);
            Assert.True(rewrittenContext.TriggerKind == CompletionTriggerKind.Invoked);
            Assert.True(((VSInternalCompletionContext)rewrittenContext).InvokeKind == VSInternalCompletionInvokeKind.Explicit);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_DoNotPreselectAfterAt()
        {
            // Arrange
            var called = false;
            var expectedItem = new CompletionItem() { InsertText = "AccessViolationException", Label = "AccessViolationException", Preselect = true };
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = "@",
                },
                Position = new Position(0, 1),
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(
                    TextBuffer,
                    Methods.TextDocumentCompletionName,
                    RazorLSPConstants.RazorCSharpLanguageServerName,
                    It.IsAny<CompletionParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, clientName, completionParams, ct) => called = true)
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, new CompletionList
                {
                    Items = new[] { expectedItem }
                })));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.True(result.HasValue);
            var _ = result.Value.Match<SumType<CompletionItem[], CompletionList>>(
                array => throw new NotImplementedException(),
                list =>
                {
                    var violationException = list.Items.FirstOrDefault(item => item.Label == "AccessViolationException");
                    Assert.NotNull(violationException);
                    Assert.False(violationException.Preselect);

                    return list;
                });
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_ReturnsKeywordsFromRazor()
        {
            // Arrange
            var called = false;
            var expectedItem = new CompletionItem() { InsertText = "DateTime", Label = "DateTime" };
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext() { TriggerKind = CompletionTriggerKind.Invoked },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(TextBuffer, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CompletionParams>(), It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, clientName, completionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentCompletionName, method);
                    Assert.Equal(RazorLSPConstants.RazorCSharpLanguageServerName, clientName);
                    called = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, new CompletionList
                {
                    Items = new[] { expectedItem }
                })));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.True(result.HasValue);
            var _ = result.Value.Match<SumType<CompletionItem[], CompletionList>>(
                array => throw new NotImplementedException(),
                list =>
                {
                    Assert.Collection(list.Items,
                        item => Assert.Equal("DateTime", item.Label)
                    );

                    return list;
                });
        }

        [Fact]
        public async Task HandleRequestAsync_HtmlProjection_IncompatibleTriggerCharacter_ReturnsNull()
        {
            // Arrange
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext() { TriggerKind = CompletionTriggerKind.TriggerCharacter, TriggerCharacter = "~" },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_IncompatibleTriggerCharacter_ReturnsNull()
        {
            // Arrange
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext() { TriggerKind = CompletionTriggerKind.TriggerCharacter, TriggerCharacter = "&" },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_IdentifierTriggerCharacter_InvokesCSharpLanguageServerNull()
        {
            // Arrange
            var called = false;
            var expectedItem = new CompletionItem() { InsertText = "DateTime", Label = "DateTime" };
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext() { TriggerKind = CompletionTriggerKind.TriggerCharacter, TriggerCharacter = "a" },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(
                    TextBuffer,
                    Methods.TextDocumentCompletionName,
                    RazorLSPConstants.RazorCSharpLanguageServerName,
                    It.IsAny<CompletionParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, clientName, completionParams, ct) => called = true)
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, new[] { expectedItem })));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.NotEmpty(((CompletionList)result.Value).Items);
            var item = ((CompletionList)result.Value).Items.First();
            Assert.Equal(expectedItem.InsertText, item.InsertText);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_TransitionTriggerCharacter_InvokesCSharpLanguageServerWithInvoke()
        {
            // Arrange
            var called = false;
            var expectedItem = new CompletionItem() { InsertText = "DateTime", Label = "DateTime" };
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext() { TriggerKind = CompletionTriggerKind.TriggerCharacter, TriggerCharacter = "a" },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(TextBuffer, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CompletionParams>(), It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, clientName, completionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentCompletionName, method);
                    Assert.Equal(RazorLSPConstants.RazorCSharpLanguageServerName, clientName);
                    Assert.Equal(CompletionTriggerKind.Invoked, completionParams.Context.TriggerKind);
                    called = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, new[] { expectedItem })));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.NotEmpty(((CompletionList)result.Value).Items);
            var item = ((CompletionList)result.Value).Items.First();
            Assert.Equal(expectedItem.InsertText, item.InsertText);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_ReturnOnlyPropertiesIfInsideInitializer()
        {
            // Arrange
            var called = false;
            var expectedItems = new CompletionItem[] {
                 new CompletionItem() { Kind = CompletionItemKind.Property, Label = "DayOfWeek" },
                 new CompletionItem() { Kind = CompletionItemKind.Property, Label = "Kind" },
            };

            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext() { TriggerKind = CompletionTriggerKind.Invoked },
                Position = new Position(4, 9)
            };

            var documentManager = new TestDocumentManager();
            var content = @"@using System
@{
    var date = new DateTime()
    {
        
    };
}";
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, snapshotContent: content, CSharpVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(TextBuffer, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CompletionParams>(), It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, clientName, completionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentCompletionName, method);
                    Assert.Equal(RazorLSPConstants.RazorCSharpLanguageServerName, clientName);
                    called = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, expectedItems)));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, BuildNavigatorSelector(new TextExtent(new SnapshotSpan(new StringTextSnapshot(content), new Span(57, 9)), isSignificant: false)), CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.True(result.HasValue);
            var _ = result.Value.Match<SumType<CompletionItem[], CompletionList>>(
                array => throw new NotImplementedException(),
                list =>
                {
                    Assert.Collection(list.Items,
                        item => Assert.Equal("DayOfWeek", item.Label),
                        item => Assert.Equal("Kind", item.Label)
                    );

                    return list;
                });
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_RemoveAllDesignTimeHelpers()
        {
            // Arrange
            var called = false;
            var expectedItems = new CompletionItem[]
            {
                new CompletionItem() { InsertText = "BuildRenderTree", Label = "BuildRenderTree" },
                new CompletionItem() { InsertText = "DateTime", Label = "DateTime" },
                new CompletionItem() { InsertText = "__o", Label = "__o" },
                new CompletionItem() { InsertText = "__RazorDirectiveTokenHelpers__", Label = "__RazorDirectiveTokenHelpers__" },
                new CompletionItem() { InsertText = "_Imports", Label = "_Imports" },
            };

            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext() { TriggerKind = CompletionTriggerKind.TriggerCharacter, TriggerCharacter = "@" },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(TextBuffer, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CompletionParams>(), It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, clientName, completionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentCompletionName, method);
                    Assert.Equal(RazorLSPConstants.RazorCSharpLanguageServerName, clientName);
                    called = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, expectedItems)));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.True(result.HasValue);
            _ = result.Value.Match<SumType<CompletionItem[], CompletionList>>(
                array => throw new NotImplementedException(),
                list =>
                {
                    Assert.Collection(list.Items,
                        item => Assert.Equal("DateTime", item.InsertText),
                        item => Assert.Equal("for (...)", item.Label),
                        item => Assert.Equal("foreach (...)", item.Label),
                        item => Assert.Equal("if (...)", item.Label),
                        item => Assert.Equal("prop", item.Label)
                    );

                    return list;
                });
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_OnlyRemoveCommonDesignTimeHelpers()
        {
            // Arrange
            var called = false;
            var expectedItems = new CompletionItem[] {
                new CompletionItem() { InsertText = "__RazorDirectiveTokenHelpers__", Label = "__RazorDirectiveTokenHelpers__" },
                new CompletionItem() { InsertText = "__o", Label = "__o" },
                new CompletionItem() { InsertText = "__x", Label = "__x" },
                new CompletionItem() { InsertText = "_Imports", Label = "_Imports" },
            };

            // Requesting completion at:
            //     @{ void M() { var __x = 1; __[||] } }
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext() { TriggerKind = CompletionTriggerKind.Invoked },
                Position = new Position(0, 29)
            };

            var documentSnapshot = new TestLSPDocumentSnapshot(
                new Uri("C:/path/file.razor"),
                version: 0,
                snapshotContent: "@{ void M() { var __x = 1; __ } }",
                CSharpVirtualDocumentSnapshot);
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, documentSnapshot);

            var wordSnapshotSpan = new SnapshotSpan(documentSnapshot.Snapshot, new Span(27, 2));
            var wordRange = new TextExtent(wordSnapshotSpan, isSignificant: true);
            var navigatorSelector = BuildNavigatorSelector(wordRange);
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(TextBuffer, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CompletionParams>(), It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, clientName, completionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentCompletionName, method);
                    Assert.Equal(RazorLSPConstants.RazorCSharpLanguageServerName, clientName);
                    called = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, expectedItems)));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, navigatorSelector, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.True(result.HasValue);
            _ = result.Value.Match<SumType<CompletionItem[], CompletionList>>(
                array => throw new NotImplementedException(),
                list =>
                {
                    Assert.Collection(list.Items, item => Assert.Equal("__x", item.Label));
                    return list;
                });
        }

        [Fact]
        public async Task HandleRequestAsync_HtmlProjection_IdentifierTriggerCharacter_InvokesHtmlLanguageServer()
        {
            // Arrange
            var called = false;
            var expectedItem = new CompletionItem() { InsertText = "Sample" };
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext() { TriggerKind = CompletionTriggerKind.TriggerCharacter, TriggerCharacter = "h" },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, HtmlVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(TextBuffer, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CompletionParams>(), It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, clientName, completionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentCompletionName, method);
                    Assert.Equal(RazorLSPConstants.HtmlLanguageServerName, clientName);
                    called = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, new[] { expectedItem })));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            var item = Assert.Single(((CompletionList)result.Value).Items);
            Assert.Equal(expectedItem.InsertText, item.InsertText);
        }

        [Fact]
        public void SetResolveData_RewritesData()
        {
            // Arrange
            var originalData = new object();
            var items = new[]
            {
                new CompletionItem() { InsertText = "Hello", Data = originalData }
            };
            var completionList = new CompletionList()
            {
                Items = items,
            };
            _ = new TestDocumentManager();
            _ = new Mock<LSPRequestInvoker>(MockBehavior.Strict).Object;
            _ = new Mock<LSPProjectionProvider>(MockBehavior.Strict).Object;

            // Act
            CompletionHandler.SetResolveData(123, completionList);

            // Assert
            var item = Assert.Single(completionList.Items);
            var newData = Assert.IsType<CompletionResolveData>(item.Data);
            Assert.Same(originalData, newData.OriginalData);
        }

        [Fact]
        public void SetResolveData_RewritesCompletionListData()
        {
            // Arrange
            var originalData = new object();
            var completionList = new VSInternalCompletionList()
            {
                Items = new[]
                {
                    new CompletionItem() { InsertText = "Hello" }
                },
                Data = originalData
            };
            _ = new TestDocumentManager();
            _ = new Mock<LSPRequestInvoker>(MockBehavior.Strict).Object;
            _ = new Mock<LSPProjectionProvider>(MockBehavior.Strict).Object;

            // Act
            CompletionHandler.SetResolveData(123, completionList);

            // Assert
            var newData = Assert.IsType<CompletionResolveData>(completionList.Data);
            Assert.Same(originalData, newData.OriginalData);
        }

        [Fact]
        public async Task HandleRequestAsync_ProvisionalCompletion()
        {
            // Arrange
            var called = false;
            var expectedItem = new CompletionItem() { Label = "Sample", InsertText = "Sample" };
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = "."
                },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, HtmlVirtualDocumentSnapshot, CSharpVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(TextBuffer, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CompletionParams>(), It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, clientName, completionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentCompletionName, method);
                    Assert.Equal(RazorLSPConstants.RazorCSharpLanguageServerName, clientName);
                    called = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, new[] { expectedItem })));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
                Position = new Position(1, 7)
            };
            var virtualDocumentUri = new Uri("C:/path/to/file.razor__virtual.cs");
            var previousCharacterProjection = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
                Position = new Position(100, 10),
                PositionIndex = 1000,
                Uri = virtualDocumentUri,
                HostDocumentVersion = 1,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), new Position(1, 6), It.IsAny<CancellationToken>())).Returns(Task.FromResult(previousCharacterProjection));
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), completionRequest.Position, It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            var item = Assert.Single(((CompletionList)result.Value).Items);
            Assert.Equal(expectedItem.InsertText, item.InsertText);
        }

        [Fact]
        public async Task TryGetProvisionalCompletionsAsync_CSharpProjection_ReturnsFalse()
        {
            // Arrange
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = "."
                },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var (succeeded, result) = await completionHandler.TryGetProvisionalCompletionsAsync(completionRequest, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot), projectionResult, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.False(succeeded);
            Assert.Null(result);
        }

        [Fact]
        public async Task TryGetProvisionalCompletionsAsync_RevertsDotOnFailures()
        {
            // Arrange
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = "."
                },
                Position = new Position(0, 1)
            };

            var virtualDocumentUri = new Uri("C:/path/to/file.razor__virtual.cs");

            var documentManager = new TestDocumentManager();

            var languageServerCalled = false;
            var expectedItem = new CompletionItem() { InsertText = "DateTime" };
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(
                    TextBuffer,
                    It.IsAny<string>(),
                    RazorLSPConstants.RazorCSharpLanguageServerName,
                    It.IsAny<CompletionParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, clientName, completionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentCompletionName, method);
                    Assert.Equal(RazorLSPConstants.RazorCSharpLanguageServerName, clientName);
                    languageServerCalled = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, null)));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
                Position = new Position(1, 7)
            };
            var previousCharacterProjection = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
                Position = new Position(100, 10),
                PositionIndex = 1000,
                Uri = virtualDocumentUri,
                HostDocumentVersion = 1,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(previousCharacterProjection));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var (succeeded, result) = await completionHandler.TryGetProvisionalCompletionsAsync(completionRequest, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot), projectionResult, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.False(succeeded);
            Assert.True(languageServerCalled);
            Assert.Equal(2, documentManager.UpdateVirtualDocumentCallCount);
            Assert.False(result.HasValue);
        }

        [Fact]
        public async Task TryGetProvisionalCompletionsAsync_TriggerCharacterNotDot_ReturnsFalse()
        {
            // Arrange
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = "D"
                },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var (succeeded, result) = await completionHandler.TryGetProvisionalCompletionsAsync(completionRequest, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot), projectionResult, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.False(succeeded);
            Assert.Null(result);
        }

        [Fact]
        public async Task TryGetProvisionalCompletionsAsync_PreviousCharacterHtml_ReturnsFalse()
        {
            // Arrange
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = "."
                },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
                Position = new Position(1, 7)
            };
            var previousCharacterProjection = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(previousCharacterProjection));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var (succeeded, result) = await completionHandler.TryGetProvisionalCompletionsAsync(completionRequest, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot), projectionResult, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.False(succeeded);
            Assert.Null(result);
        }

        [Fact]
        public async Task TryGetProvisionalCompletionsAsync_ProjectionAtStartOfLine_ReturnsFalse()
        {
            // Arrange
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = "."
                },
                Position = new Position(0, 1)
            };

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot));

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
                Position = new Position(1, 0)
            };
            var previousCharacterProjection = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(previousCharacterProjection));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var (succeeded, result) = await completionHandler.TryGetProvisionalCompletionsAsync(completionRequest, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot), projectionResult, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.False(succeeded);
            Assert.Null(result);
        }

        [Fact]
        public async Task TryGetProvisionalCompletionsAsync_NullHostDocumentVersion_ReturnsFalse()
        {
            // Arrange
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = "."
                },
                Position = new Position(0, 1)
            };

            var virtualDocumentUri = new Uri("C:/path/to/file.razor__virtual.cs");

            var documentManager = new TestDocumentManager();

            var languageServerCalled = false;
            var expectedItem = new CompletionItem() { InsertText = "DateTime" };
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(
                    TextBuffer,
                    It.IsAny<string>(),
                    RazorLSPConstants.CSharpContentTypeName,
                    It.IsAny<CompletionParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, clientName, completionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentCompletionName, method);
                    Assert.Equal(RazorLSPConstants.RazorCSharpLanguageServerName, clientName);
                    languageServerCalled = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, new[] { expectedItem })));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
                Position = new Position(1, 7)
            };
            var previousCharacterProjection = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
                Position = new Position(100, 10),
                PositionIndex = 1000,
                Uri = virtualDocumentUri,
                HostDocumentVersion = null,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(previousCharacterProjection));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var (succeeded, result) = await completionHandler.TryGetProvisionalCompletionsAsync(completionRequest, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot), projectionResult, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.False(succeeded);
            Assert.False(languageServerCalled);
            Assert.Equal(0, documentManager.UpdateVirtualDocumentCallCount);
            Assert.Null(result);
        }

        [Fact]
        public async Task TryGetProvisionalCompletionsAsync_AtCorrectProvisionalCompletionPoint_ReturnsExpectedResult()
        {
            // Arrange
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = "."
                },
                Position = new Position(0, 1)
            };

            var virtualDocumentUri = new Uri("C:/path/to/file.razor__virtual.cs");

            var documentManager = new TestDocumentManager();

            var languageServerCalled = false;
            var expectedItem = new CompletionItem() { InsertText = "DateTime" };
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(
                    TextBuffer,
                    It.IsAny<string>(),
                    RazorLSPConstants.RazorCSharpLanguageServerName,
                    It.IsAny<CompletionParams>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ITextBuffer, string, string, CompletionParams, CancellationToken>((textBuffer, method, clientName, completionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentCompletionName, method);
                    Assert.Equal(RazorLSPConstants.RazorCSharpLanguageServerName, clientName);
                    languageServerCalled = true;
                })
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, new[] { expectedItem })));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
                Position = new Position(1, 7)
            };
            var previousCharacterProjection = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
                Position = new Position(100, 10),
                PositionIndex = 1000,
                Uri = virtualDocumentUri,
                HostDocumentVersion = 1,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(previousCharacterProjection));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var (succeeded, result) = await completionHandler.TryGetProvisionalCompletionsAsync(completionRequest, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot), projectionResult, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(succeeded);
            Assert.True(languageServerCalled);
            Assert.Equal(2, documentManager.UpdateVirtualDocumentCallCount);
            Assert.NotNull(result);
            var item = Assert.Single((CompletionItem[])result.Value);
            Assert.Equal(expectedItem.InsertText, item.InsertText);
        }

        [Fact]
        public void TriggerAppliedToProjection_Razor_ReturnsFalse()
        {
            // Arrange
            _ = new CompletionHandler(JoinableTaskContext, Mock.Of<LSPRequestInvoker>(MockBehavior.Strict), Mock.Of<LSPDocumentManager>(MockBehavior.Strict), Mock.Of<LSPProjectionProvider>(MockBehavior.Strict), TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);
            var context = new CompletionContext();

            // Act
            var result = CompletionHandler.TriggerAppliesToProjection(context, RazorLanguageKind.Razor);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(" ", CompletionTriggerKind.TriggerCharacter, true)]
        [InlineData("<", CompletionTriggerKind.TriggerCharacter, true)]
        [InlineData("&", CompletionTriggerKind.TriggerCharacter, true)]
        [InlineData("\\", CompletionTriggerKind.TriggerCharacter, true)]
        [InlineData("/", CompletionTriggerKind.TriggerCharacter, true)]
        [InlineData("'", CompletionTriggerKind.TriggerCharacter, true)]
        [InlineData("=", CompletionTriggerKind.TriggerCharacter, true)]
        [InlineData(":", CompletionTriggerKind.TriggerCharacter, true)]
        [InlineData("\"", CompletionTriggerKind.TriggerCharacter, true)]
        [InlineData(".", CompletionTriggerKind.TriggerCharacter, true)]
        [InlineData(".", CompletionTriggerKind.Invoked, true)]
        [InlineData("@", CompletionTriggerKind.TriggerCharacter, true)]
        [InlineData("@", CompletionTriggerKind.Invoked, true)]
        [InlineData("a", CompletionTriggerKind.TriggerCharacter, true)] // Auto-invoked from VS platform
        [InlineData("a", CompletionTriggerKind.Invoked, true)]
        public void TriggerAppliedToProjection_Html_ReturnsExpectedResult(string character, CompletionTriggerKind kind, bool expected)
        {
            // Arrange
            _ = new CompletionHandler(JoinableTaskContext, Mock.Of<LSPRequestInvoker>(MockBehavior.Strict), Mock.Of<LSPDocumentManager>(MockBehavior.Strict), Mock.Of<LSPProjectionProvider>(MockBehavior.Strict), TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);
            var context = new CompletionContext()
            {
                TriggerCharacter = character,
                TriggerKind = kind
            };

            // Act
            var result = CompletionHandler.TriggerAppliesToProjection(context, RazorLanguageKind.Html);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(".", CompletionTriggerKind.TriggerCharacter, true)]
        [InlineData("@", CompletionTriggerKind.TriggerCharacter, true)]
        [InlineData(" ", CompletionTriggerKind.TriggerCharacter, true)]
        [InlineData("&", CompletionTriggerKind.TriggerCharacter, false)]
        [InlineData("a", CompletionTriggerKind.TriggerCharacter, true)] // Auto-invoked from VS platform
        [InlineData("a", CompletionTriggerKind.Invoked, true)]
        public void TriggerAppliedToProjection_CSharp_ReturnsExpectedResult(string character, CompletionTriggerKind kind, bool expected)
        {
            // Arrange
            _ = new CompletionHandler(JoinableTaskContext, Mock.Of<LSPRequestInvoker>(MockBehavior.Strict), Mock.Of<LSPDocumentManager>(MockBehavior.Strict), Mock.Of<LSPProjectionProvider>(MockBehavior.Strict), TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);
            var context = new CompletionContext()
            {
                TriggerCharacter = character,
                TriggerKind = kind
            };

            // Act
            var result = CompletionHandler.TriggerAppliesToProjection(context, RazorLanguageKind.CSharp);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TranslateTextEdits()
        {
            var razorDocPosition = new Position(line: 4, character: 9);
            var cSharpDocPosition = new Position(line: 99, character: 5);

            var documentSnapshot = new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, snapshotContent: @"@code
{
    void M()
    {
        M
    }
}");

            // Word 'M'
            var wordSnapshotSpan = new SnapshotSpan(documentSnapshot.Snapshot, new Span(39, 1));
            var wordRange = new TextExtent(wordSnapshotSpan, isSignificant: true);

            var completionList = new VSInternalCompletionList
            {
                Items = new CompletionItem[]
                {
                    new CompletionItem
                    {
                        TextEdit = new TextEdit
                        {
                            NewText = "M",
                            Range = new LanguageServer.Protocol.Range
                            {
                                Start = new Position
                                {
                                    Line = 99,
                                    Character = 4
                                },
                                End = new Position
                                {
                                    Line = 99,
                                    Character = 5
                                }
                            }
                        }
                    }
                }
            };

            var expectedRange = new LanguageServer.Protocol.Range
            {
                Start = new Position
                {
                    Line = 4,
                    Character = 8
                },
                End = new Position
                {
                    Line = 4,
                    Character = 9
                }
            };

            var result = CompletionHandler.TranslateTextEdits(razorDocPosition, cSharpDocPosition, wordRange, completionList);
            var actualRange = result.Items.First().TextEdit.Range;
            Assert.Equal(expectedRange, actualRange);
        }

        [Fact]
        public void GetBaseIndentation_Spaces()
        {
            // Arrange
            var snapshot = new StringTextSnapshot("    @i");
            var snapshotSpan = new SnapshotSpan(snapshot, new Span(5, 1));
            var wordExtent = new TextExtent(snapshotSpan, isSignificant: true);
            var formattingOptions = new FormattingOptions()
            {
                InsertSpaces = true,
                TabSize = 2,
            };

            // Assert
            var indentation = CompletionHandler.GetBaseIndentation(wordExtent, formattingOptions);

            // Act
            Assert.Equal(5, indentation);
        }

        [Fact]
        public void GetBaseIndentation_Tabs_ImplicitExpression()
        {
            // Arrange
            var snapshot = new StringTextSnapshot("\t\t@i");
            var snapshotSpan = new SnapshotSpan(snapshot, new Span(3, 1));
            var wordExtent = new TextExtent(snapshotSpan, isSignificant: true);
            var formattingOptions = new FormattingOptions()
            {
                InsertSpaces = false,
                TabSize = 3,
            };

            // Assert
            var indentation = CompletionHandler.GetBaseIndentation(wordExtent, formattingOptions);

            // Act
            Assert.Equal(7, indentation);
        }

        [Fact]
        public void GetBaseIndentation_Tabs_Text()
        {
            // Arrange
            var snapshot = new StringTextSnapshot("\t\ti");
            var snapshotSpan = new SnapshotSpan(snapshot, new Span(2, 1));
            var wordExtent = new TextExtent(snapshotSpan, isSignificant: true);
            var formattingOptions = new FormattingOptions()
            {
                InsertSpaces = false,
                TabSize = 3,
            };

            // Assert
            var indentation = CompletionHandler.GetBaseIndentation(wordExtent, formattingOptions);

            // Act
            Assert.Equal(6, indentation);
        }

        [Fact]
        public void GetBaseIndentation_Tabs_Mixed()
        {
            // Arrange
            var snapshot = new StringTextSnapshot("\t\t  i");
            var snapshotSpan = new SnapshotSpan(snapshot, new Span(4, 1));
            var wordExtent = new TextExtent(snapshotSpan, isSignificant: true);
            var formattingOptions = new FormattingOptions()
            {
                InsertSpaces = false,
                TabSize = 3,
            };

            // Assert
            var indentation = CompletionHandler.GetBaseIndentation(wordExtent, formattingOptions);

            // Act
            Assert.Equal(8, indentation);
        }

        private static ITextStructureNavigatorSelectorService BuildNavigatorSelector(TextExtent wordRange)
        {
            var navigator = new Mock<ITextStructureNavigator>(MockBehavior.Strict);
            navigator.Setup(n => n.GetExtentOfWord(It.IsAny<SnapshotPoint>()))
                .Returns(wordRange);
            var navigatorSelector = new Mock<ITextStructureNavigatorSelectorService>(MockBehavior.Strict);
            navigatorSelector.Setup(selector => selector.GetTextStructureNavigator(It.IsAny<ITextBuffer>()))
                .Returns(navigator.Object);
            return navigatorSelector.Object;
        }

        private static TextExtent GetWordExtent(string input)
        {
            var wordStart = input.IndexOf("|");
            var wordEnd = input.LastIndexOf("|");
            var wordLength = wordEnd - wordStart - 1;

            var actualInput = input.Remove(wordEnd, count: 1);
            actualInput = actualInput.Remove(wordStart, count: 1);

            var snapshot = new StringTextSnapshot(actualInput);
            var wordSpan = new Span(wordStart, wordLength);
            var snapshotSpan = new SnapshotSpan(snapshot, wordSpan);
            var isSignificant = !string.IsNullOrWhiteSpace(actualInput.Substring(wordSpan.Start, wordSpan.Length));
            var wordExtent = new TextExtent(snapshotSpan, isSignificant);
            return wordExtent;
        }

        private class TestFormattingOptionsProvider : FormattingOptionsProvider
        {
            public static readonly TestFormattingOptionsProvider Default = new(
                new FormattingOptions()
                {
                    InsertSpaces = true,
                    TabSize = 4,
                });
            private readonly FormattingOptions _options;

            public TestFormattingOptionsProvider(FormattingOptions options)
            {
                _options = options;
            }

            public override FormattingOptions GetOptions(LSPDocumentSnapshot documentSnapshot) => _options;
        }
    }
}
