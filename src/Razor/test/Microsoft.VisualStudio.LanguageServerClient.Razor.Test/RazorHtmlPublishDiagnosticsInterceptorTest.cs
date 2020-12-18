// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp;
using Microsoft.VisualStudio.Text;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    public class RazorHtmlPublishDiagnosticsInterceptorTest
    {
        private static readonly Uri RazorUri = new Uri("C:/path/to/file.razor");
        private static readonly Uri CshtmlUri = new Uri("C:/path/to/file.cshtml");
        private static readonly Uri RazorVirtualHtmlUri = new Uri("C:/path/to/file.razor__virtual.html");

        private static readonly Diagnostic ValidDiagnostic_HTML = new Diagnostic()
        {
            Range = new Range()
            {
                Start = new Position(149, 19),
                End = new Position(149, 23)
            },
            Code = null
        };

        private static readonly Diagnostic ValidDiagnostic_CSS = new Diagnostic()
        {
            Range = new Range()
            {
                Start = new Position(150, 19),
                End = new Position(150, 23)
            },
            Code = "expectedSemicolon",
        };

        private static readonly Diagnostic[] Diagnostics = new Diagnostic[]
        {
            ValidDiagnostic_HTML,
            ValidDiagnostic_CSS
        };

        [Fact]
        public async Task ApplyChangesAsync_InvalidParams()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var diagnosticsProvider = Mock.Of<LSPDiagnosticsProvider>();

            var htmlDiagnosticsInterceptor = new RazorHtmlPublishDiagnosticsInterceptor(documentManager, diagnosticsProvider);
            var diagnosticRequest = new CodeActionParams()
            {
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = RazorUri
                }
            };

            // Act
            await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
                    await htmlDiagnosticsInterceptor.ApplyChangesAsync(
                        JToken.FromObject(diagnosticRequest),
                        containedLanguageName: string.Empty,
                        cancellationToken: default).ConfigureAwait(false)).ConfigureAwait(false);
        }
    }
}
