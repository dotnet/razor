// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.Razor.LanguageClient.WrapWithTag;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostWrapWithTagEndpointTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task ValidHtmlLocation_ReturnsResult()
    {
        await VerifyWrapWithTagAsync(
            input: """
                <div>
                    [||]
                </div>
                """,
            htmlResponse: new VSInternalWrapWithTagResponse(
                LspFactory.CreateSingleLineRange(start: (0, 0), length: 10),
                [LspFactory.CreateTextEdit(position: (0, 0), "<p></p>")]
            ),
            expected: new VSInternalWrapWithTagResponse(
                LspFactory.CreateSingleLineRange(start: (0, 0), length: 10),
                [LspFactory.CreateTextEdit(position: (0, 0), "<p></p>")]
            ));
    }

    [Fact]
    public async Task CSharpLocation_ReturnsNull()
    {
        await VerifyWrapWithTagAsync(
            input: """
                @code {
                    [||]
                }
                """,
            htmlResponse: null,
            expected: null);
    }

    [Fact]
    public async Task ImplicitExpression_ReturnsResult()
    {
        await VerifyWrapWithTagAsync(
            input: """
                <div>
                    @[||]currentCount
                </div>
                """,
            htmlResponse: new VSInternalWrapWithTagResponse(
                LspFactory.CreateSingleLineRange(start: (1, 4), length: 16),
                [LspFactory.CreateTextEdit(position: (1, 4), "<span>@currentCount</span>")]
            ),
            expected: new VSInternalWrapWithTagResponse(
                LspFactory.CreateSingleLineRange(start: (1, 4), length: 16),
                [LspFactory.CreateTextEdit(position: (1, 4), "<span>@currentCount</span>")]
            ));
    }

    [Fact]
    public async Task HtmlWithTildes_FixesTextEdits()
    {
        await VerifyWrapWithTagAsync(
            input: """
                <div>
                    [||]
                </div>
                """,
            htmlResponse: new VSInternalWrapWithTagResponse(
                LspFactory.CreateSingleLineRange(start: (0, 0), length: 10),
                [LspFactory.CreateTextEdit(position: (0, 0), "~~~<p>~~~~</p>~~~")]
            ),
            expected: new VSInternalWrapWithTagResponse(
                LspFactory.CreateSingleLineRange(start: (0, 0), length: 10),
                [LspFactory.CreateTextEdit(position: (0, 0), "<p></p>")]
            ));
    }

    private async Task VerifyWrapWithTagAsync(string input, VSInternalWrapWithTagResponse? htmlResponse, VSInternalWrapWithTagResponse? expected)
    {
        TestFileMarkupParser.GetSpan(input, out input, out var span);
        var document = CreateProjectAndRazorDocument(input);
        var sourceText = await document.GetTextAsync(DisposalToken);

        var requestInvoker = new TestHtmlRequestInvoker([(LanguageServerConstants.RazorWrapWithTagEndpoint, htmlResponse)]);

        var endpoint = new CohostWrapWithTagEndpoint(RemoteServiceInvoker, FilePathService, requestInvoker);

        var request = new VSInternalWrapWithTagParams(
            sourceText.GetRange(span),
            "div",
            new FormattingOptions(),
            new VersionedTextDocumentIdentifier()
            {
                DocumentUri = new(document.CreateUri())
            });

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(request, document, DisposalToken);

        if (expected is null)
        {
            Assert.Null(result);
        }
        else
        {
            Assert.NotNull(result);
            Assert.Equal(expected.TagRange, result.TagRange);
            Assert.Equal(expected.TextEdits, result.TextEdits);
        }
    }
}