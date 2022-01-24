// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Debugging;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Debugging
{
    public class RazorBreakpointSpanEndpointTest : LanguageServerTestBase
    {
        public RazorBreakpointSpanEndpointTest()
        {
            var documentVersionCache = new Mock<DocumentVersionCache>(MockBehavior.Strict);
            int? version = 1337;
            documentVersionCache.Setup(cache => cache.TryGetDocumentVersion(It.IsAny<DocumentSnapshot>(), out version))
                .Returns(true);

            DocumentVersionCache = documentVersionCache.Object;
            MappingService = new DefaultRazorDocumentMappingService(LoggerFactory);
        }

        private DocumentVersionCache DocumentVersionCache { get; }

        private RazorDocumentMappingService MappingService { get; }

        [Fact]
        public void GetMappingBehavior_CSHTML()
        {
            // Arrange
            var documentPath = "/path/to/document.cshtml";

            // Act
            var result = RazorBreakpointSpanEndpoint.GetMappingBehavior(documentPath);

            // Assert
            Assert.Equal(MappingBehavior.Inclusive, result);
        }

        [Fact]
        public void GetMappingBehavior_Razor()
        {
            // Arrange
            var documentPath = "/path/to/document.razor";

            // Act
            var result = RazorBreakpointSpanEndpoint.GetMappingBehavior(documentPath);

            // Assert
            Assert.Equal(MappingBehavior.Strict, result);
        }

        [Fact]
        public async Task Handle_DocumentVersionFailed_ReturnsNull()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var codeDocument = CreateCodeDocument(@"
<p>@DateTime.Now</p>");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);
            var documentVersionCache = new Mock<DocumentVersionCache>(MockBehavior.Strict);
            int? version = default;
            documentVersionCache.Setup(cache => cache.TryGetDocumentVersion(It.IsAny<DocumentSnapshot>(), out version))
                .Returns(false);

            var diagnosticsEndpoint = new RazorBreakpointSpanEndpoint(LegacyDispatcher, documentResolver, documentVersionCache.Object, MappingService, LoggerFactory);
            var request = new RazorBreakpointSpanParams()
            {
                Uri = new Uri(documentPath),
                Position = new Position(1, 0)
            };

            // Act
            var response = await Task.Run(() => diagnosticsEndpoint.Handle(request, default));

            // Assert
            Assert.Null(response);
        }

        [Fact]
        public async Task Handle_UnsupportedDocument_ReturnsNull()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var codeDocument = CreateCodeDocument(@"
<p>@DateTime.Now</p>");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);

            var diagnosticsEndpoint = new RazorBreakpointSpanEndpoint(LegacyDispatcher, documentResolver, DocumentVersionCache, MappingService, LoggerFactory);
            var request = new RazorBreakpointSpanParams()
            {
                Uri = new Uri(documentPath),
                Position = new Position(1, 0)
            };
            codeDocument.SetUnsupported();

            // Act
            var response = await Task.Run(() => diagnosticsEndpoint.Handle(request, default));

            // Assert
            Assert.Null(response);
        }

        [Fact]
        public async Task Handle_StartsInHtml_BreakpointMoved()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var codeDocument = CreateCodeDocument(@"
<p>@{var abc = 123;}</p>");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);

            var diagnosticsEndpoint = new RazorBreakpointSpanEndpoint(LegacyDispatcher, documentResolver, DocumentVersionCache, MappingService, LoggerFactory);
            var request = new RazorBreakpointSpanParams()
            {
                Uri = new Uri(documentPath),
                Position = new Position(1, 0)
            };
            var expectedRange = new Range(new Position(1, 5), new Position(1, 19));

            // Act
            var response = await Task.Run(() => diagnosticsEndpoint.Handle(request, default));

            // Assert
            Assert.Equal(expectedRange, response!.Range);
            Assert.Equal(1337, response.HostDocumentVersion);
        }

        [Fact]
        public async Task Handle_StartsInHtml_InvalidBreakpointSpan_ReturnsNull()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";

            var codeDocument = CreateCodeDocument(@"
<p>@{var abc;}</p>");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);

            var diagnosticsEndpoint = new RazorBreakpointSpanEndpoint(LegacyDispatcher, documentResolver, DocumentVersionCache, MappingService, LoggerFactory);
            var request = new RazorBreakpointSpanParams()
            {
                Uri = new Uri(documentPath),
                Position = new Position(1, 0)
            };

            // Act
            var response = await Task.Run(() => diagnosticsEndpoint.Handle(request, default));

            // Assert
            Assert.Null(response);
        }

        [Fact]
        public async Task Handle_StartInHtml_NoCSharpOnLine_ReturnsNull()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var codeDocument = CreateCodeDocument(@"
<p></p>");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);

            var diagnosticsEndpoint = new RazorBreakpointSpanEndpoint(LegacyDispatcher, documentResolver, DocumentVersionCache, MappingService, LoggerFactory);
            var request = new RazorBreakpointSpanParams()
            {
                Uri = new Uri(documentPath),
                Position = new Position(1, 0)
            };

            // Act
            var response = await Task.Run(() => diagnosticsEndpoint.Handle(request, default));

            // Assert
            Assert.Null(response);
        }

        [Fact]
        public async Task Handle_StartInHtml_NoActualCSharp_ReturnsNull()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var codeDocument = CreateCodeDocument(
                @"
<p>@{
    var abc = 123;
}</p>");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);

            var diagnosticsEndpoint = new RazorBreakpointSpanEndpoint(LegacyDispatcher, documentResolver, DocumentVersionCache, MappingService, LoggerFactory);
            var request = new RazorBreakpointSpanParams()
            {
                Uri = new Uri(documentPath),
                Position = new Position(1, 0)
            };

            // Act
            var response = await Task.Run(() => diagnosticsEndpoint.Handle(request, default));

            // Assert
            Assert.Null(response);
        }

        [Fact]
        public async Task Handle_InvalidBreakpointSpan_ReturnsNull()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var codeDocument = CreateCodeDocument(@"
<p>@{

    var abc = 123;
}</p>");
            var documentResolver = CreateDocumentResolver(documentPath, codeDocument);

            var diagnosticsEndpoint = new RazorBreakpointSpanEndpoint(LegacyDispatcher, documentResolver, DocumentVersionCache, MappingService, LoggerFactory);
            var request = new RazorBreakpointSpanParams()
            {
                Uri = new Uri(documentPath),
                Position = new Position(2, 0)
            };

            // Act
            var response = await Task.Run(() => diagnosticsEndpoint.Handle(request, default));

            // Assert
            Assert.Null(response);
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
            return documentResolver.Object;
        }

        private static RazorCodeDocument CreateCodeDocument(string text)
        {
            var sourceDocument = TestRazorSourceDocument.Create(text);
            var projectEngine = RazorProjectEngine.Create(builder => { });
            var codeDocument = projectEngine.ProcessDesignTime(sourceDocument, FileKinds.Legacy, Array.Empty<RazorSourceDocument>(), Array.Empty<TagHelperDescriptor>());
            return codeDocument;
        }
    }
}
