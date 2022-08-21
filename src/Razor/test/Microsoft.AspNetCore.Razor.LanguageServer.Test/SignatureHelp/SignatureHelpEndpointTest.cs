// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.SignatureHelp;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Xunit;

using LS = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.SignatureHelp
{
    [UseExportProvider]
    public class SignatureHelpEndpointTest : LanguageServerTestBase
    {
        [Fact]
        public async Task Handle_SingleServer_CSharpSignature()
        {
            var input = """
                <div></div>

                @{
                    string M1(int i) => throw new NotImplementedException();

                    void Act()
                    {
                        M1($$);
                    }
                }
                """;

            await VerifySignatureHelpAsync(input, "string M1(int i)");
        }

        [Fact]
        public async Task Handle_SingleServer_CSharpSignature_Razor()
        {
            var input = """
                <div>@GetDiv($$)</div>

                @{
                    string GetDiv() => "";
                }
                """;

            await VerifySignatureHelpAsync(input, "string GetDiv()");
        }

        [Fact]
        public async Task Handle_SingleServer_ReturnNull()
        {
            var input = """
                <div>@GetDiv($$)</div>

                @{
                }
                """;

            await VerifySignatureHelpAsync(input);
        }

        private async Task VerifySignatureHelpAsync(string input, params string[] signatures)
        {
            // Arrange
            TestFileMarkupParser.GetPositionAndSpans(input, out var output, out int cursorPosition, out ImmutableArray<TextSpan> spans);
            var codeDocument = CreateCodeDocument(output);
            var csharpSourceText = codeDocument.GetCSharpSourceText();
            var csharpDocumentUri = new Uri("C:/path/to/file.razor__virtual.g.cs");
            var serverCapabilities = new ServerCapabilities()
            {
                DocumentHighlightProvider = true
            };
            var csharpServer = await CSharpTestLspServerHelpers.CreateCSharpLspServerAsync(csharpSourceText, csharpDocumentUri, serverCapabilities, razorSpanMappingService: null).ConfigureAwait(false);
            await csharpServer.OpenDocumentAsync(csharpDocumentUri, csharpSourceText.ToString()).ConfigureAwait(false);

            var razorFilePath = "C:/path/to/file.razor";
            var documentContextFactory = new TestDocumentContextFactory(razorFilePath, codeDocument, version: 1337);
            var languageServerFeatureOptions = Mock.Of<LanguageServerFeatureOptions>(options =>
                options.SupportsFileManipulation == true &&
                options.SingleServerSupport == true &&
                options.CSharpVirtualDocumentSuffix == ".g.cs" &&
                options.HtmlVirtualDocumentSuffix == ".g.html"
                , MockBehavior.Strict);
            var languageServer = new SignatureHelpLanguageServer(csharpServer, csharpDocumentUri);
            var documentMappingService = new DefaultRazorDocumentMappingService(languageServerFeatureOptions, documentContextFactory, LoggerFactory);

            var endpoint = new SignatureHelpEndpoint(documentContextFactory, languageServerFeatureOptions, documentMappingService, languageServer, TestLoggerFactory.Instance);

            codeDocument.GetSourceText().GetLineAndOffset(cursorPosition, out var line, out var offset);
            var request = new SignatureHelpParamsBridge
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = new Uri(razorFilePath)
                },
                Position = new Position(line, offset)
            };

            // Act
            var result = await endpoint.Handle(request, CancellationToken.None);

            // Assert

            if (signatures.Length == 0)
            {
                Assert.Null(result);
                return;
            }

            Assert.Equal(signatures.Length, result.Signatures.Length);
            for (var i = 0; i < signatures.Length; i++)
            {
                var expected = signatures[i];
                var actual = result.Signatures[i];

                Assert.Equal(expected, actual.Label);
            }
        }

        private class SignatureHelpLanguageServer : ClientNotifierServiceBase
        {
            private readonly CSharpTestLspServer _csharpServer;
            private readonly Uri _csharpDocumentUri;

            public SignatureHelpLanguageServer(CSharpTestLspServer csharpServer, Uri csharpDocumentUri)
            {
                _csharpServer = csharpServer;
                _csharpDocumentUri = csharpDocumentUri;
            }

            public override OmniSharp.Extensions.LanguageServer.Protocol.Models.InitializeParams ClientSettings { get; }

            public override Task OnStarted(ILanguageServer server, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public override Task<IResponseRouterReturns> SendRequestAsync(string method)
            {
                throw new NotImplementedException();
            }

            public async override Task<IResponseRouterReturns> SendRequestAsync<T>(string method, T @params)
            {
                Assert.Equal(RazorLanguageServerCustomMessageTargets.RazorSignatureHelpEndpointName, method);
                var signatureHelpParams = Assert.IsType<DelegatedPositionParams>(@params);

                var signatureHelpRequest = new SignatureHelpParams()
                {
                    TextDocument = new TextDocumentIdentifier()
                    {
                        Uri = _csharpDocumentUri
                    },
                    Position = signatureHelpParams.ProjectedPosition,
                };

                var result = await _csharpServer.ExecuteRequestAsync<SignatureHelpParams, LS.SignatureHelp>(Methods.TextDocumentSignatureHelpName, signatureHelpRequest, CancellationToken.None);

                return new TestResponseRouterReturn(result);
            }
        }
    }
}
