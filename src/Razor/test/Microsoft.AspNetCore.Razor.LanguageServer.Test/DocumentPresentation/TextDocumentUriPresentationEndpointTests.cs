// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation
{
    public class TextDocumentUriPresentationEndpointTests : LanguageServerTestBase
    {
        [Fact]
        public async Task Handle_SimpleComponent_ReturnsResult()
        {
            // Arrange
            var codeDocument = TestRazorCodeDocument.Create("<div></div>");
            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>()) == RazorLanguageKind.Html, MockBehavior.Strict);

            var componentCodeDocument = TestRazorCodeDocument.Create("<div></div>");
            var droppedUri = new Uri("file:///c:/path/MyTagHelper.razor");
            var builder = TagHelperDescriptorBuilder.Create("MyTagHelper", "MyAssembly");
            builder.SetTypeNameIdentifier("MyTagHelper");
            var tagHelperDescriptor = builder.Build();

            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument, droppedUri.GetAbsoluteOrUNCPath(), componentCodeDocument);
            var searchEngine = Mock.Of<RazorComponentSearchEngine>(
                s => s.TryGetTagHelperDescriptorAsync(It.IsAny<DocumentSnapshot>()) == Task.FromResult(tagHelperDescriptor),
                MockBehavior.Strict);

            var endpoint = new TextDocumentUriPresentationEndpoint(
                Dispatcher,
                documentResolver,
                documentMappingService,
                searchEngine,
                LoggerFactory);

            var parameters = new UriPresentationParams(new TextDocumentIdentifier(uri), new(0, 1, 0, 2))
            {
                Uris = new[] { droppedUri }
            };

            // Act
            var result = await endpoint.Handle(parameters, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("<MyTagHelper />", result?.DocumentChanges?.First().TextDocumentEdit?.Edits.First().NewText);
        }

        [Fact]
        public async Task Handle_SimpleComponentWithChildFile_ReturnsResult()
        {
            // Arrange
            var codeDocument = TestRazorCodeDocument.Create("<div></div>");
            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>()) == RazorLanguageKind.Html, MockBehavior.Strict);

            var componentCodeDocument = TestRazorCodeDocument.Create("<div></div>");
            var droppedUri = new Uri("file:///c:/path/MyTagHelper.razor");
            var builder = TagHelperDescriptorBuilder.Create("MyTagHelper", "MyAssembly");
            builder.SetTypeNameIdentifier("MyTagHelper");
            var tagHelperDescriptor = builder.Build();

            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument, droppedUri.GetAbsoluteOrUNCPath(), componentCodeDocument);
            var searchEngine = Mock.Of<RazorComponentSearchEngine>(
                s => s.TryGetTagHelperDescriptorAsync(It.IsAny<DocumentSnapshot>()) == Task.FromResult(tagHelperDescriptor),
                MockBehavior.Strict);

            var endpoint = new TextDocumentUriPresentationEndpoint(
                Dispatcher,
                documentResolver,
                documentMappingService,
                searchEngine,
                LoggerFactory);

            var parameters = new UriPresentationParams(new TextDocumentIdentifier(uri), new(0, 1, 0, 2))
            {
                Uris = new[] {
                    new Uri("file:///c:/path/MyTagHelper.razor.cs"),
                    new Uri("file:///c:/path/MyTagHelper.razor.css"),
                    droppedUri,
                }
            };

            // Act
            var result = await endpoint.Handle(parameters, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("<MyTagHelper />", result?.DocumentChanges?.First().TextDocumentEdit?.Edits.First().NewText);
        }

        [Fact]
        public async Task Handle_ComponentWithRequiredAttribute_ReturnsResult()
        {
            // Arrange
            var codeDocument = TestRazorCodeDocument.Create("<div></div>");
            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>()) == RazorLanguageKind.Html, MockBehavior.Strict);

            var componentCodeDocument = TestRazorCodeDocument.Create("<div></div>");
            var droppedUri = new Uri("file:///c:/path/MyTagHelper.razor");
            var builder = TagHelperDescriptorBuilder.Create("MyTagHelper", "MyAssembly");
            builder.SetTypeNameIdentifier("MyTagHelper");
            builder.BindAttribute(b =>
            {
                b.IsEditorRequired = true;
                b.Name = "MyAttribute";
            });
            builder.BindAttribute(b =>
            {
                b.Name = "MyNonRequiredAttribute";
            });
            var tagHelperDescriptor = builder.Build();

            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument, droppedUri.GetAbsoluteOrUNCPath(), componentCodeDocument);
            var searchEngine = Mock.Of<RazorComponentSearchEngine>(
                s => s.TryGetTagHelperDescriptorAsync(It.IsAny<DocumentSnapshot>()) == Task.FromResult(tagHelperDescriptor),
                MockBehavior.Strict);

            var endpoint = new TextDocumentUriPresentationEndpoint(
                Dispatcher,
                documentResolver,
                documentMappingService,
                searchEngine,
                LoggerFactory);

            var parameters = new UriPresentationParams(new TextDocumentIdentifier(uri), new(0, 1, 0, 2))
            {
                Uris = new[] { droppedUri }
            };

            // Act
            var result = await endpoint.Handle(parameters, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("<MyTagHelper MyAttribute=\"\" />", result?.DocumentChanges?.First().TextDocumentEdit?.Edits.First().NewText);
        }

        [Fact]
        public async Task Handle_NoTypeNameIdentifier_ReturnsResult()
        {
            // Arrange
            var codeDocument = TestRazorCodeDocument.Create("<div></div>");
            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>()) == RazorLanguageKind.Html, MockBehavior.Strict);

            var componentCodeDocument = TestRazorCodeDocument.Create("<div></div>");
            var droppedUri = new Uri("file:///c:/path/MyTagHelper.razor");
            var builder = TagHelperDescriptorBuilder.Create("MyTagHelper", "MyAssembly");
            var tagHelperDescriptor = builder.Build();

            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument, droppedUri.GetAbsoluteOrUNCPath(), componentCodeDocument);
            var searchEngine = Mock.Of<RazorComponentSearchEngine>(
                s => s.TryGetTagHelperDescriptorAsync(It.IsAny<DocumentSnapshot>()) == Task.FromResult(tagHelperDescriptor),
                MockBehavior.Strict);

            var endpoint = new TextDocumentUriPresentationEndpoint(
                Dispatcher,
                documentResolver,
                documentMappingService,
                searchEngine,
                LoggerFactory);

            var parameters = new UriPresentationParams(new TextDocumentIdentifier(uri), new(0, 1, 0, 2))
            {
                Uris = new[] { droppedUri }
            };

            // Act
            var result = await endpoint.Handle(parameters, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_MultipleUris_ReturnsResult()
        {
            // Arrange
            var codeDocument = TestRazorCodeDocument.Create("<div></div>");
            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>()) == RazorLanguageKind.Html, MockBehavior.Strict);

            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var searchEngine = Mock.Of<RazorComponentSearchEngine>(MockBehavior.Strict);

            var endpoint = new TextDocumentUriPresentationEndpoint(
                Dispatcher,
                documentResolver,
                documentMappingService,
                searchEngine,
                LoggerFactory);

            var parameters = new UriPresentationParams(new TextDocumentIdentifier(uri), new(0, 1, 0, 2))
            {
                Uris = new[] {
                    new Uri("file:///c:/path/SomeOtherFile.cs"),
                    new Uri("file:///c:/path/Bar.Foo"),
                    new Uri("file:///c:/path/MyTagHelper.razor"),
                }
            };

            // Act
            var result = await endpoint.Handle(parameters, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_NotComponent_ReturnsResult()
        {
            // Arrange
            var codeDocument = TestRazorCodeDocument.Create("<div></div>");
            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>()) == RazorLanguageKind.Html, MockBehavior.Strict);

            var droppedUri = new Uri("file:///c:/path/MyTagHelper.cshtml");
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var searchEngine = Mock.Of<RazorComponentSearchEngine>(MockBehavior.Strict);

            var endpoint = new TextDocumentUriPresentationEndpoint(
                Dispatcher,
                documentResolver,
                documentMappingService,
                searchEngine,
                LoggerFactory);

            var parameters = new UriPresentationParams(new TextDocumentIdentifier(uri), new(0, 1, 0, 2))
            {
                Uris = new[] { droppedUri }
            };

            // Act
            var result = await endpoint.Handle(parameters, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_CSharp_ReturnsNull()
        {
            // Arrange
            var codeDocument = TestRazorCodeDocument.Create("@counter");
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>()) == RazorLanguageKind.CSharp, MockBehavior.Strict);
            var searchEngine = Mock.Of<RazorComponentSearchEngine>(MockBehavior.Strict);

            var endpoint = new TextDocumentUriPresentationEndpoint(
                Dispatcher,
                documentResolver,
                documentMappingService,
                searchEngine,
                LoggerFactory);

            var parameters = new UriPresentationParams(new TextDocumentIdentifier(uri), new(0, 1, 0, 2));

            // Act
            var result = await endpoint.Handle(parameters, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            var codeDocument = TestRazorCodeDocument.Create("<div></div>");
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>()) == RazorLanguageKind.Html, MockBehavior.Strict);
            var searchEngine = Mock.Of<RazorComponentSearchEngine>(MockBehavior.Strict);

            var endpoint = new TextDocumentUriPresentationEndpoint(
                Dispatcher,
                documentResolver,
                documentMappingService,
                searchEngine,
                LoggerFactory);

            var parameters = new UriPresentationParams(new TextDocumentIdentifier(uri), new(0, 1, 0, 2));

            // Act
            var result = await endpoint.Handle(parameters, CancellationToken.None);

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
            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>()) == RazorLanguageKind.Html, MockBehavior.Strict);
            var searchEngine = Mock.Of<RazorComponentSearchEngine>(MockBehavior.Strict);

            var endpoint = new TextDocumentUriPresentationEndpoint(
                Dispatcher,
                documentResolver,
                documentMappingService,
                searchEngine,
                LoggerFactory);

            var parameters = new UriPresentationParams(new TextDocumentIdentifier(uri), new(0, 1, 0, 2));

            // Act
            var result = await endpoint.Handle(parameters, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_NoUris_ReturnsNull()
        {
            // Arrange
            var codeDocument = TestRazorCodeDocument.Create("<div></div>");
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var documentMappingService = Mock.Of<RazorDocumentMappingService>(
                s => s.GetLanguageKind(codeDocument, It.IsAny<int>()) == RazorLanguageKind.Html, MockBehavior.Strict);
            var searchEngine = Mock.Of<RazorComponentSearchEngine>(MockBehavior.Strict);

            var endpoint = new TextDocumentUriPresentationEndpoint(
                Dispatcher,
                documentResolver,
                documentMappingService,
                searchEngine,
                LoggerFactory);

            var parameters = new UriPresentationParams(new TextDocumentIdentifier(uri), new(0, 1, 0, 2));

            // Act
            var result = await endpoint.Handle(parameters, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        private static DocumentResolver CreateDocumentResolver(string documentPath, RazorCodeDocument codeDocument, string? additionalDocumentPath = null, RazorCodeDocument? additionalCodeDocument = null)
        {
            var documentResolver = new Mock<DocumentResolver>(MockBehavior.Strict);
            SetupDocumentResolver(documentPath, codeDocument, documentResolver);

            if (additionalCodeDocument != null)
            {
                Debug.Assert(additionalDocumentPath is not null);

                SetupDocumentResolver(additionalDocumentPath, additionalCodeDocument, documentResolver);
            }

            additionalDocumentPath ??= "<invalid>";

            DocumentSnapshot? nullDocumentSnapshot = null;
            documentResolver.Setup(resolver => resolver.TryResolveDocument(It.IsNotIn(documentPath, additionalDocumentPath), out nullDocumentSnapshot))
                .Returns(false);
            return documentResolver.Object;

            static void SetupDocumentResolver(string documentPath, RazorCodeDocument codeDocument, Mock<DocumentResolver> documentResolver)
            {
                var sourceTextChars = new char[codeDocument.Source.Length];
                codeDocument.Source.CopyTo(0, sourceTextChars, 0, codeDocument.Source.Length);
                var sourceText = SourceText.From(new string(sourceTextChars));
                var documentSnapshot = Mock.Of<DocumentSnapshot>(document =>
                    document.GetGeneratedOutputAsync() == Task.FromResult(codeDocument) &&
                    document.GetTextAsync() == Task.FromResult(sourceText), MockBehavior.Strict);
                documentResolver.Setup(resolver => resolver.TryResolveDocument(documentPath, out documentSnapshot))
                    .Returns(true);
            }
        }
    }
}
