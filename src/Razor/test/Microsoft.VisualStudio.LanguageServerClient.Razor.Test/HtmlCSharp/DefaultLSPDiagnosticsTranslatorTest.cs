// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class DefaultLSPDiagnosticsTranslatorTest
    {
        private readonly ILanguageClient _languageClient = Mock.Of<ILanguageClient>(MockBehavior.Strict);

        [Fact]
        public async Task ProcessDiagnosticsAsync_ReturnsResponse()
        {
            // Arrange
            var response = new RazorDiagnosticsResponse()
            {
                HostDocumentVersion = 5
            };

            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker.Setup(ri => ri.ReinvokeRequestOnServerAsync<RazorDiagnosticsParams, RazorDiagnosticsResponse>(
                    LanguageServerConstants.RazorTranslateDiagnosticsEndpoint,
                    RazorLSPConstants.RazorLanguageServerName,
                    It.IsAny<RazorDiagnosticsParams>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new ReinvokeResponse<RazorDiagnosticsResponse>(_languageClient, response)));

            var diagnosticsProvider = new DefaultLSPDiagnosticsTranslator(requestInvoker.Object);

            // Act
            var diagnosticsResponse = await diagnosticsProvider.TranslateAsync(
                RazorLanguageKind.CSharp,
                new Uri("file://C:/path/to/file"),
                Array.Empty<Diagnostic>(),
                CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Equal(5, diagnosticsResponse.HostDocumentVersion);
        }
    }
}
