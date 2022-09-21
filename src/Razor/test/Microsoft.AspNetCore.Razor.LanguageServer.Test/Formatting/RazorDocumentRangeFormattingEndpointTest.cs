// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    public class RazorDocumentRangeFormattingEndpointTest : FormattingLanguageServerTestBase
    {
        [Fact]
        public async Task Handle_FormattingEnabled_InvokesFormattingService()
        {
            // Arrange
            var codeDocument = TestRazorCodeDocument.CreateEmpty();
            var uri = new Uri("file://path/test.razor");

            var documentContext = CreateDocumentContext(uri, codeDocument);
            var formattingService = new DummyRazorFormattingService();

            var optionsMonitor = GetOptionsMonitor(enableFormatting: true);
            var endpoint = new RazorDocumentRangeFormattingEndpoint(
                formattingService, optionsMonitor);
            var @params = new DocumentRangeFormattingParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri, }
            };
            var requestContext = CreateRazorRequestContext(documentContext);

            // Act
            var result = await endpoint.HandleRequestAsync(@params, requestContext, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.True(formattingService.Called);
        }

        [Fact]
        public async Task Handle_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            var formattingService = new DummyRazorFormattingService();
            var optionsMonitor = GetOptionsMonitor(enableFormatting: true);
            var endpoint = new RazorDocumentRangeFormattingEndpoint(formattingService, optionsMonitor);
            var uri = new Uri("file://path/test.razor");
            var @params = new DocumentRangeFormattingParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri, }
            };
            var requestContext = CreateRazorRequestContext(documentContext: null);

            // Act
            var result = await endpoint.HandleRequestAsync(@params, requestContext, CancellationToken.None);

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

            var documentContext = CreateDocumentContext(uri, codeDocument);
            var formattingService = new DummyRazorFormattingService();
            var optionsMonitor = GetOptionsMonitor(enableFormatting: true);
            var endpoint = new RazorDocumentRangeFormattingEndpoint(formattingService, optionsMonitor);
            var @params = new DocumentRangeFormattingParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri, }
            };
            var requestContext = CreateRazorRequestContext(documentContext);

            // Act
            var result = await endpoint.HandleRequestAsync(@params, requestContext, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task Handle_FormattingDisabled_ReturnsNull()
        {
            // Arrange
            var formattingService = new DummyRazorFormattingService();
            var optionsMonitor = GetOptionsMonitor(enableFormatting: false);
            var endpoint = new RazorDocumentRangeFormattingEndpoint(formattingService, optionsMonitor);
            var @params = new DocumentRangeFormattingParamsBridge();
            var requestContext = CreateRazorRequestContext(documentContext: null);

            // Act
            var result = await endpoint.HandleRequestAsync(@params, requestContext, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }
    }
}
