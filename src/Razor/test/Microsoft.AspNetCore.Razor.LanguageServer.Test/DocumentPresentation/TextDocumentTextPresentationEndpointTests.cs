// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using OmniSharp.Extensions.JsonRpc;
using Xunit;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation
{
    public class TextDocumentTextPresentationEndpointTests : LanguageServerTestBase
    {
        [Fact]
        public async Task Handle_Html_MakesRequest()
        {
            // Arrange
            int? version = 1;
            var documentVersionCache = Mock.Of<DocumentVersionCache>(
                s => s.TryGetDocumentVersion(It.IsAny<DocumentSnapshot>(), out version) == true,
                MockBehavior.Strict);
            var codeDocument = TestRazorCodeDocument.Create("<div></div>");
            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>(), It.IsAny<bool>()) == RazorLanguageKind.Html, MockBehavior.Strict);

            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);

            var responseRouterReturns = new Mock<IResponseRouterReturns>(MockBehavior.Strict);
            responseRouterReturns
                .Setup(l => l.Returning<WorkspaceEdit?>(It.IsAny<CancellationToken>()))
                .ReturnsAsync((WorkspaceEdit?)null);

            var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
            languageServer
                .Setup(l => l.SendRequestAsync(LanguageServerConstants.RazorTextPresentationEndpoint, It.IsAny<IRazorPresentationParams>()))
                .ReturnsAsync(responseRouterReturns.Object);

            var endpoint = new TextDocumentTextPresentationEndpoint(
                Dispatcher,
                documentResolver,
                documentMappingService,
                languageServer.Object,
                documentVersionCache,
                TestLanguageServerFeatureOptions.Instance,
                LoggerFactory);

            var parameters = new TextPresentationParams()
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = uri
                },
                Range = new Range
                {
                    Start = new Position(0, 1),
                    End = new Position(0, 2)
                },
                Text = "Hi there"
            };

            // Act
            var result = await endpoint.Handle(parameters, CancellationToken.None);

            // Assert
            languageServer.Verify();
        }

        [Fact]
        public async Task Handle_CSharp_MakesRequest()
        {
            // Arrange
            int? version = 1;
            var documentVersionCache = Mock.Of<DocumentVersionCache>(
                s => s.TryGetDocumentVersion(It.IsAny<DocumentSnapshot>(), out version) == true,
                MockBehavior.Strict);
            var codeDocument = TestRazorCodeDocument.Create("@counter");
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var projectedRange = It.IsAny<Range>();
            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>(), It.IsAny<bool>()) == RazorLanguageKind.CSharp &&
                s.TryMapToProjectedDocumentRange(codeDocument, It.IsAny<Range>(), out projectedRange) == true, MockBehavior.Strict);

            var responseRouterReturns = new Mock<IResponseRouterReturns>(MockBehavior.Strict);
            responseRouterReturns
                .Setup(l => l.Returning<WorkspaceEdit?>(It.IsAny<CancellationToken>()))
                .ReturnsAsync((WorkspaceEdit?)null);

            var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
            languageServer
                .Setup(l => l.SendRequestAsync(LanguageServerConstants.RazorTextPresentationEndpoint, It.IsAny<IRazorPresentationParams>()))
                .ReturnsAsync(responseRouterReturns.Object);

            var endpoint = new TextDocumentTextPresentationEndpoint(
                Dispatcher,
                documentResolver,
                documentMappingService,
                languageServer.Object,
                documentVersionCache,
                TestLanguageServerFeatureOptions.Instance,
                LoggerFactory);

            var parameters = new TextPresentationParams()
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = uri
                },
                Range = new Range
                {
                    Start = new Position(0, 1),
                    End = new Position(0, 2)
                },
                Text = "Hi there"
            };

            // Act
            var result = await endpoint.Handle(parameters, CancellationToken.None);

            // Assert
            languageServer.Verify();
        }

        [Fact]
        public async Task Handle_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            int? version = 1;
            var documentVersionCache = Mock.Of<DocumentVersionCache>(
                s => s.TryGetDocumentVersion(It.IsAny<DocumentSnapshot>(), out version) == true,
                MockBehavior.Strict);
            var codeDocument = TestRazorCodeDocument.Create("<div></div>");
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>(), It.IsAny<bool>()) == RazorLanguageKind.Html, MockBehavior.Strict);

            var responseRouterReturns = new Mock<IResponseRouterReturns>(MockBehavior.Strict);
            responseRouterReturns
                .Setup(l => l.Returning<WorkspaceEdit?>(It.IsAny<CancellationToken>()))
                .ReturnsAsync((WorkspaceEdit?)null);

            var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
            languageServer
                .Setup(l => l.SendRequestAsync(LanguageServerConstants.RazorTextPresentationEndpoint, It.IsAny<IRazorPresentationParams>()))
                .ReturnsAsync(responseRouterReturns.Object);

            var endpoint = new TextDocumentTextPresentationEndpoint(
                Dispatcher,
                documentResolver,
                documentMappingService,
                languageServer.Object,
                documentVersionCache,
                TestLanguageServerFeatureOptions.Instance,
                LoggerFactory);

            var parameters = new TextPresentationParams()
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = uri
                },
                Range = new Range
                {
                    Start = new Position(0, 1),
                    End = new Position(0, 2)
                },
                Text = "Hi there"
            };

            // Act
            var result = await endpoint.Handle(parameters, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_UnsupportedCodeDocument_ReturnsNull()
        {
            // Arrange
            int? version = 1;
            var documentVersionCache = Mock.Of<DocumentVersionCache>(
                s => s.TryGetDocumentVersion(It.IsAny<DocumentSnapshot>(), out version) == true,
                MockBehavior.Strict);
            var codeDocument = TestRazorCodeDocument.Create("<div></div>");
            codeDocument.SetUnsupported();
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>(), It.IsAny<bool>()) == RazorLanguageKind.Html, MockBehavior.Strict);

            var responseRouterReturns = new Mock<IResponseRouterReturns>(MockBehavior.Strict);
            responseRouterReturns
                .Setup(l => l.Returning<WorkspaceEdit?>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WorkspaceEdit());

            var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
            languageServer
                .Setup(l => l.SendRequestAsync(LanguageServerConstants.RazorTextPresentationEndpoint, It.IsAny<IRazorPresentationParams>()))
                .ReturnsAsync(responseRouterReturns.Object);

            var endpoint = new TextDocumentTextPresentationEndpoint(
                Dispatcher,
                documentResolver,
                documentMappingService,
                languageServer.Object,
                documentVersionCache,
                TestLanguageServerFeatureOptions.Instance,
                LoggerFactory);

            var parameters = new TextPresentationParams()
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = uri
                },
                Range = new Range
                {
                    Start = new Position(0, 1),
                    End = new Position(0, 2)
                },
                Text = "Hi there"
            };

            // Act
            var result = await endpoint.Handle(parameters, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        private static DocumentResolver CreateDocumentResolver(string documentPath, RazorCodeDocument codeDocument)
        {
            var documentResolver = new Mock<DocumentResolver>(MockBehavior.Strict);
            var sourceTextChars = new char[codeDocument.Source.Length];
            codeDocument.Source.CopyTo(0, sourceTextChars, 0, codeDocument.Source.Length);
            var sourceText = SourceText.From(new string(sourceTextChars));
            var documentSnapshot = Mock.Of<DocumentSnapshot>(document =>
                document.GetGeneratedOutputAsync() == Task.FromResult(codeDocument) &&
                document.GetTextAsync() == Task.FromResult(sourceText), MockBehavior.Strict);
            documentResolver.Setup(resolver => resolver.TryResolveDocument(documentPath, out documentSnapshot))
                .Returns(true);

            DocumentSnapshot? nullDocumentSnapshot = null;
            documentResolver.Setup(resolver => resolver.TryResolveDocument(It.IsNotIn(documentPath), out nullDocumentSnapshot))
                .Returns(false);
            return documentResolver.Object;
        }
    }
}
