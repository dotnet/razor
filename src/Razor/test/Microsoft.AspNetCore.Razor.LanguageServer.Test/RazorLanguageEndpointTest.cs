// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class RazorLanguageEndpointTest : LanguageServerTestBase
    {
        public RazorLanguageEndpointTest()
        {
            MappingService = new DefaultRazorDocumentMappingService(TestLanguageServerFeatureOptions.Instance, new TestDocumentContextFactory(), LoggerFactory);
        }

        private RazorDocumentMappingService MappingService { get; }

        // These are more integration tests to validate that all the pieces work together
        [Fact]
        public async Task Handle_MapToDocumentRanges_CSharp()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocumentWithCSharpProjection(
                "<p>@DateTime.Now</p>",
                "var __o = DateTime.Now",
                new[] {
                    new SourceMapping(
                        new SourceSpan(4, 12),
                        new SourceSpan(10, 12))
                });
            var documentResolver = CreateDocumentContextFactory(documentPath, codeDocument);
            var languageEndpoint = new RazorLanguageEndpoint(documentResolver, MappingService, Mock.Of<RazorFormattingService>(MockBehavior.Strict), LoggerFactory);
            var request = new RazorMapToDocumentRangesParams()
            {
                Kind = RazorLanguageKind.CSharp,
                ProjectedRanges = new[] { new Range { Start = new Position(0, 10), End = new Position(0, 22) }, },
                RazorDocumentUri = documentPath,
            };
            var expectedRange = new Range { Start = new Position(0, 4), End = new Position(0, 16) };

            // Act
            var response = await Task.Run(() => languageEndpoint.Handle(request, default));

            // Assert
            Assert.NotNull(response);
            Assert.Equal(expectedRange, response!.Ranges[0]);
            Assert.Equal(1337, response.HostDocumentVersion);
        }

        [Fact]
        public async Task Handle_MapToDocumentRanges_CSharp_Unmapped()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocumentWithCSharpProjection(
                "<p>@DateTime.Now</p>",
                "var __o = DateTime.Now",
                new[] {
                    new SourceMapping(
                        new SourceSpan(4, 12),
                        new SourceSpan(10, 12))
                });
            var documentResolver = CreateDocumentContextFactory(documentPath, codeDocument);
            var languageEndpoint = new RazorLanguageEndpoint(documentResolver, MappingService, Mock.Of<RazorFormattingService>(MockBehavior.Strict), LoggerFactory);
            var request = new RazorMapToDocumentRangesParams()
            {
                Kind = RazorLanguageKind.CSharp,
                ProjectedRanges = new[] { new Range { Start = new Position(0, 0), End = new Position(0, 3) } },
                RazorDocumentUri = documentPath,
            };

            // Act
            var response = await Task.Run(() => languageEndpoint.Handle(request, default));

            // Assert
            Assert.NotNull(response);
            Assert.Equal(RangeExtensions.UndefinedRange, response!.Ranges[0]);
            Assert.Equal(1337, response.HostDocumentVersion);
        }

        [Fact]
        public async Task Handle_MapToDocumentRanges_CSharp_LeadingOverlapsUnmapped()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocumentWithCSharpProjection(
                "<p>@DateTime.Now</p>",
                "var __o = DateTime.Now",
                new[] {
                    new SourceMapping(
                        new SourceSpan(4, 12),
                        new SourceSpan(10, 12))
                });
            var documentResolver = CreateDocumentContextFactory(documentPath, codeDocument);
            var languageEndpoint = new RazorLanguageEndpoint(documentResolver, MappingService, Mock.Of<RazorFormattingService>(MockBehavior.Strict), LoggerFactory);
            var request = new RazorMapToDocumentRangesParams()
            {
                Kind = RazorLanguageKind.CSharp,
                ProjectedRanges = new[] { new Range { Start = new Position(0, 0), End = new Position(0, 22) } },
                RazorDocumentUri = documentPath,
            };

            // Act
            var response = await Task.Run(() => languageEndpoint.Handle(request, default));

            // Assert
            Assert.NotNull(response);
            Assert.Equal(RangeExtensions.UndefinedRange, response!.Ranges[0]);
            Assert.Equal(1337, response.HostDocumentVersion);
        }

        [Fact]
        public async Task Handle_MapToDocumentRanges_CSharp_TrailingOverlapsUnmapped()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocumentWithCSharpProjection(
                "<p>@DateTime.Now</p>",
                "var __o = DateTime.Now",
                new[] {
                    new SourceMapping(
                        new SourceSpan(4, 12),
                        new SourceSpan(10, 12))
                });
            var documentResolver = CreateDocumentContextFactory(documentPath, codeDocument);
            var languageEndpoint = new RazorLanguageEndpoint(documentResolver, MappingService, Mock.Of<RazorFormattingService>(MockBehavior.Strict), LoggerFactory);
            var request = new RazorMapToDocumentRangesParams()
            {
                Kind = RazorLanguageKind.CSharp,
                ProjectedRanges = new[] { new Range { Start = new Position(0, 10), End = new Position(0, 23) } },
                RazorDocumentUri = documentPath,
            };

            // Act
            var response = await Task.Run(() => languageEndpoint.Handle(request, default));

            // Assert
            Assert.NotNull(response);
            Assert.Equal(RangeExtensions.UndefinedRange, response!.Ranges[0]);
            Assert.Equal(1337, response.HostDocumentVersion);
        }

        [Fact]
        public async Task Handle_MapToDocumentRanges_Html()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocument("<p>@DateTime.Now</p>");
            var documentResolver = CreateDocumentContextFactory(documentPath, codeDocument);
            var languageEndpoint = new RazorLanguageEndpoint(documentResolver, MappingService, Mock.Of<RazorFormattingService>(MockBehavior.Strict), LoggerFactory);
            var request = new RazorMapToDocumentRangesParams()
            {
                Kind = RazorLanguageKind.Html,
                ProjectedRanges = new[] { new Range { Start = new Position(0, 16), End = new Position(0, 20) } },
                RazorDocumentUri = documentPath,
            };

            // Act
            var response = await Task.Run(() => languageEndpoint.Handle(request, default));

            // Assert
            Assert.NotNull(response);
            Assert.Equal(request.ProjectedRanges[0], response!.Ranges[0]);
            Assert.Equal(1337, response.HostDocumentVersion);
        }

        [Fact]
        public async Task Handle_MapToDocumentRanges_Razor()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocument("<p>@DateTime.Now</p>");
            var documentResolver = CreateDocumentContextFactory(documentPath, codeDocument);
            var languageEndpoint = new RazorLanguageEndpoint(documentResolver, MappingService, Mock.Of<RazorFormattingService>(MockBehavior.Strict), LoggerFactory);
            var request = new RazorMapToDocumentRangesParams()
            {
                Kind = RazorLanguageKind.Razor,
                ProjectedRanges = new[] { new Range { Start = new Position(0, 3), End = new Position(0, 4) } },
                RazorDocumentUri = documentPath,
            };

            // Act
            var response = await Task.Run(() => languageEndpoint.Handle(request, default));

            // Assert
            Assert.NotNull(response);
            Assert.Equal(request.ProjectedRanges[0], response!.Ranges[0]);
            Assert.Equal(1337, response.HostDocumentVersion);
        }

        [Fact]
        public async Task Handle_MapToDocumentRanges_Unsupported()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocumentWithCSharpProjection(
                "<p>@DateTime.Now</p>",
                "var __o = DateTime.Now",
                new[] {
                    new SourceMapping(
                        new SourceSpan(4, 12),
                        new SourceSpan(10, 12))
                });
            codeDocument.SetUnsupported();
            var documentResolver = CreateDocumentContextFactory(documentPath, codeDocument);
            var languageEndpoint = new RazorLanguageEndpoint(documentResolver, MappingService, Mock.Of<RazorFormattingService>(MockBehavior.Strict), LoggerFactory);
            var request = new RazorMapToDocumentRangesParams()
            {
                Kind = RazorLanguageKind.CSharp,
                ProjectedRanges = new[] { new Range { Start = new Position(0, 10), End = new Position(0, 22) } },
                RazorDocumentUri = documentPath,
            };

            // Act
            var response = await Task.Run(() => languageEndpoint.Handle(request, default));

            // Assert
            Assert.NotNull(response);
            Assert.Equal(RangeExtensions.UndefinedRange, response!.Ranges[0]);
            Assert.Equal(1337, response.HostDocumentVersion);
        }

        [Fact]
        public async Task Handle_ResolvesLanguageRequest_Razor()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocument("@{}");
            var documentResolver = CreateDocumentContextFactory(documentPath, codeDocument);
            var languageEndpoint = new RazorLanguageEndpoint(documentResolver, MappingService, Mock.Of<RazorFormattingService>(MockBehavior.Strict), LoggerFactory);
            var request = new RazorLanguageQueryParams()
            {
                Uri = documentPath,
                Position = new Position(0, 1),
            };

            // Act
            var response = await Task.Run(() => languageEndpoint.Handle(request, default));

            // Assert
            Assert.Equal(RazorLanguageKind.Razor, response.Kind);
            Assert.Equal(request.Position, response.Position);
            Assert.Equal(1337, response.HostDocumentVersion);
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_ResolvesLanguageRequest_Html()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocument("<s");
            var documentResolver = CreateDocumentContextFactory(documentPath, codeDocument);
            var languageEndpoint = new RazorLanguageEndpoint(documentResolver, MappingService, Mock.Of<RazorFormattingService>(MockBehavior.Strict), LoggerFactory);
            var request = new RazorLanguageQueryParams()
            {
                Uri = documentPath,
                Position = new Position(0, 2),
            };

            // Act
            var response = await Task.Run(() => languageEndpoint.Handle(request, default));

            // Assert
            Assert.Equal(RazorLanguageKind.Html, response.Kind);
            Assert.Equal(request.Position, response.Position);
            Assert.Equal(1337, response.HostDocumentVersion);
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_ResolvesLanguageRequest_CSharp()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocumentWithCSharpProjection(
                "@",
                "/* CSharp */",
                new[] { new SourceMapping(new SourceSpan(0, 1), new SourceSpan(0, 12)) });
            var documentResolver = CreateDocumentContextFactory(documentPath, codeDocument);
            var languageEndpoint = new RazorLanguageEndpoint(documentResolver, MappingService, Mock.Of<RazorFormattingService>(MockBehavior.Strict), LoggerFactory);
            var request = new RazorLanguageQueryParams()
            {
                Uri = documentPath,
                Position = new Position(0, 1),
            };

            // Act
            var response = await Task.Run(() => languageEndpoint.Handle(request, default));

            // Assert
            Assert.Equal(RazorLanguageKind.CSharp, response.Kind);
            Assert.Equal(0, response.Position.Line);
            Assert.Equal(1, response.Position.Character);
            Assert.Equal(1337, response.HostDocumentVersion);
        }

        // This is more of an integration test to validate that all the pieces work together
        [Fact]
        public async Task Handle_Unsupported_ResolvesLanguageRequest_Html()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocumentWithCSharpProjection(
                "@",
                "/* CSharp */",
                new[] { new SourceMapping(new SourceSpan(0, 1), new SourceSpan(0, 12)) });
            codeDocument.SetUnsupported();
            var documentResolver = CreateDocumentContextFactory(documentPath, codeDocument);
            var languageEndpoint = new RazorLanguageEndpoint(documentResolver, MappingService, Mock.Of<RazorFormattingService>(MockBehavior.Strict), LoggerFactory);
            var request = new RazorLanguageQueryParams()
            {
                Uri = documentPath,
                Position = new Position(0, 1),
            };

            // Act
            var response = await Task.Run(() => languageEndpoint.Handle(request, default));

            // Assert
            Assert.Equal(RazorLanguageKind.Html, response.Kind);
            Assert.Equal(0, response.Position.Line);
            Assert.Equal(1, response.Position.Character);
            Assert.Equal(1337, response.HostDocumentVersion);
        }

        private static RazorCodeDocument CreateCodeDocumentWithCSharpProjection(string razorSource, string projectedCSharpSource, IEnumerable<SourceMapping> sourceMappings)
        {
            var codeDocument = CreateCodeDocument(razorSource, Array.Empty<TagHelperDescriptor>());
            var csharpDocument = RazorCSharpDocument.Create(
                    projectedCSharpSource,
                    RazorCodeGenerationOptions.CreateDefault(),
                    Enumerable.Empty<RazorDiagnostic>(),
                    sourceMappings,
                    Enumerable.Empty<LinePragma>());
            codeDocument.SetCSharpDocument(csharpDocument);
            return codeDocument;
        }
    }
}
