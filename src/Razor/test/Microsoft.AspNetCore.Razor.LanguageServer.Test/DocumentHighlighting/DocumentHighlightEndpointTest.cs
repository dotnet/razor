// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
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

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentHighlighting.Test
{
    [UseExportProvider]
    public class DocumentHighlightEndpointTest : LanguageServerTestBase
    {
        [Fact]
        public async Task Handle_SingleServer_Local()
        {
            var input = """
                <div></div>

                @{
                    var [|$$myVariable|] = "Hello";

                    var length = [|myVariable|].Length;
                }
                """;

            await VerifyHighlightingRangesAsync(input);
        }

        [Fact]
        public async Task Handle_SingleServer_Method()
        {
            var input = """
                <div></div>

                @functions
                {
                    void [|Method|]()
                    {
                        [|$$Method|]();
                    }
                }
                """;

            await VerifyHighlightingRangesAsync(input);
        }

        [Fact]
        public async Task Handle_SingleServer_AttributeToField()
        {
            var input = """
                <div>
                    <div class="@[|$$_className|]">
                    </div>
                </div>

                @functions
                {
                    private string [|_className|] = "hello";
                }
                """;

            await VerifyHighlightingRangesAsync(input);
        }

        [Fact]
        public async Task Handle_SingleServer_FieldToAttribute()
        {
            var input = """
                <div>
                    <div class="@[|_className|]">
                    </div>
                </div>

                @functions
                {
                    private string [|$$_className|] = "hello";
                }
                """;

            await VerifyHighlightingRangesAsync(input);
        }

        private async Task VerifyHighlightingRangesAsync(string input)
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
            var languageServer = new DocumentHighlightServer(csharpServer, csharpDocumentUri);
            var documentMappingService = new DefaultRazorDocumentMappingService(languageServerFeatureOptions, documentContextFactory, LoggerFactory);

            var endpoint = new DocumentHighlightEndpoint(documentContextFactory, languageServerFeatureOptions, documentMappingService, languageServer, TestLoggerFactory.Instance);

            codeDocument.GetSourceText().GetLineAndOffset(cursorPosition, out var line, out var offset);
            var request = new DocumentHighlightParamsBridge
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
            var sourceText = codeDocument.GetSourceText();
            var expected = spans.Select(s => s.AsRange(sourceText)).OrderBy(s => s.Start.Line).ThenBy(s => s.Start.Character).ToArray();
            var actual = result.Select(r => r.Range).OrderBy(s => s.Start.Line).ThenBy(s => s.Start.Character).ToArray();
            Assert.Equal(actual, expected);
        }

        private class DocumentHighlightServer : ClientNotifierServiceBase
        {
            private readonly CSharpTestLspServer _csharpServer;
            private readonly Uri _csharpDocumentUri;

            public DocumentHighlightServer(CSharpTestLspServer csharpServer, Uri csharpDocumentUri)
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
                Assert.Equal(RazorLanguageServerCustomMessageTargets.RazorDocumentHighlightEndpointName, method);
                var highlightParams = Assert.IsType<DelegatedPositionParams>(@params);

                var highlightRequest = new DocumentHighlightParams()
                {
                    TextDocument = new TextDocumentIdentifier()
                    {
                        Uri = _csharpDocumentUri
                    },
                    Position = highlightParams.ProjectedPosition,
                };

                var result = await _csharpServer.ExecuteRequestAsync<DocumentHighlightParams, DocumentHighlight[]>(Methods.TextDocumentDocumentHighlightName, highlightRequest, CancellationToken.None);

                return new TestResponseRouterReturn(result);
            }
        }
    }
}
