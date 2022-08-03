// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Debugging;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts.Debugging;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Debugging
{
    public class RazorProximityExpressionsEndpointTest : LanguageServerTestBase
    {
        public RazorProximityExpressionsEndpointTest()
        {
            MappingService = new DefaultRazorDocumentMappingService(TestLanguageServerFeatureOptions.Instance, new TestDocumentContextFactory(), LoggerFactory);
        }

        private RazorDocumentMappingService MappingService { get; }

        [Fact]
        public async Task Handle_UnsupportedDocument_ReturnsNull()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocument(@"
<p>@DateTime.Now</p>");
            var documentContextFactory = CreateDocumentContextFactory(documentPath, codeDocument);

            var diagnosticsEndpoint = new RazorProximityExpressionsEndpoint(documentContextFactory, MappingService, LoggerFactory);
            var request = new RazorProximityExpressionsParamsBridge()
            {
                Uri = documentPath,
                Position = new Position(1, 0),
            };
            codeDocument.SetUnsupported();

            // Act
            var response = await Task.Run(() => diagnosticsEndpoint.Handle(request, default));

            // Assert
            Assert.Null(response);
        }

        [Fact]
        public async Task Handle_ReturnsValidExpressions()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocument(@"
<p>@{var abc = 123;}</p>");
            var documentContextFactory = CreateDocumentContextFactory(documentPath, codeDocument);

            var endpoint = new RazorProximityExpressionsEndpoint(documentContextFactory, MappingService, LoggerFactory);
            var request = new RazorProximityExpressionsParamsBridge()
            {
                Uri = documentPath,
                Position = new Position(1, 8),
            };

            // Act
            var response = await Task.Run(() => endpoint.Handle(request, default));

            // Assert
            Assert.Contains("abc", response!.Expressions);
            Assert.Contains("this", response!.Expressions);
        }

        [Fact]
        public async Task Handle_StartsInHtml_ReturnsValidExpressions()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocument(@"
<p>@{var abc = 123;}</p>");
            var documentContextFactory = CreateDocumentContextFactory(documentPath, codeDocument);

            var endpoint = new RazorProximityExpressionsEndpoint(documentContextFactory, MappingService, LoggerFactory);
            var request = new RazorProximityExpressionsParamsBridge()
            {
                Uri = documentPath,
                Position = new Position(1, 0),
            };

            // Act
            var response = await Task.Run(() => endpoint.Handle(request, default));

            // Assert
            Assert.Contains("abc", response!.Expressions);
            Assert.Contains("this", response!.Expressions);
        }

        [Fact]
        public async Task Handle_StartInHtml_NoCSharpOnLine_ReturnsNull()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocument(@"
<p></p>");
            var documentContextFactory = CreateDocumentContextFactory(documentPath, codeDocument);

            var diagnosticsEndpoint = new RazorProximityExpressionsEndpoint(documentContextFactory, MappingService, LoggerFactory);
            var request = new RazorProximityExpressionsParamsBridge()
            {
                Uri = documentPath,
                Position = new Position(1, 0),
            };

            // Act
            var response = await Task.Run(() => diagnosticsEndpoint.Handle(request, default));

            // Assert
            Assert.Null(response);
        }

        [Fact]
        public async Task Handle_InvalidLocation_ReturnsNull()
        {
            // Arrange
            var documentPath = new Uri("C:/path/to/document.cshtml");
            var codeDocument = CreateCodeDocument(@"
<p>@{

    var abc = 123;
}</p>");
            var documentContextFactory = CreateDocumentContextFactory(documentPath, codeDocument);

            var diagnosticsEndpoint = new RazorProximityExpressionsEndpoint(documentContextFactory, MappingService, LoggerFactory);
            var request = new RazorProximityExpressionsParamsBridge()
            {
                Uri = documentPath,
                Position = new Position(0, 0),
            };

            // Act
            var response = await Task.Run(() => diagnosticsEndpoint.Handle(request, default));

            // Assert
            Assert.Null(response);
        }
    }
}
