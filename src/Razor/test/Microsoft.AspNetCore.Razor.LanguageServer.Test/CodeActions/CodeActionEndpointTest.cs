// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Moq;
using Xunit;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using OmniSharp.Extensions.LanguageServer.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.CodeActions
{
    public class CodeActionEndpointTest : LanguageServerTestBase
    {
        private readonly DocumentResolver EmptyDocumentResolver = Mock.Of<DocumentResolver>();
        private readonly ILanguageServer LanguageServer = Mock.Of<ILanguageServer>();

        [Fact]
        public async Task Handle_NoDocument()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeActionEndpoint = new CodeActionEndpoint(Array.Empty<RazorCodeActionProvider>(), Dispatcher, EmptyDocumentResolver, LanguageServer);
            codeActionEndpoint.SupportsCodeActionResolve = false;
            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(new Position(0, 1), new Position(0, 1)),
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
            var codeActionEndpoint = new CodeActionEndpoint(Array.Empty<RazorCodeActionProvider>(), Dispatcher, documentResolver, LanguageServer);
            codeActionEndpoint.SupportsCodeActionResolve = false;
            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(new Position(0, 1), new Position(0, 1)),
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
            var codeActionEndpoint = new CodeActionEndpoint(Array.Empty<RazorCodeActionProvider>(), Dispatcher, documentResolver, LanguageServer);
            codeActionEndpoint.SupportsCodeActionResolve = false;
            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(new Position(0, 1), new Position(0, 1)),
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Empty(commandOrCodeActionContainer);
        }

        [Fact]
        public async Task Handle_OneProvider()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var codeActionEndpoint = new CodeActionEndpoint(new RazorCodeActionProvider[] {
                new MockCodeActionProvider()
            }, Dispatcher, documentResolver, LanguageServer);
            codeActionEndpoint.SupportsCodeActionResolve = false;

            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(new Position(0, 1), new Position(0, 1)),
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Single(commandOrCodeActionContainer);
        }


        [Fact]
        public async Task Handle_MultipleProviders()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var codeActionEndpoint = new CodeActionEndpoint(new RazorCodeActionProvider[] {
                new MockCodeActionProvider(),
                new MockCodeActionProvider(),
                new MockCodeActionProvider(),
            }, Dispatcher, documentResolver, LanguageServer);
            codeActionEndpoint.SupportsCodeActionResolve = false;

            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(new Position(0, 1), new Position(0, 1)),
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Equal(3, commandOrCodeActionContainer.Count());
        }

        [Fact]
        public async Task Handle_OneNullReturningProvider()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var codeActionEndpoint = new CodeActionEndpoint(new RazorCodeActionProvider[] {
                new NullMockCodeActionProvider()
            }, Dispatcher, documentResolver, LanguageServer);
            codeActionEndpoint.SupportsCodeActionResolve = false;

            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(new Position(0, 1), new Position(0, 1)),
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Empty(commandOrCodeActionContainer);
        }

        [Fact]
        public async Task Handle_MultipleMixedProvider()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var codeActionEndpoint = new CodeActionEndpoint(new RazorCodeActionProvider[] {
                new MockCodeActionProvider(),
                new NullMockCodeActionProvider(),
                new MockCodeActionProvider(),
                new NullMockCodeActionProvider(),
            }, Dispatcher, documentResolver, LanguageServer);
            codeActionEndpoint.SupportsCodeActionResolve = false;

            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(new Position(0, 1), new Position(0, 1)),
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Equal(2, commandOrCodeActionContainer.Count());
        }

        [Fact]
        public async Task Handle_MultipleMixedProvider_SupportsCodeActionResolveTrue()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var codeActionEndpoint = new CodeActionEndpoint(new RazorCodeActionProvider[] {
                new MockCodeActionProvider(),
                new NullMockCodeActionProvider(),
                new MockCodeActionProvider(),
                new NullMockCodeActionProvider(),
            }, Dispatcher, documentResolver, LanguageServer);
            codeActionEndpoint.SupportsCodeActionResolve = true;

            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(new Position(0, 1), new Position(0, 1)),
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Collection(commandOrCodeActionContainer,
                c => Assert.True(c.CodeAction is RazorCodeAction),
                c => Assert.True(c.CodeAction is RazorCodeAction));
        }

        [Fact]
        public async Task Handle_MultipleMixedProvider_SupportsCodeActionResolveFalse()
        {
            // Arrange
            var documentPath = "C:/path/to/Page.razor";
            var codeDocument = CreateCodeDocument("@code {}");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var codeActionEndpoint = new CodeActionEndpoint(new RazorCodeActionProvider[] {
                new MockCodeActionProvider(),
                new NullMockCodeActionProvider(),
                new MockCodeActionProvider(),
                new NullMockCodeActionProvider(),
            }, Dispatcher, documentResolver, LanguageServer);
            codeActionEndpoint.SupportsCodeActionResolve = false;

            var request = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier(new Uri(documentPath)),
                Range = new Range(new Position(0, 1), new Position(0, 1)),
            };

            // Act
            var commandOrCodeActionContainer = await codeActionEndpoint.Handle(request, default);

            // Assert
            Assert.Collection(commandOrCodeActionContainer,
                c => Assert.False(c.CodeAction is RazorCodeAction),
                c => Assert.False(c.CodeAction is RazorCodeAction));
        }

        private class MockCodeActionProvider : RazorCodeActionProvider
        {
            public override Task<RazorCodeAction[]> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
            {
                return Task.FromResult(new[] { new RazorCodeAction() });
            }
        }

        private class NullMockCodeActionProvider : RazorCodeActionProvider
        {
            public override Task<RazorCodeAction[]> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
            {
                return null;
            }
        }

        private static DocumentResolver CreateDocumentResolver(string documentPath, RazorCodeDocument codeDocument)
        {
            var sourceTextChars = new char[codeDocument.Source.Length];
            codeDocument.Source.CopyTo(0, sourceTextChars, 0, codeDocument.Source.Length);
            var sourceText = SourceText.From(new string(sourceTextChars));
            var documentSnapshot = Mock.Of<DocumentSnapshot>(document =>
                document.GetGeneratedOutputAsync() == Task.FromResult(codeDocument) &&
                document.GetTextAsync() == Task.FromResult(sourceText));
            var documentResolver = new Mock<DocumentResolver>();
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
    }
}
