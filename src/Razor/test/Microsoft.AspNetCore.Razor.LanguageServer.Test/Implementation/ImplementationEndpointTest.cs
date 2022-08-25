// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
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

namespace Microsoft.AspNetCore.Razor.LanguageServer.Implementation
{
    [UseExportProvider]
    public class ImplementationEndpointTest : TagHelperServiceTestBase
    {
        [Fact]
        public async Task Handle_SingleServer_CSharp_Method()
        {
            var input = """
                <div></div>

                @{
                    var x = Ge$$tX();
                }

                @functions
                {
                    void [|GetX|]()
                    {
                    }
                }
                """;

            await VerifyCSharpGoToImplementationAsync(input);
        }

        [Fact]
        public async Task Handle_SingleServer_CSharp_Local()
        {
            var input = """
                <div></div>

                @{
                    var x = GetX();
                }

                @functions
                {
                    private string [|_name|];

                    string GetX()
                    {
                        return _na$$me;
                    }
                }
                """;

            await VerifyCSharpGoToImplementationAsync(input);
        }

        [Fact]
        public async Task Handle_SingleServer_CSharp_MultipleResults()
        {
            var input = """
                <div></div>

                @functions
                {
                    class [|Base|] { }
                    class [|Derived1|] : Base { }
                    class [|Derived2|] : Base { }

                    void M(Ba$$se b)
                    {
                    }
                }
                """;

            await VerifyCSharpGoToImplementationAsync(input);
        }

        private async Task VerifyCSharpGoToImplementationAsync(string input)
        {
            // Arrange
            TestFileMarkupParser.GetPositionAndSpans(input, out var output, out int cursorPosition, out ImmutableArray<TextSpan> expectedSpans);

            var codeDocument = CreateCodeDocument(output);
            var razorFilePath = "C:/path/to/file.razor";

            // Act
            var result = await GetImplementationResultAsync(codeDocument, razorFilePath, cursorPosition);

            // Assert
            Assert.NotNull(result.First);
            var locations = result.First;

            Assert.Equal(expectedSpans.Length, locations.Length);

            var i = 0;
            foreach (var location in locations.OrderBy(l => l.Range.Start.Line))
            {
                Assert.Equal(new Uri(razorFilePath), location.Uri);

                var expectedRange = expectedSpans[i].AsRange(codeDocument.GetSourceText());
                Assert.Equal(expectedRange, location.Range);

                i++;
            }
        }

        private async Task<SumType<Location[], VSInternalReferenceItem[]>> GetImplementationResultAsync(RazorCodeDocument codeDocument, string razorFilePath, int cursorPosition)
        {
            var realLanguageServerFeatureOptions = new DefaultLanguageServerFeatureOptions();

            var csharpSourceText = codeDocument.GetCSharpSourceText();
            var serverCapabilities = new ServerCapabilities()
            {
                ImplementationProvider = true
            };
            var csharpDocumentUri = new Uri(realLanguageServerFeatureOptions.GetRazorCSharpFilePath(razorFilePath));
            var csharpServer = await CSharpTestLspServerHelpers.CreateCSharpLspServerAsync(csharpSourceText, csharpDocumentUri, serverCapabilities, razorSpanMappingService: null).ConfigureAwait(false);
            await csharpServer.OpenDocumentAsync(csharpDocumentUri, csharpSourceText.ToString()).ConfigureAwait(false);

            var documentContextFactory = new TestDocumentContextFactory(razorFilePath, codeDocument, version: 1337);
            var languageServerFeatureOptions = Mock.Of<LanguageServerFeatureOptions>(options =>
                options.SupportsFileManipulation == true &&
                options.SingleServerSupport == true &&
                options.CSharpVirtualDocumentSuffix == realLanguageServerFeatureOptions.CSharpVirtualDocumentSuffix &&
                options.HtmlVirtualDocumentSuffix == realLanguageServerFeatureOptions.HtmlVirtualDocumentSuffix
                , MockBehavior.Strict);
            var languageServer = new ImplementationLanguageServer(csharpServer, csharpDocumentUri);
            var documentMappingService = new DefaultRazorDocumentMappingService(languageServerFeatureOptions, documentContextFactory, LoggerFactory);

            var endpoint = new ImplementationEndpoint(documentContextFactory, languageServerFeatureOptions, documentMappingService, languageServer, TestLoggerFactory.Instance);

            codeDocument.GetSourceText().GetLineAndOffset(cursorPosition, out var line, out var offset);
            var request = new ImplementationParamsBridge
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = new Uri(razorFilePath)
                },
                Position = new Position(line, offset)
            };

            return await endpoint.Handle(request, CancellationToken.None);
        }

        private class ImplementationLanguageServer : ClientNotifierServiceBase
        {
            private readonly CSharpTestLspServer _csharpServer;
            private readonly Uri _csharpDocumentUri;

            public ImplementationLanguageServer(CSharpTestLspServer csharpServer, Uri csharpDocumentUri)
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
                Assert.Equal(RazorLanguageServerCustomMessageTargets.RazorImplementationEndpointName, method);
                var implementationParams = Assert.IsType<DelegatedPositionParams>(@params);

                var implementationRequest = new TextDocumentPositionParams()
                {
                    TextDocument = new TextDocumentIdentifier()
                    {
                        Uri = _csharpDocumentUri
                    },
                    Position = implementationParams.ProjectedPosition
                };

                var result = await _csharpServer.ExecuteRequestAsync<TextDocumentPositionParams, SumType<Location[], VSInternalReferenceItem[]>>(Methods.TextDocumentImplementationName, implementationRequest, CancellationToken.None);

                return new TestResponseRouterReturn(result);
            }
        }
    }
}
