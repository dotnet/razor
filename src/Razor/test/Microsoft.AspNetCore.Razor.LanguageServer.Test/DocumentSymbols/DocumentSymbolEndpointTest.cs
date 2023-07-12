// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentSymbol;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.DocumentSymbols;

public class DocumentSymbolEndpointTest : SingleServerDelegatingEndpointTestBase
{
    public DocumentSymbolEndpointTest(ITestOutputHelper testOutput) : base(testOutput)
    {
    }

    [Fact]
    public Task DocumentSymols_CSharpMethods()
        => VerifyRazorFoldsAsync(
            """
            @functions {
                private void {|HandleString(string s):HandleString|}(string s)
                {
                    s += "Hello";
                }

                private void {|M(int i):M|}(int i)
                {
                    i++;
                }

                private string {|ObjToString(object o):ObjToString|}(object o)
                {
                    return o.ToString();
                }
            }
            
            """);

    private async Task VerifyRazorFoldsAsync(string input)
    {
        TestFileMarkupParser.GetSpans(input, out input, out ImmutableDictionary<string, ImmutableArray<TextSpan>>  spansDict);
        var codeDocument = CreateCodeDocument(input);
        var razorFilePath = "C:/path/to/file.razor";

        await CreateLanguageServerAsync(codeDocument, razorFilePath);

        var endpoint = new DocumentSymbolEndpoint(LanguageServer, DocumentMappingService);

        var request = new DocumentSymbolParams()
        {
            TextDocument = new VSTextDocumentIdentifier
            {
                Uri = new Uri(razorFilePath),
                ProjectContext = new VSProjectContext()
                {
                    Label = "test",
                    Kind = VSProjectKind.CSharp,
                    Id = "test"
                }
            }
        };
        var documentContext = await DocumentContextFactory.TryCreateForOpenDocumentAsync(request.TextDocument.Uri, DisposalToken);
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var symbolsInformations = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(symbolsInformations);
        Assert.Equal(spansDict.Values.Count(), symbolsInformations.Length);

        var sourceText = SourceText.From(input);
        foreach (var symbolInformation in symbolsInformations)
        {
            Assert.True(spansDict.TryGetValue(symbolInformation.Name, out var spans), $"Expected {symbolInformation.Name} to be in test provided markers");
            Assert.Single(spans);
            var expectedRange = spans.Single().AsRange(sourceText);
            Assert.Equal(expectedRange, symbolInformation.Location.Range);
        }
    }
}
