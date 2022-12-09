// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.FindAllReferences
{
    internal class FindReferencesEndpointTest : SingleServerDelegatingEndpointTestBase
    {
        public FindReferencesEndpointTest(ITestOutputHelper testOutput) : base(testOutput)
        {
        }

        private async Task VerifyCSharpFindAllReferencesAsyncAsync(string input)
        {
            // Arrange
            TestFileMarkupParser.GetPositionAndSpans(input, out var output, out int cursorPosition, out ImmutableArray<TextSpan> expectedSpans);

            var codeDocument = CreateCodeDocument(output);
            var razorFilePath = "C:/path/to/file.razor";

            await CreateLanguageServerAsync(codeDocument, razorFilePath);

            var endpoint = new FindAllReferencesEndpoint(
                LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, LoggerFactory, LanguageServerFeatureOptions, TagHelperFactsService, HtmlFactsService);

            codeDocument.GetSourceText().GetLineAndOffset(cursorPosition, out var line, out var offset);
            var request = new TextDocumentPositionParamsBridge
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = new Uri(razorFilePath)
                },
                Position = new Position(line, offset)
            };
            var documentContext = await DocumentContextFactory.TryCreateAsync(request.TextDocument.Uri, DisposalToken);
            var requestContext = CreateRazorRequestContext(documentContext);

            // Act
            var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

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
