﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Implementation
{
    public class ImplementationEndpointTest : SingleServerDelegatingEndpointTestBase
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

            await CreateLanguageServerAsync(codeDocument, razorFilePath).ConfigureAwait(false);

            var endpoint = new ImplementationEndpoint(LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, TestLoggerFactory.Instance);

            codeDocument.GetSourceText().GetLineAndOffset(cursorPosition, out var line, out var offset);
            var request = new TextDocumentPositionParamsBridge
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = new Uri(razorFilePath)
                },
                Position = new Position(line, offset)
            };
            var documentContext = await DocumentContextFactory.TryCreateAsync(request.TextDocument.Uri, CancellationToken.None);
            var requestContext = CreateRazorRequestContext(documentContext);

            // Act
            var result = await endpoint.HandleRequestAsync(request, requestContext, CancellationToken.None);

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
    }
}
