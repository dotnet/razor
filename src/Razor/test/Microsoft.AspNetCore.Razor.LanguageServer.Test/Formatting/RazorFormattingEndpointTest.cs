// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    public class RazorFormattingEndpointTest : LanguageServerTestBase
    {
        public RazorFormattingEndpointTest()
        {
            EmptyDocumentResolver = Mock.Of<DocumentResolver>(r => r.TryResolveDocument(It.IsAny<string>(), out It.Ref<DocumentSnapshot?>.IsAny) == false, MockBehavior.Strict);
        }

        private DocumentResolver EmptyDocumentResolver { get; }

        [Fact]
        public async Task Handle_FormattingEnabled_InvokesFormattingService()
        {
            // Arrange
            var codeDocument = TestRazorCodeDocument.CreateEmpty();
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var formattingService = new TestRazorFormattingService();
            var documentMappingService = new DefaultRazorDocumentMappingService(LoggerFactory);
            var optionsMonitor = GetOptionsMonitor(enableFormatting: true);
            var endpoint = new RazorFormattingEndpoint(
                Dispatcher, documentResolver, formattingService, documentMappingService, optionsMonitor, LoggerFactory);
            var @params = new DocumentRangeFormattingParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri, }
            };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(formattingService.Called);
        }

        [Fact]
        public async Task Handle_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            var formattingService = new TestRazorFormattingService();
            var documentMappingService = new DefaultRazorDocumentMappingService(LoggerFactory);
            var optionsMonitor = GetOptionsMonitor(enableFormatting: true);
            var endpoint = new RazorFormattingEndpoint(
                Dispatcher, EmptyDocumentResolver, formattingService, documentMappingService, optionsMonitor, LoggerFactory);
            var uri = new Uri("file://path/test.razor");
            var @params = new DocumentRangeFormattingParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri, }
            };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_UnsupportedCodeDocument_ReturnsNull()
        {
            // Arrange
            var codeDocument = TestRazorCodeDocument.CreateEmpty();
            codeDocument.SetUnsupported();
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.AbsolutePath, codeDocument);
            var formattingService = new TestRazorFormattingService();
            var documentMappingService = new DefaultRazorDocumentMappingService(LoggerFactory);
            var optionsMonitor = GetOptionsMonitor(enableFormatting: true);
            var endpoint = new RazorFormattingEndpoint(
                Dispatcher, documentResolver, formattingService, documentMappingService, optionsMonitor, LoggerFactory);
            var @params = new DocumentRangeFormattingParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri, }
            };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_FormattingDisabled_ReturnsNull()
        {
            // Arrange
            var formattingService = new TestRazorFormattingService();
            var documentMappingService = new DefaultRazorDocumentMappingService(LoggerFactory);
            var optionsMonitor = GetOptionsMonitor(enableFormatting: false);
            var endpoint = new RazorFormattingEndpoint(
                Dispatcher, EmptyDocumentResolver, formattingService, documentMappingService, optionsMonitor, LoggerFactory);
            var @params = new DocumentRangeFormattingParamsBridge();

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_OnTypeFormatting_FormattingDisabled_ReturnsNull()
        {
            // Arrange
            var uri = new Uri("file://path/test.razor");
            var formattingService = new TestRazorFormattingService();
            var documentMappingService = new DefaultRazorDocumentMappingService(LoggerFactory);
            var optionsMonitor = GetOptionsMonitor(enableFormatting: false);
            var endpoint = new RazorFormattingEndpoint(
                Dispatcher, EmptyDocumentResolver, formattingService, documentMappingService, optionsMonitor, LoggerFactory);
            var @params = new DocumentOnTypeFormattingParamsBridge { TextDocument = new TextDocumentIdentifier { Uri = uri, } };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_OnTypeFormatting_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            var content = @"
@{
 if(true){}
}";
            var sourceMappings = new List<SourceMapping> { new SourceMapping(new SourceSpan(17, 0), new SourceSpan(17, 0)) };
            var codeDocument = CreateCodeDocument(content, sourceMappings);
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver("file://path/testDifferentFile.razor", codeDocument);
            var formattingService = new TestRazorFormattingService();
            var documentMappingService = new DefaultRazorDocumentMappingService(LoggerFactory);
            var optionsMonitor = GetOptionsMonitor(enableFormatting: true);
            var endpoint = new RazorFormattingEndpoint(
                Dispatcher, documentResolver, formattingService, documentMappingService, optionsMonitor, LoggerFactory);
            var @params = new DocumentOnTypeFormattingParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri, },
                Character = ".",
                Position = new Position(2, 11),
                Options = new FormattingOptions { InsertSpaces = true, TabSize = 4 }
            };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_OnTypeFormatting_RemapFailed_ReturnsNull()
        {
            // Arrange
            var content = @"
@{
 if(true){}
}";
            var sourceMappings = new List<SourceMapping> { };
            var codeDocument = CreateCodeDocument(content, sourceMappings);
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var formattingService = new TestRazorFormattingService();
            var documentMappingService = new DefaultRazorDocumentMappingService(LoggerFactory);
            var optionsMonitor = GetOptionsMonitor(enableFormatting: true);
            var endpoint = new RazorFormattingEndpoint(
                Dispatcher, documentResolver, formattingService, documentMappingService, optionsMonitor, LoggerFactory);
            var @params = new DocumentOnTypeFormattingParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri, },
                Character = ".",
                Position = new Position(2, 11),
                Options = new FormattingOptions { InsertSpaces = true, TabSize = 4 },
            };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_OnTypeFormatting_HtmlLanguageKind_ReturnsNull()
        {
            // Arrange
            var content = @"
@{
 if(true){}
}";
            var sourceMappings = new List<SourceMapping> { new SourceMapping(new SourceSpan(17, 0), new SourceSpan(17, 0)) };
            var codeDocument = CreateCodeDocument(content, sourceMappings);
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var formattingService = new TestRazorFormattingService();
            var documentMappingService = new Mock<RazorDocumentMappingService>(MockBehavior.Strict);
            documentMappingService.Setup(s => s.GetLanguageKind(codeDocument, 17, false)).Returns(RazorLanguageKind.Html);
            var optionsMonitor = GetOptionsMonitor(enableFormatting: true);
            var endpoint = new RazorFormattingEndpoint(
                Dispatcher, documentResolver, formattingService, documentMappingService.Object, optionsMonitor, LoggerFactory);
            var @params = new DocumentOnTypeFormattingParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri, },
                Character = "}",
                Position = new Position(2, 11),
                Options = new FormattingOptions { InsertSpaces = true, TabSize = 4 },
            };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_OnTypeFormatting_RazorLanguageKind_ReturnsNull()
        {
            // Arrange
            var content = @"
@{
 if(true){}
}";
            var sourceMappings = new List<SourceMapping> { new SourceMapping(new SourceSpan(17, 0), new SourceSpan(17, 0)) };
            var codeDocument = CreateCodeDocument(content, sourceMappings);
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var formattingService = new TestRazorFormattingService();
            var documentMappingService = new Mock<RazorDocumentMappingService>(MockBehavior.Strict);
            documentMappingService.Setup(s => s.GetLanguageKind(codeDocument, 17, false)).Returns(RazorLanguageKind.Razor);
            var optionsMonitor = GetOptionsMonitor(enableFormatting: true);
            var endpoint = new RazorFormattingEndpoint(
                Dispatcher, documentResolver, formattingService, documentMappingService.Object, optionsMonitor, LoggerFactory);
            var @params = new DocumentOnTypeFormattingParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri, },
                Character = "}",
                Position = new Position(2, 11),
                Options = new FormattingOptions { InsertSpaces = true, TabSize = 4 }
            };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_OnTypeFormatting_UnexpectedTriggerCharacter_ReturnsNull()
        {
            // Arrange
            var content = @"
@{
 if(true){}
}";
            var sourceMappings = new List<SourceMapping> { new SourceMapping(new SourceSpan(17, 0), new SourceSpan(17, 0)) };
            var codeDocument = CreateCodeDocument(content, sourceMappings);
            var uri = new Uri("file://path/test.razor");
            var documentResolver = CreateDocumentResolver(uri.GetAbsoluteOrUNCPath(), codeDocument);
            var formattingService = new TestRazorFormattingService();
            var documentMappingService = new DefaultRazorDocumentMappingService(LoggerFactory);
            var optionsMonitor = GetOptionsMonitor(enableFormatting: true);
            var endpoint = new RazorFormattingEndpoint(
                Dispatcher, documentResolver, formattingService, documentMappingService, optionsMonitor, LoggerFactory);
            var @params = new DocumentOnTypeFormattingParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri, },
                Character = ".",
                Position = new Position(2, 11),
                Options = new FormattingOptions { InsertSpaces = true, TabSize = 4 }
            };

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        private static IOptionsMonitor<RazorLSPOptions> GetOptionsMonitor(bool enableFormatting)
        {
            var monitor = new Mock<IOptionsMonitor<RazorLSPOptions>>(MockBehavior.Strict);
            monitor.SetupGet(m => m.CurrentValue).Returns(new RazorLSPOptions(default, enableFormatting, true, insertSpaces: true, tabSize: 4));
            return monitor.Object;
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
            documentResolver.Setup(resolver => resolver.TryResolveDocument(It.IsNotIn(documentPath), out documentSnapshot))
                .Returns(false);
            return documentResolver.Object;
        }

        private static RazorCodeDocument CreateCodeDocument(string content, IReadOnlyList<SourceMapping> sourceMappings)
        {
            var sourceDocument = TestRazorSourceDocument.Create(content);
            var codeDocument = RazorCodeDocument.Create(sourceDocument);
            var syntaxTree = RazorSyntaxTree.Parse(sourceDocument, RazorParserOptions.CreateDefault());
            var razorCSharpDocument = RazorCSharpDocument.Create(
                content, RazorCodeGenerationOptions.CreateDefault(), Array.Empty<RazorDiagnostic>(), sourceMappings, Array.Empty<LinePragma>());
            codeDocument.SetSyntaxTree(syntaxTree);
            codeDocument.SetCSharpDocument(razorCSharpDocument);

            return codeDocument;
        }

        private class TestRazorFormattingService : RazorFormattingService
        {
            public bool Called { get; private set; }

            public override Task<TextEdit[]> FormatAsync(Uri uri, DocumentSnapshot documentSnapshot, VisualStudio.LanguageServer.Protocol.Range range, FormattingOptions options, CancellationToken cancellationToken)
            {
                Called = true;
                return Task.FromResult(Array.Empty<TextEdit>());
            }

            public override Task<TextEdit[]> FormatCodeActionAsync(Uri uri, DocumentSnapshot documentSnapshot, RazorLanguageKind kind, TextEdit[] formattedEdits, FormattingOptions options, CancellationToken cancellationToken)
            {
                return Task.FromResult(formattedEdits);
            }

            public override Task<TextEdit[]> FormatOnTypeAsync(Uri uri, DocumentSnapshot documentSnapshot, RazorLanguageKind kind, TextEdit[] formattedEdits, FormattingOptions options, int hostDocumentIndex, char triggerCharacter, CancellationToken cancellationToken)
            {
                return Task.FromResult(formattedEdits);
            }

            public override Task<TextEdit[]> FormatSnippetAsync(Uri uri, DocumentSnapshot documentSnapshot, RazorLanguageKind kind, TextEdit[] formattedEdits, FormattingOptions options, CancellationToken cancellationToken)
            {
                return Task.FromResult(formattedEdits);
            }

            public override Task<OmniSharp.Extensions.LanguageServer.Protocol.Models.TextEdit[]> OmniFormatOnTypeAsync(Uri uri, DocumentSnapshot documentSnapshot, RazorLanguageKind kind, OmniSharp.Extensions.LanguageServer.Protocol.Models.TextEdit[] formattedEdits, OmniSharp.Extensions.LanguageServer.Protocol.Models.FormattingOptions options, int hostDocumentIndex, char triggerCharacter, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }
    }
}
