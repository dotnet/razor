// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;
using RoslynHover = Roslyn.LanguageServer.Protocol.Hover;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

using static HoverAssertions;

public class CohostHoverEndpointTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task Razor()
    {
        TestCode code = """
            <[|PageTi$$tle|]></PageTitle>
            <div></div>
            
            @{
                var myVariable = "Hello";
            
                var length = myVariable.Length;
            }
            """;

        await VerifyHoverAsync(code, async (hover, document) =>
        {
            await hover.VerifyRangeAsync(code.Span, document);

            hover.VerifyRawContent(
                Container(
                    Container(
                        Image,
                        ClassifiedText(
                            Text("Microsoft"),
                            Punctuation("."),
                            Text("AspNetCore"),
                            Punctuation("."),
                            Text("Components"),
                            Punctuation("."),
                            Text("Web"),
                            Punctuation("."),
                            Type("PageTitle")))));
        });
    }

    [Fact]
    public async Task Html()
    {
        TestCode code = """
            <PageTitle></PageTitle>
            <div$$></div>
            
            @{
                var myVariable = "Hello";
            
                var length = myVariable.Length;
            }
            """;

        var htmlResponse = new VSInternalHover();

        await VerifyHoverAsync(code, htmlResponse, h => Assert.Same(htmlResponse, h));
    }

    [Fact]
    public async Task CSharp()
    {
        TestCode code = """
            <PageTitle></PageTitle>
            <div></div>

            @{
                var $$[|myVariable|] = "Hello";

                var length = myVariable.Length;
            }
            """;

        await VerifyHoverAsync(code, async (hover, document) =>
        {
            await hover.VerifyRangeAsync(code.Span, document);

            hover.VerifyRawContent(
                Container(
                    Container(
                        Image,
                        ClassifiedText(
                            Punctuation("("),
                            Text("local variable"),
                            Punctuation(")"),
                            WhiteSpace(" "),
                            Keyword("string"),
                            WhiteSpace(" "),
                            LocalName("myVariable")))));
        });
    }

    private async Task VerifyHoverAsync(TestCode input, Func<RoslynHover, TextDocument, Task> verifyHover)
    {
        var document = await CreateProjectAndRazorDocumentAsync(input.Text);
        var result = await GetHoverResultAsync(document, input);

        Assert.NotNull(result);
        var value = result.GetValueOrDefault();

        Assert.True(value.TryGetFirst(out var hover));
        await verifyHover(hover, document);
    }

    private async Task VerifyHoverAsync(TestCode input, Hover htmlResponse, Action<Hover?> verifyHover)
    {
        var document = await CreateProjectAndRazorDocumentAsync(input.Text);
        var result = await GetHoverResultAsync(document, input, htmlResponse);

        Assert.NotNull(result);
        var value = result.GetValueOrDefault();

        Assert.True(value.TryGetSecond(out var hover));
        verifyHover(hover);
    }

    private async Task<SumType<RoslynHover, Hover>?> GetHoverResultAsync(TextDocument document, TestCode input, Hover? htmlResponse = null)
    {
        var inputText = await document.GetTextAsync(DisposalToken);
        var linePosition = inputText.GetLinePosition(input.Position);

        var requestInvoker = new TestLSPRequestInvoker([(Methods.TextDocumentHoverName, htmlResponse)]);
        var endpoint = new CohostHoverEndpoint(RemoteServiceInvoker, TestHtmlDocumentSynchronizer.Instance, requestInvoker);

        var textDocumentPositionParams = new TextDocumentPositionParams
        {
            Position = VsLspFactory.CreatePosition(linePosition),
            TextDocument = new TextDocumentIdentifier { Uri = document.CreateUri() },
        };

        return await endpoint.GetTestAccessor().HandleRequestAsync(textDocumentPositionParams, document, DisposalToken);
    }
}
