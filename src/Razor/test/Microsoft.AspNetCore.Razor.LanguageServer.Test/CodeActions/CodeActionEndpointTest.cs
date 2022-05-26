// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Moq;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using Xunit;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    public class CodeActionEndpointTest : LanguageServerTestBase
    {
        private readonly RazorDocumentMappingService _documentMappingService = Mock.Of<RazorDocumentMappingService>(s => s.TryMapToProjectedDocumentVSRange(It.IsAny<RazorCodeDocument>(), It.IsAny<Range>(), out It.Ref<Range>.IsAny) == false, MockBehavior.Strict);
        private readonly DocumentResolver _emptyDocumentResolver = Mock.Of<DocumentResolver>(r => r.TryResolveDocument(It.IsAny<string>(), out It.Ref<DocumentSnapshot>.IsAny) == false, MockBehavior.Strict);
        private readonly LanguageServerFeatureOptions _languageServerFeatureOptions = Mock.Of<LanguageServerFeatureOptions>(l => l.SupportsFileManipulation == true, MockBehavior.Strict);
        private readonly ClientNotifierServiceBase _languageServer = Mock.Of<ClientNotifierServiceBase>(MockBehavior.Strict);

        [Fact]
        public async Task Handle_NoDocument()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeActionEndpoint = new CodeActionEndpoint(
                _documentMappingService,
                Array.Empty<RazorCodeActionProvider>(),
                Array.Empty<CSharpCodeActionProvider>(),
                Dispatcher,
                _emptyDocumentResolver,
                _languageServer,
                _languageServerFeatureOptions)
            {
                _supportsCodeActionResolve = false
            };
            var request = new CodeActionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(documentPath) },
                Range = new Range { Start = new Position(0, 1), End = new Position(0, 1) },
                Context = new CodeActionContext()
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Null(commandOrCodeActionContainer);
        }

        [Fact]
        public async Task Handle_UnsupportedDocument()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            codeDocument.SetUnsupported();
            var codeActionEndpoint = new CodeActionEndpoint(
                _documentMappingService,
                Array.Empty<RazorCodeActionProvider>(),
                Array.Empty<CSharpCodeActionProvider>(),
                Dispatcher,
                documentResolver,
                _languageServer,
                _languageServerFeatureOptions)
            {
                _supportsCodeActionResolve = false
            };
            var request = new CodeActionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(documentPath) },
                Range = new Range { Start = new Position(0, 1), End = new Position(0, 1) },
                Context = new OmniSharpVSCodeActionContext()
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Null(commandOrCodeActionContainer);
        }

        [Fact]
        public async Task Handle_NoProviders()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var codeActionEndpoint = new CodeActionEndpoint(
                _documentMappingService,
                Array.Empty<RazorCodeActionProvider>(),
                Array.Empty<CSharpCodeActionProvider>(),
                Dispatcher,
                documentResolver,
                _languageServer,
                _languageServerFeatureOptions)
            {
                _supportsCodeActionResolve = false
            };
            var request = new CodeActionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(documentPath) },
                Range = new Range { Start = new Position(0, 1), End = new Position(0, 1) },
                Context = new OmniSharpVSCodeActionContext()
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Null(commandOrCodeActionContainer);
        }

        [Fact]
        public async Task Handle_OneRazorCodeActionProvider()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var codeActionEndpoint = new CodeActionEndpoint(
                _documentMappingService,
                new RazorCodeActionProvider[] {
                    new MockRazorCodeActionProvider()
                },
                Array.Empty<CSharpCodeActionProvider>(),
                Dispatcher,
                documentResolver,
                _languageServer,
                _languageServerFeatureOptions)
            {
                _supportsCodeActionResolve = false
            };

            var request = new CodeActionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(documentPath) },
                Range = new Range { Start = new Position(0, 1), End = new Position(0, 1) },
                Context = new CodeActionContext()
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Single(commandOrCodeActionContainer);
        }

        [Fact]
        public async Task Handle_OneCSharpCodeActionProvider()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var documentMappingService = CreateDocumentMappingService();
            var languageServer = CreateLanguageServer();
            var codeActionEndpoint = new CodeActionEndpoint(
                documentMappingService,
                Array.Empty<RazorCodeActionProvider>(),
                new CSharpCodeActionProvider[] {
                    new MockCSharpCodeActionProvider()
                },
                Dispatcher,
                documentResolver,
                languageServer,
                _languageServerFeatureOptions)
            {
                _supportsCodeActionResolve = false
            };

            var request = new CodeActionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(documentPath) },
                Range = new Range { Start = new Position(0, 1), End = new Position(0, 1) },
                Context = new OmniSharpVSCodeActionContext()
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Single(commandOrCodeActionContainer);
        }

        [Fact]
        public async Task Handle_OneCodeActionProviderWithMultipleCodeActions()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var codeActionEndpoint = new CodeActionEndpoint(
                _documentMappingService,
                new RazorCodeActionProvider[] {
                    new MockMultipleRazorCodeActionProvider(),
                },
                Array.Empty<CSharpCodeActionProvider>(),
                Dispatcher,
                documentResolver,
                _languageServer,
                _languageServerFeatureOptions)
            {
                _supportsCodeActionResolve = false
            };

            var request = new CodeActionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(documentPath) },
                Range = new Range { Start = new Position(0, 1), End = new Position(0, 1) },
                Context = new OmniSharpVSCodeActionContext()
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Equal(2, commandOrCodeActionContainer.Count());
        }

        [Fact]
        public async Task Handle_MultipleCodeActionProvidersWithMultipleCodeActions()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var documentMappingService = CreateDocumentMappingService();
            var languageServer = CreateLanguageServer();
            var codeActionEndpoint = new CodeActionEndpoint(
                documentMappingService,
                new RazorCodeActionProvider[] {
                    new MockMultipleRazorCodeActionProvider(),
                    new MockMultipleRazorCodeActionProvider(),
                    new MockRazorCodeActionProvider(),
                },
                new CSharpCodeActionProvider[] {
                    new MockCSharpCodeActionProvider(),
                    new MockCSharpCodeActionProvider()
                },
                Dispatcher,
                documentResolver,
                languageServer,
                _languageServerFeatureOptions)
            {
                _supportsCodeActionResolve = false
            };

            var request = new CodeActionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(documentPath) },
                Range = new Range { Start = new Position(0, 1), End = new Position(0, 1) },
                Context = new OmniSharpVSCodeActionContext()
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Equal(7, commandOrCodeActionContainer.Count());
        }

        [Fact]
        public async Task Handle_MultipleProviders()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var documentMappingService = CreateDocumentMappingService();
            var languageServer = CreateLanguageServer();
            var codeActionEndpoint = new CodeActionEndpoint(
                documentMappingService,
                new RazorCodeActionProvider[] {
                    new MockRazorCodeActionProvider(),
                    new MockRazorCodeActionProvider(),
                    new MockRazorCodeActionProvider(),
                },
                new CSharpCodeActionProvider[] {
                    new MockCSharpCodeActionProvider(),
                    new MockCSharpCodeActionProvider()
                },
                Dispatcher,
                documentResolver,
                languageServer,
                _languageServerFeatureOptions)
            {
                _supportsCodeActionResolve = false
            };

            var request = new CodeActionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(documentPath) },
                Range = new Range { Start = new Position(0, 1), End = new Position(0, 1) },
                Context = new OmniSharpVSCodeActionContext()
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Equal(5, commandOrCodeActionContainer.Count());
        }

        [Fact]
        public async Task Handle_OneNullReturningProvider()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var codeActionEndpoint = new CodeActionEndpoint(
                _documentMappingService,
                new RazorCodeActionProvider[] {
                    new MockNullRazorCodeActionProvider()
                },
                Array.Empty<CSharpCodeActionProvider>(),
                Dispatcher,
                documentResolver,
                _languageServer,
                _languageServerFeatureOptions)
            {
                _supportsCodeActionResolve = false
            };

            var request = new CodeActionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(documentPath) },
                Range = new Range { Start = new Position(0, 1), End = new Position(0, 1) },
                Context = new OmniSharpVSCodeActionContext()
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Null(commandOrCodeActionContainer);
        }

        [Fact]
        public async Task Handle_MultipleMixedProvider()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var documentMappingService = CreateDocumentMappingService();
            var languageServer = CreateLanguageServer();
            var codeActionEndpoint = new CodeActionEndpoint(
                documentMappingService,
                new RazorCodeActionProvider[] {
                    new MockRazorCodeActionProvider(),
                    new MockNullRazorCodeActionProvider(),
                    new MockRazorCodeActionProvider(),
                    new MockNullRazorCodeActionProvider(),
                },
                new CSharpCodeActionProvider[] {
                    new MockCSharpCodeActionProvider(),
                    new MockCSharpCodeActionProvider()
                },
                Dispatcher,
                documentResolver,
                languageServer,
                _languageServerFeatureOptions)
            {
                _supportsCodeActionResolve = false
            };

            var request = new CodeActionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(documentPath) },
                Range = new Range { Start = new Position(0, 1), End = new Position(0, 1) },
                Context = new OmniSharpVSCodeActionContext()
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Equal(4, commandOrCodeActionContainer.Count());
        }

        [Fact]
        public async Task Handle_MultipleMixedProvider_SupportsCodeActionResolveTrue()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var codeActionEndpoint = new CodeActionEndpoint(
                _documentMappingService,
                new RazorCodeActionProvider[] {
                    new MockRazorCodeActionProvider(),
                    new MockRazorCommandProvider(),
                    new MockNullRazorCodeActionProvider()
                },
                Array.Empty<CSharpCodeActionProvider>(),
                Dispatcher,
                documentResolver,
                _languageServer,
                _languageServerFeatureOptions)
            {
                _supportsCodeActionResolve = true
            };

            var request = new CodeActionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(documentPath) },
                Range = new Range { Start = new Position(0, 1), End = new Position(0, 1) },
                Context = new OmniSharpVSCodeActionContext()
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Collection(commandOrCodeActionContainer,
                c =>
                {
                    Assert.True(c.TryGetSecond(out var codeAction));
                    Assert.True(codeAction is RazorCodeAction);
                },
                c =>
                {
                    Assert.True(c.TryGetSecond(out var codeAction));
                    Assert.True(codeAction is RazorCodeAction);
                });
        }

        [Fact]
        public async Task Handle_MultipleMixedProvider_SupportsCodeActionResolveFalse()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var codeActionEndpoint = new CodeActionEndpoint(
                _documentMappingService,
                new RazorCodeActionProvider[] {
                    new MockRazorCodeActionProvider(),
                    new MockRazorCommandProvider(),
                    new MockNullRazorCodeActionProvider()
                },
                Array.Empty<CSharpCodeActionProvider>(),
                Dispatcher,
                documentResolver,
                _languageServer,
                _languageServerFeatureOptions)
            {
                _supportsCodeActionResolve = false
            };

            var request = new CodeActionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(documentPath) },
                Range = new Range { Start = new Position(0, 1), End = new Position(0, 1) },
                Context = new OmniSharpVSCodeActionContext()
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Collection(commandOrCodeActionContainer,
                c =>
                {
                    Assert.True(c.TryGetFirst(out var command1));
                    var command = Assert.IsType<Command>(command1);
                    var codeActionParams = command.Arguments.First() as RazorCodeActionResolutionParams;
                    Assert.Equal(LanguageServerConstants.CodeActions.EditBasedCodeActionCommand, codeActionParams.Action);
                },
                c => Assert.True(c.TryGetFirst(out var _)));
        }

        [Fact]
        public async Task GenerateRazorCodeActionContextAsync_WithSelectionRange()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var codeActionEndpoint = new CodeActionEndpoint(
                _documentMappingService,
                new RazorCodeActionProvider[] {
                    new MockRazorCodeActionProvider()
                },
                Array.Empty<CSharpCodeActionProvider>(),
                Dispatcher,
                documentResolver,
                _languageServer,
                _languageServerFeatureOptions)
            {
                _supportsCodeActionResolve = false
            };

            var initialRange = new Range { Start = new Position(0, 1), End = new Position(0, 1) };
            var selectionRange = new Range { Start = new Position(0, 5), End = new Position(0, 5) };
            var request = new CodeActionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(documentPath) },
                Range = initialRange,
                Context = new OmniSharpVSCodeActionContext()
                {
                    SelectionRange = selectionRange,
                }
            };

            // Act
            var razorCodeActionContext = await codeActionEndpoint.GenerateRazorCodeActionContextAsync(request, default);

            // Assert
            Assert.NotNull(razorCodeActionContext);
            Assert.Equal(selectionRange, razorCodeActionContext.Request.Range);
        }

        [Fact]
        public async Task GenerateRazorCodeActionContextAsync_WithoutSelectionRange()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var codeActionEndpoint = new CodeActionEndpoint(
                _documentMappingService,
                new RazorCodeActionProvider[] {
                    new MockRazorCodeActionProvider()
                },
                Array.Empty<CSharpCodeActionProvider>(),
                Dispatcher,
                documentResolver,
                _languageServer,
                _languageServerFeatureOptions)
            {
                _supportsCodeActionResolve = false
            };

            var initialRange = new Range { Start = new Position(0, 1), End = new Position(0, 1) };
            var request = new CodeActionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(documentPath) },
                Range = initialRange,
                Context = new OmniSharpVSCodeActionContext()
                {
                    SelectionRange = null
                }
            };

            // Act
            var razorCodeActionContext = await codeActionEndpoint.GenerateRazorCodeActionContextAsync(request, default);

            // Assert
            Assert.NotNull(razorCodeActionContext);
            Assert.Equal(initialRange, razorCodeActionContext.Request.Range);
        }

        [Fact]
        public async Task GetCSharpCodeActionsFromLanguageServerAsync_InvalidRangeMapping()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            Range projectedRange = null;
            var documentMappingService = Mock.Of<DefaultRazorDocumentMappingService>(
                d => d.TryMapToProjectedDocumentVSRange(It.IsAny<RazorCodeDocument>(), It.IsAny<Range>(), out projectedRange) == false
            , MockBehavior.Strict);
            var codeActionEndpoint = new CodeActionEndpoint(
                documentMappingService,
                Array.Empty<RazorCodeActionProvider>(),
                new CSharpCodeActionProvider[] {
                    new MockCSharpCodeActionProvider()
                },
                Dispatcher,
                documentResolver,
                _languageServer,
                _languageServerFeatureOptions)
            {
                _supportsCodeActionResolve = false
            };

            var initialRange = new Range { Start = new Position(0, 1), End = new Position(0, 1) };
            var request = new CodeActionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(documentPath) },
                Range = initialRange,
                Context = new OmniSharpVSCodeActionContext()
            };

            var context = await codeActionEndpoint.GenerateRazorCodeActionContextAsync(request, default);

            // Act
            var results = await codeActionEndpoint.GetCSharpCodeActionsFromLanguageServerAsync(context, default);

            // Assert
            Assert.Empty(results);
            Assert.Equal(initialRange, context.Request.Range);
        }

        [Fact]
        public async Task GetCSharpCodeActionsFromLanguageServerAsync_ReturnsCodeActions()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var projectedRange = new Range { Start = new Position(15, 2), End = new Position(15, 2) };
            var documentMappingService = CreateDocumentMappingService(projectedRange);
            var languageServer = CreateLanguageServer();
            var codeActionEndpoint = new CodeActionEndpoint(
                documentMappingService,
                Array.Empty<RazorCodeActionProvider>(),
                new CSharpCodeActionProvider[] {
                    new MockCSharpCodeActionProvider()
                },
                Dispatcher,
                documentResolver,
                languageServer,
                _languageServerFeatureOptions)
            {
                _supportsCodeActionResolve = false
            };

            var initialRange = new Range{ Start = new Position(0, 1), End = new Position(0, 1) };
            var request = new CodeActionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = new Uri(documentPath) },
                Range = initialRange,
                Context = new OmniSharpVSCodeActionContext()
                {
                    SelectionRange = initialRange
                }
            };

            var context = await codeActionEndpoint.GenerateRazorCodeActionContextAsync(request, default);

            // Act
            var results = await codeActionEndpoint.GetCSharpCodeActionsFromLanguageServerAsync(context, default);

            // Assert
            Assert.Single(results);
            var diagnostics = results.Single().Diagnostics.ToArray();
            Assert.Equal(2, diagnostics.Length);

            // Diagnostic ranges contain the projected range for
            // 1. Range
            // 2. SelectionRange
            //
            // This helps verify that the CodeActionEndpoint is mapping the
            // ranges correctly using the mapping service
            Assert.Equal(projectedRange, diagnostics[0].Range);
            Assert.Equal(projectedRange, diagnostics[1].Range);
        }

        private static DefaultRazorDocumentMappingService CreateDocumentMappingService(Range projectedRange = null)
        {
            projectedRange ??= new Range { Start = new Position(5, 2), End = new Position(5, 2) };
            var documentMappingService = Mock.Of<DefaultRazorDocumentMappingService>(
                d => d.TryMapToProjectedDocumentVSRange(It.IsAny<RazorCodeDocument>(), It.IsAny<Range>(), out projectedRange) == true
            , MockBehavior.Strict);
            return documentMappingService;
        }

        private static ClientNotifierServiceBase CreateLanguageServer()
        {
            return new TestLanguageServer();
        }

        private static DocumentResolver CreateDocumentResolver(string documentPath, RazorCodeDocument codeDocument)
        {
            var sourceTextChars = new char[codeDocument.Source.Length];
            codeDocument.Source.CopyTo(0, sourceTextChars, 0, codeDocument.Source.Length);
            var sourceText = SourceText.From(new string(sourceTextChars));
            var documentSnapshot = Mock.Of<DocumentSnapshot>(document =>
                document.GetGeneratedOutputAsync() == Task.FromResult(codeDocument) &&
                document.GetTextAsync() == Task.FromResult(sourceText), MockBehavior.Strict);
            var documentResolver = new Mock<DocumentResolver>(MockBehavior.Strict);
            documentResolver
                .Setup(resolver => resolver.TryResolveDocument(documentPath, out documentSnapshot))
                .Returns(true);
            return documentResolver.Object;
        }

        private static RazorCodeDocument CreateCodeDocument(string text)
        {
            var codeDocument = TestRazorCodeDocument.CreateEmpty();
            var sourceDocument = TestRazorSourceDocument.Create(text);
            var syntaxTree = RazorSyntaxTree.Parse(sourceDocument);
            codeDocument.SetSyntaxTree(syntaxTree);
            return codeDocument;
        }

        private class MockRazorCodeActionProvider : RazorCodeActionProvider
        {
            public override Task<IReadOnlyList<RazorCodeAction>> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
            {
                return Task.FromResult(new List<RazorCodeAction>() { new RazorCodeAction() } as IReadOnlyList<RazorCodeAction>);
            }
        }

        private class MockMultipleRazorCodeActionProvider : RazorCodeActionProvider
        {
            public override Task<IReadOnlyList<RazorCodeAction>> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
            {
                return Task.FromResult(new List<RazorCodeAction>()
                {
                    new RazorCodeAction(),
                    new RazorCodeAction()
                } as IReadOnlyList<RazorCodeAction>);
            }
        }

        private class MockCSharpCodeActionProvider : CSharpCodeActionProvider
        {
            public override Task<IReadOnlyList<RazorCodeAction>> ProvideAsync(RazorCodeActionContext context, IEnumerable<RazorCodeAction> codeActions, CancellationToken cancellationToken)
            {
                return Task.FromResult(new List<RazorCodeAction>()
                {
                    new RazorCodeAction()
                } as IReadOnlyList<RazorCodeAction>);
            }
        }

        private class MockRazorCommandProvider : RazorCodeActionProvider
        {
            public override Task<IReadOnlyList<RazorCodeAction>> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
            {
                // O# Code Actions don't have `Data`, but `Commands` do
                return Task.FromResult(new List<RazorCodeAction>() {
                    new RazorCodeAction() {
                        Title = "SomeTitle",
                        Data = JToken.FromObject(new AddUsingsCodeActionParams())
                    }
                } as IReadOnlyList<RazorCodeAction>);
            }
        }

        private class MockNullRazorCodeActionProvider : RazorCodeActionProvider
        {
            public override Task<IReadOnlyList<RazorCodeAction>> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<RazorCodeAction>>(null);
            }
        }

        private class TestLanguageServer : ClientNotifierServiceBase
        {
            public override OmniSharp.Extensions.LanguageServer.Protocol.Models.InitializeParams ClientSettings => throw new NotImplementedException();

            public override Task OnStarted(ILanguageServer server, CancellationToken cancellationToken) => Task.CompletedTask;

            public override Task<IResponseRouterReturns> SendRequestAsync(string method)
            {
                if (method != LanguageServerConstants.RazorProvideCodeActionsEndpoint)
                {
                    throw new InvalidOperationException($"Unexpected method {method}");
                }

                return Task.FromResult<IResponseRouterReturns>(new TestResponseRouterReturns(null));
            }

            public override Task<IResponseRouterReturns> SendRequestAsync<T>(string method, T @params)
            {
                if (method != LanguageServerConstants.RazorProvideCodeActionsEndpoint)
                {
                    throw new InvalidOperationException($"Unexpected method {method}");
                }

                if (@params is not CodeActionParams codeActionParams || codeActionParams.Context is not OmniSharpVSCodeActionContext codeActionContext)
                {
                    throw new InvalidOperationException(@params.GetType().FullName);
                }

                // Create a code action specifically with diagnostics that
                // contain the contextual information for it's creation. This is
                // a hacky way to verify that data transmitted to the language server
                // is correct rather than providing specific test hooks in the CodeActionEndpoint
                var result = new[]
                {
                    new RazorCodeAction()
                    {
                        Data = JToken.FromObject(new { CustomTags = new object[] { "CodeActionName" } }),
                        Diagnostics = new[]
                        {
                            new Diagnostic()
                            {
                                Range = codeActionParams.Range,
                                Message = "Range"
                            },
                            new Diagnostic()
                            {
                                Range = codeActionContext.SelectionRange,
                                Message = "Selection Range"
                            }
                        }
                    }
                };

                return Task.FromResult<IResponseRouterReturns>(new TestResponseRouterReturns(result));
            }

            private class TestResponseRouterReturns : IResponseRouterReturns
            {
                private readonly object _result;

                public TestResponseRouterReturns(object result)
                {
                    _result = result;
                }

                public Task<Response> Returning<Response>(CancellationToken cancellationToken)
                {
                    return Task.FromResult((Response)_result);
                }

                public Task ReturningVoid(CancellationToken cancellationToken)
                {
                    return Task.CompletedTask;
                }
            }
        }
    }
}
