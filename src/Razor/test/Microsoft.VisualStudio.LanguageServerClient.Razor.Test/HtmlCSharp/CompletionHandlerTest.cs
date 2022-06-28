// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Test;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Threading;
using Moq;
using Xunit;
using CompletionContext = Microsoft.VisualStudio.LanguageServer.Protocol.CompletionContext;
using CompletionItem = Microsoft.VisualStudio.LanguageServer.Protocol.CompletionItem;
using CompletionList = Microsoft.VisualStudio.LanguageServer.Protocol.CompletionList;
using CompletionOptions = Microsoft.VisualStudio.LanguageServer.Protocol.CompletionOptions;
using CompletionTriggerKind = Microsoft.VisualStudio.LanguageServer.Protocol.CompletionTriggerKind;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [UseExportProvider]
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

        private ServerCapabilities CompletionServerCapabilities { get; } = new()
        {
            CompletionProvider = new CompletionOptions
            {
                ResolveProvider = true,
                AllCommitCharacters = CompletionRules.Default.DefaultCommitCharacters.Select(c => c.ToString()).ToArray(),
                TriggerCharacters = CompletionHandler.AllTriggerCharacters.ToArray(),
            }
        };

        private readonly string _languageClient = "languageClient";

        [Fact]
        public async Task HandleRequestAsync_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var requestInvoker = new TestLSPRequestInvoker();
            var projectionProvider = TestLSPProjectionProvider.Instance;
            var completionHandler = new CompletionHandler(
                JoinableTaskContext, requestInvoker, documentManager, projectionProvider, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);
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
            var requestInvoker = new TestLSPRequestInvoker();
            var projectionProvider = TestLSPProjectionProvider.Instance;
            var completionHandler = new CompletionHandler(
                JoinableTaskContext, requestInvoker, documentManager, projectionProvider, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);
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
            var text =
                """
                @using System
                @DateT
                """;

            var cursorPosition = new Position(1, 6);
            var documentUri = new Uri("C:/path/to/file.razor");
            var completionParams = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
                Context = new VSInternalCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.Invoked,
                    InvokeKind = VSInternalCompletionInvokeKind.Explicit
                },
                Position = cursorPosition
            };

            var expectedTextEditRange = new Range
            {
                Start = new Position { Line = 1, Character = 1 },
                End = new Position { Line = 1, Character = 6 }
            };

            // Act
            var result = await ExecuteCompletionRequestAsync(documentUri, text, completionParams).ConfigureAwait(false);

            // Assert
            var vsCompletionList = Assert.IsType<OptimizedVSCompletionList>(result.Value.Value);
            var item = vsCompletionList.Items.First();
            Assert.Equal("DateTime", item.Label);
            Assert.Equal(expectedTextEditRange, item.TextEdit.Range);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_DoNotReturnCSharpCompletionsInNonCSharpContextAsync()
        {
            // Arrange
            var text =
                """
                @using System

                """;

            var cursorPosition = new Position(1, 0);
            var documentUri = new Uri("C:/path/to/file.razor");
            var completionParams = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
                Context = new VSInternalCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.Invoked,
                    InvokeKind = VSInternalCompletionInvokeKind.Explicit
                },
                Position = cursorPosition
            };

            // Act
            var result = await ExecuteCompletionRequestAsync(documentUri, text, completionParams).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
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
            var text =
                """
                @using System
                @
                """;

            var cursorPosition = new Position(1, 1);
            var documentUri = new Uri("C:/path/to/file.razor");
            var completionParams = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
                Context = new VSInternalCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.Invoked,
                    InvokeKind = VSInternalCompletionInvokeKind.Explicit
                },
                Position = cursorPosition
            };

            // Act
            var result = await ExecuteCompletionRequestAsync(documentUri, text, completionParams).ConfigureAwait(false);

            // Assert
            var vsCompletionList = Assert.IsType<OptimizedVSCompletionList>(result.Value.Value);
            Assert.DoesNotContain(true, vsCompletionList.Items.Select(c => c.Preselect));
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_ReturnsCSharpSnippets()
        {
            // Arrange
            var text =
                """
                @
                """;

            var cursorPosition = new Position(0, 1);
            var documentUri = new Uri("C:/path/to/file.razor");
            var completionParams = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
                Context = new VSInternalCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = "@",
                    InvokeKind = VSInternalCompletionInvokeKind.Typing
                },
                Position = cursorPosition
            };

            // Act
            var result = await ExecuteCompletionRequestAsync(documentUri, text, completionParams).ConfigureAwait(false);

            // Assert
            var vsCompletionList = Assert.IsType<OptimizedVSCompletionList>(result.Value.Value);
            var itemLabels = vsCompletionList.Items.Select(c => c.Label);
            Assert.Contains("for (...)", itemLabels);
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
            var text =
                """
                @code
                {
                    ;
                }
                """;

            var cursorPosition = new Position(2, 5);
            var documentUri = new Uri("C:/path/to/file.razor");
            var completionParams = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
                Context = new VSInternalCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = ";",
                    InvokeKind = VSInternalCompletionInvokeKind.Typing
                },
                Position = cursorPosition
            };

            // Act
            var result = await ExecuteCompletionRequestAsync(documentUri, text, completionParams).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_IdentifierTriggerCharacter_InvokesCSharpLanguageServerNull()
        {
            // Arrange
            var text =
                """
                @using System;
                @Da
                """;

            var cursorPosition = new Position(1, 3);
            var documentUri = new Uri("C:/path/to/file.razor");
            var completionParams = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
                Context = new VSInternalCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = "a",
                    InvokeKind = VSInternalCompletionInvokeKind.Typing
                },
                Position = cursorPosition
            };

            // Act
            var result = await ExecuteCompletionRequestAsync(documentUri, text, completionParams).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_TransitionTriggerCharacter_InvokesCSharpLanguageServerWithInvoke()
        {
            // Arrange
            var text =
                """
                @using System;
                @Da
                """;

            var cursorPosition = new Position(1, 3);
            var documentUri = new Uri("C:/path/to/file.razor");
            var completionParams = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
                Context = new VSInternalCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.Invoked,
                    InvokeKind = VSInternalCompletionInvokeKind.Explicit
                },
                Position = cursorPosition
            };

            // Act
            var result = await ExecuteCompletionRequestAsync(documentUri, text, completionParams).ConfigureAwait(false);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_ReturnOnlyPropertiesIfInsideInitializer()
        {
            // Arrange
            var text =
                """
                @using System
                @{
                    var date = new DateTime()
                    {
                        
                    };
                }
                """;

            var cursorPosition = new Position(4, 8);
            var documentUri = new Uri("C:/path/to/file.razor");
            var completionParams = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
                Context = new VSInternalCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.Invoked,
                    InvokeKind = VSInternalCompletionInvokeKind.Explicit
                },
                Position = cursorPosition
            };

            // Act
            var result = await ExecuteCompletionRequestAsync(documentUri, text, completionParams).ConfigureAwait(false);

            // Assert
            var vsCompletionList = Assert.IsType<OptimizedVSCompletionList>(result.Value.Value);
            Assert.Equal(2, vsCompletionList.Items.Length);

            var itemLabels = vsCompletionList.Items.Select(c => c.Label);
            Assert.Contains("DayOfWeek", itemLabels);
            Assert.Contains("Kind", itemLabels);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_RemoveAllDesignTimeHelpers()
        {
            // Arrange
            var text =
                """
                @using System
                @code
                {
                    
                }
                """;

            var cursorPosition = new Position(3, 4);
            var documentUri = new Uri("C:/path/to/file.razor");
            var completionParams = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
                Context = new VSInternalCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.Invoked,
                    InvokeKind = VSInternalCompletionInvokeKind.Explicit
                },
                Position = cursorPosition
            };

            // Act
            var result = await ExecuteCompletionRequestAsync(documentUri, text, completionParams).ConfigureAwait(false);

            // Assert
            var vsCompletionList = Assert.IsType<OptimizedVSCompletionList>(result.Value.Value);

            var itemLabels = vsCompletionList.Items.Select(c => c.Label);
            Assert.DoesNotContain("BuildRenderTree", itemLabels);
            Assert.DoesNotContain("__o", itemLabels);
            Assert.DoesNotContain("__RazorDirectiveTokenHelpers__", itemLabels);
            Assert.DoesNotContain("_Imports", itemLabels);
            Assert.Contains("DateTime", itemLabels);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_OnlyRemoveCommonDesignTimeHelpers()
        {
            // Arrange
            var text =
                """
                @using System
                @{ void M() { var foo = 1; f } }
                """;

            var cursorPosition = new Position(1, 28);
            var documentUri = new Uri("C:/path/to/file.razor");
            var completionParams = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = documentUri },
                Context = new VSInternalCompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = "f",
                    InvokeKind = VSInternalCompletionInvokeKind.Typing
                },
                Position = cursorPosition
            };

            // Act
            var result = await ExecuteCompletionRequestAsync(documentUri, text, completionParams).ConfigureAwait(false);

            // Assert
            var vsCompletionList = Assert.IsType<OptimizedVSCompletionList>(result.Value.Value);

            var itemLabels = vsCompletionList.Items.Select(c => c.Label);
            Assert.DoesNotContain("BuildRenderTree", itemLabels);
            Assert.DoesNotContain("__o", itemLabels);
            Assert.DoesNotContain("__RazorDirectiveTokenHelpers__", itemLabels);
            Assert.DoesNotContain("_Imports", itemLabels);
            Assert.Contains("foo", itemLabels);
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
            var result = await ExecuteCompletionRequestAsync(documentUri, text, completionParams).ConfigureAwait(false);

            // Assert
            var vsCompletionList = Assert.IsType<OptimizedVSCompletionList>(result.Value.Value);

            var itemLabels = vsCompletionList.Items.Select(c => c.Label);
            Assert.Contains("Now", itemLabels);
        }

        [Fact]
        public async Task HandleRequestAsync_ProvisionalCompletion_CSharpTextEdits()
        {
            // Arrange
            var called = false;
            var expectedItem = new CompletionItem()
            {
                Label = "Now",
                InsertText = "Now",
                TextEdit = new TextEdit()
                {
                    Range = new Range()
                    {
                        Start = new Position(93, 15),
                        End = new Position(93, 15),
                    }
                }
            };
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext()
                {
                    TriggerKind = CompletionTriggerKind.TriggerCharacter,
                    TriggerCharacter = "."
                },
                Position = new Position(0, 10)
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
                Position = new Position(0, 10)
            };
            var virtualDocumentUri = new Uri("C:/path/to/file.razor__virtual.cs");
            var previousCharacterProjection = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
                Position = new Position(93, 14),
                PositionIndex = 1000,
                Uri = virtualDocumentUri,
                HostDocumentVersion = 1,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), new Position(0, 9), It.IsAny<CancellationToken>())).Returns(Task.FromResult(previousCharacterProjection));
            projectionProvider.Setup(p => p.GetProjectionForCompletionAsync(It.IsAny<LSPDocumentSnapshot>(), completionRequest.Position, It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var completionHandler = new CompletionHandler(JoinableTaskContext, requestInvoker.Object, documentManager, projectionProvider.Object, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            var item = Assert.Single(((CompletionList)result.Value).Items);
            Assert.Equal(expectedItem.InsertText, item.InsertText);
            Assert.Equal(expectedItem.TextEdit.Range.Start, completionRequest.Position);
            Assert.Equal(expectedItem.TextEdit.Range.End, completionRequest.Position);
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
            var provisionalCompletionResult = await completionHandler.TryGetProvisionalCompletionsAsync(completionRequest, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot), projectionResult, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.False(provisionalCompletionResult.Success);
            Assert.Null(provisionalCompletionResult.Result);
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
            var provisionalCompletionResult = await completionHandler.TryGetProvisionalCompletionsAsync(completionRequest, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot), projectionResult, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.False(provisionalCompletionResult.Success);
            Assert.True(languageServerCalled);
            Assert.Equal(2, documentManager.UpdateVirtualDocumentCallCount);
            Assert.False(provisionalCompletionResult.Result.HasValue);
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
            var provisionalCompletionResult = await completionHandler.TryGetProvisionalCompletionsAsync(completionRequest, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot), projectionResult, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.False(provisionalCompletionResult.Success);
            Assert.Null(provisionalCompletionResult.Result);
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
            var provisionalCompletionResult = await completionHandler.TryGetProvisionalCompletionsAsync(completionRequest, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot), projectionResult, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.False(provisionalCompletionResult.Success);
            Assert.Null(provisionalCompletionResult.Result);
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
            var provisionalCompletionResult = await completionHandler.TryGetProvisionalCompletionsAsync(completionRequest, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot), projectionResult, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.False(provisionalCompletionResult.Success);
            Assert.Null(provisionalCompletionResult.Result);
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
            var provisionalCompletionResult = await completionHandler.TryGetProvisionalCompletionsAsync(completionRequest, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot), projectionResult, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.False(provisionalCompletionResult.Success);
            Assert.False(languageServerCalled);
            Assert.Equal(0, documentManager.UpdateVirtualDocumentCallCount);
            Assert.Null(provisionalCompletionResult.Result);
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
            var provisionalCompletionResult = await completionHandler.TryGetProvisionalCompletionsAsync(completionRequest, new TestLSPDocumentSnapshot(new Uri("C:/path/file.razor"), 0, CSharpVirtualDocumentSnapshot), projectionResult, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(provisionalCompletionResult.Success);
            Assert.True(languageServerCalled);
            Assert.Equal(2, documentManager.UpdateVirtualDocumentCallCount);
            Assert.NotNull(provisionalCompletionResult.Result);
            var item = Assert.Single((CompletionItem[])provisionalCompletionResult.Result.Value);
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

            var result = CompletionHandler.TranslateTextEdits(razorDocPosition, cSharpDocPosition, wordRange.Span.AsRange(), completionList);
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

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_ItemDefault()
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

            var returnedCompletionList = new VSInternalCompletionList
            {
                Items = new CompletionItem[] { expectedItem },
                ItemDefaults = new CompletionListItemDefaults
                {
                    EditRange = new LanguageServer.Protocol.Range
                    {
                        Start = new Position(20, 30),
                        End = new Position(20, 30),
                    }
                }
            };

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
                .Returns(Task.FromResult(new ReinvocationResponse<SumType<CompletionItem[], CompletionList>?>(_languageClient, returnedCompletionList)));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
                Position = new Position(10, 30),
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
            Assert.Equal(new LanguageServer.Protocol.Range
            {
                Start = new Position(0, 1), End = new Position(0, 1)
            }, ((CompletionList)result.Value).ItemDefaults.EditRange);
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

        private async Task<SumType<CompletionItem[], CompletionList>?> ExecuteCompletionRequestAsync(Uri documentUri, string text, CompletionParams completionParams)
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
                csharpSourceText, csharpDocumentUri, CompletionServerCapabilities, razorSpanMappingService).ConfigureAwait(false);

            await csharpServer.OpenDocumentAsync(csharpDocumentUri, csharpSourceText.ToString()).ConfigureAwait(false);

            var requestInvoker = new TestLSPRequestInvoker(csharpServer);
            var documentManager = new TestDocumentManager(csharpServer);
            documentManager.AddDocument(documentUri, documentSnapshot);
            var projectionProvider = TestLSPProjectionProvider.Instance;

            var completionHandler = new CompletionHandler(
                JoinableTaskContext, requestInvoker, documentManager, projectionProvider, TextStructureNavigatorSelectorService, CompletionRequestContextCache, FormattingOptionsProvider, LoggerProvider);

            var result = await completionHandler.HandleRequestAsync(completionParams, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);
            return result;
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
