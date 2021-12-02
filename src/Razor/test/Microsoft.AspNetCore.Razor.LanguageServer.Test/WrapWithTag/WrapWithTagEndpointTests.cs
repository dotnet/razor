// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServerClient.Razor.WrapWithDiv;
using Moq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.WrapWithTag
{
    public class WrapWithTagEndpointTest : LanguageServerTestBase
    {
        [Fact]
        public async Task Handle_Html_ReturnsResult()
        {
            // Arrange
            var codeDocument = TestRazorCodeDocument.Create("<div></div>");
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var responseRouterReturns = new Mock<IResponseRouterReturns>(MockBehavior.Strict);
            responseRouterReturns
                .Setup(l => l.Returning<WrapWithTagResponse>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WrapWithTagResponse());

            var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
            languageServer
                .Setup(l => l.SendRequestAsync(LanguageServerConstants.RazorWrapWithTagEndpoint, It.IsAny<WrapWithTagParams>()))
                .ReturnsAsync(responseRouterReturns.Object);

            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>()) == RazorLanguageKind.Html, MockBehavior.Strict);
            var endpoint = new WrapWithTagEndpoint(
                languageServer.Object,
                Dispatcher,
                documentResolver,
                documentMappingService,
                LoggerFactory);

            var wrapWithDivParams = new WrapWithTagParams()
            {
                Range = new(0, 0, 0, 2),
                TextDocument = new TextDocumentIdentifier(uri)
            };

            // Act
            var result = await endpoint.Handle(wrapWithDivParams, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            languageServer.Verify();
        }

        [Fact]
        public async Task Handle_CSharp_ReturnsNull()
        {
            // Arrange
            var codeDocument = TestRazorCodeDocument.Create("@counter");
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var responseRouterReturns = new Mock<IResponseRouterReturns>(MockBehavior.Strict);
            responseRouterReturns
                .Setup(l => l.Returning<WrapWithTagResponse>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WrapWithTagResponse());

            var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
            languageServer
                .Setup(l => l.SendRequestAsync(LanguageServerConstants.RazorWrapWithTagEndpoint, It.IsAny<WrapWithTagParams>()))
                .ReturnsAsync(responseRouterReturns.Object);

            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>()) == RazorLanguageKind.CSharp, MockBehavior.Strict);
            var endpoint = new WrapWithTagEndpoint(
                languageServer.Object,
                Dispatcher,
                documentResolver,
                documentMappingService,
                LoggerFactory);

            var wrapWithDivParams = new WrapWithTagParams()
            {
                Range = new(0, 0, 0, 2),
                TextDocument = new TextDocumentIdentifier(uri)
            };

            // Act
            var result = await endpoint.Handle(wrapWithDivParams, CancellationToken.None);

            // Assert
            Assert.Null(result);
            languageServer.Verify();
        }

        [Fact]
        public async Task Handle_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            var codeDocument = TestRazorCodeDocument.Create("<div></div>");
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var responseRouterReturns = new Mock<IResponseRouterReturns>(MockBehavior.Strict);
            responseRouterReturns
                .Setup(l => l.Returning<WrapWithTagResponse>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WrapWithTagResponse());

            var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
            languageServer
                .Setup(l => l.SendRequestAsync(LanguageServerConstants.RazorWrapWithTagEndpoint, It.IsAny<WrapWithTagParams>()))
                .ReturnsAsync(responseRouterReturns.Object);

            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>()) == RazorLanguageKind.Html, MockBehavior.Strict);
            var endpoint = new WrapWithTagEndpoint(
                languageServer.Object,
                Dispatcher,
                documentResolver,
                documentMappingService,
                LoggerFactory);

            uri = new Uri("file://path/nottest.razor");
            var wrapWithDivParams = new WrapWithTagParams()
            {
                Range = new(0, 0, 0, 2),
                TextDocument = new TextDocumentIdentifier(uri)
            };

            // Act
            var result = await endpoint.Handle(wrapWithDivParams, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_UnsupportedCodeDocument_ReturnsNull()
        {
            // Arrange
            var codeDocument = TestRazorCodeDocument.Create("<div></div>");
            codeDocument.SetUnsupported();
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var responseRouterReturns = new Mock<IResponseRouterReturns>(MockBehavior.Strict);
            responseRouterReturns
                .Setup(l => l.Returning<WrapWithTagResponse>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WrapWithTagResponse());

            var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
            languageServer
                .Setup(l => l.SendRequestAsync(LanguageServerConstants.RazorWrapWithTagEndpoint, It.IsAny<WrapWithTagParams>()))
                .ReturnsAsync(responseRouterReturns.Object);

            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>()) == RazorLanguageKind.Html, MockBehavior.Strict);
            var endpoint = new WrapWithTagEndpoint(
                languageServer.Object,
                Dispatcher,
                documentResolver,
                documentMappingService,
                LoggerFactory);

            var wrapWithDivParams = new WrapWithTagParams()
            {
                Range = new(0, 0, 0, 2),
                TextDocument = new TextDocumentIdentifier(uri)
            };

            // Act
            var result = await endpoint.Handle(wrapWithDivParams, CancellationToken.None);

            // Assert
            Assert.Null(result);
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
            documentResolver.Setup(resolver => resolver.TryResolveDocument(documentPath, out documentSnapshot))
                .Returns(true);

            DocumentSnapshot nullDocumentSnapshot = null;
            documentResolver.Setup(resolver => resolver.TryResolveDocument(It.IsNotIn(documentPath), out nullDocumentSnapshot))
                .Returns(false);
            return documentResolver.Object;
        }
    }
}
