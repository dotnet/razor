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
            var documentContextFactory = CreateDocumentContextFactory(uri, codeDocument);
            var formattingService = new DummyRazorFormattingService();
            var optionsMonitor = GetOptionsMonitor(enableFormatting: true);
            var languageServerFeatureOptions = new DefaultLanguageServerFeatureOptions();
            var endpoint = new RazorDocumentRangeFormattingEndpoint(
                documentContextFactory, formattingService, optionsMonitor, languageServerFeatureOptions);
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
            var formattingService = new DummyRazorFormattingService();
            var optionsMonitor = GetOptionsMonitor(enableFormatting: true);
            var languageServerFeatureOptions = new DefaultLanguageServerFeatureOptions();
            var endpoint = new RazorDocumentRangeFormattingEndpoint(
                EmptyDocumentContextFactory, formattingService, optionsMonitor, languageServerFeatureOptions);
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
            var documentContextFactory = CreateDocumentContextFactory(uri, codeDocument);
            var formattingService = new DummyRazorFormattingService();
            var optionsMonitor = GetOptionsMonitor(enableFormatting: true);
            var languageServerFeatureOptions = new DefaultLanguageServerFeatureOptions();
            var endpoint = new RazorDocumentRangeFormattingEndpoint(
                documentContextFactory, formattingService, optionsMonitor, languageServerFeatureOptions);
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
            var formattingService = new DummyRazorFormattingService();
            var optionsMonitor = GetOptionsMonitor(enableFormatting: false);
            var languageServerFeatureOptions = new DefaultLanguageServerFeatureOptions();
            var endpoint = new RazorDocumentRangeFormattingEndpoint(
                EmptyDocumentContextFactory, formattingService, optionsMonitor, languageServerFeatureOptions);
            var @params = new DocumentRangeFormattingParamsBridge();

            // Act
            var result = await endpoint.Handle(@params, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }
    }
}
