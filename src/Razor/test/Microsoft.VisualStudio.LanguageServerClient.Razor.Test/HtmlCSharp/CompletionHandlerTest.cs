// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class CompletionHandlerTest
    {
        public CompletionHandlerTest()
        {
            var joinableTaskContext = new JoinableTaskContextNode(new JoinableTaskContext());
            JoinableTaskContext = joinableTaskContext.Context;
            Uri = new Uri("C:/path/to/file.razor");
            LSPDocumentSynchronizer = Mock.Of<LSPDocumentSynchronizer>(s => s.TrySynchronizeVirtualDocumentAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<CancellationToken>()) == Task.FromResult(true));
            LanguageClientBroker = Mock.Of<ILanguageClientBroker>();
        }

        public JoinableTaskContext JoinableTaskContext { get; }

        private Uri Uri { get; }

        private LSPDocumentSynchronizer LSPDocumentSynchronizer { get; }

        private ILanguageClientBroker LanguageClientBroker { get; }

        [Fact]
        public async Task HandleRequestAsync_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var completionHandler = new TestCompletionHandler(JoinableTaskContext, LanguageClientBroker, documentManager, LSPDocumentSynchronizer, projectionResult: null);
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext() { TriggerKind = CompletionTriggerKind.Invoked },
                Position = new Position(0, 1)
            };

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_ProjectionNotFound_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>());
            var completionHandler = new TestCompletionHandler(JoinableTaskContext, LanguageClientBroker, documentManager, LSPDocumentSynchronizer, projectionResult: null);
            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext() { TriggerKind = CompletionTriggerKind.Invoked },
                Position = new Position(0, 1)
            };

            // Act
            var result = await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_HtmlProjection_InvokesHtmlLanguageServer()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>());
            var called = false;
            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };

            var completionHandler = new TestCompletionHandler(
                JoinableTaskContext,
                LanguageClientBroker,
                documentManager,
                LSPDocumentSynchronizer,
                projectionResult,
                callback: (method, serverKind) =>
                {
                    Assert.Equal(Methods.TextDocumentCompletionName, method);
                    Assert.Equal(LanguageServerKind.Html, serverKind);
                    called = true;
                });

            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext() { TriggerKind = CompletionTriggerKind.Invoked },
                Position = new Position(0, 1)
            };

            // Act
            await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None);

            // Assert
            Assert.True(called);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_InvokesCSharpLanguageServer()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>());
            var called = false;
            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };

            var completionHandler = new TestCompletionHandler(
                JoinableTaskContext,
                LanguageClientBroker,
                documentManager,
                LSPDocumentSynchronizer,
                projectionResult,
                callback: (method, serverKind) =>
                {
                    Assert.Equal(Methods.TextDocumentCompletionName, method);
                    Assert.Equal(LanguageServerKind.CSharp, serverKind);
                    called = true;
                });

            var completionRequest = new CompletionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Context = new CompletionContext() { TriggerKind = CompletionTriggerKind.Invoked },
                Position = new Position(0, 1)
            };

            // Act
            await completionHandler.HandleRequestAsync(completionRequest, new ClientCapabilities(), CancellationToken.None);

            // Assert
            Assert.True(called);
        }

        private class TestCompletionHandler : CompletionHandler
        {
            private readonly ProjectionResult _projectionResult;
            private readonly Action<string, LanguageServerKind> _callback;

            public TestCompletionHandler(
                JoinableTaskContext joinableTaskContext,
                ILanguageClientBroker languageClientBroker,
                LSPDocumentManager documentManager,
                LSPDocumentSynchronizer documentSynchronizer,
                ProjectionResult projectionResult,
                Action<string, LanguageServerKind> callback = null) : base(joinableTaskContext, languageClientBroker, documentManager, documentSynchronizer)
            {
                _projectionResult = projectionResult;
                _callback = callback;
            }

            protected override Task<TOut> RequestServerAsync<TIn, TOut>(ILanguageClientBroker languageClientBroker, string method, LanguageServerKind serverKind, TIn parameters, CancellationToken cancellationToken)
            {
                _callback?.Invoke(method, serverKind);

                return Task.FromResult<TOut>(default);
            }

            protected override Task<ProjectionResult> GetProjectionAsync(LSPDocumentSnapshot documentSnapshot, Position position, CancellationToken cancellationToken)
            {
                return Task.FromResult(_projectionResult);
            }
        }

        private class TestDocumentManager : LSPDocumentManager
        {
            private readonly Dictionary<Uri, LSPDocumentSnapshot> _documents = new Dictionary<Uri, LSPDocumentSnapshot>();

            public override event EventHandler<LSPDocumentChangeEventArgs> Changed;

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
