// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Editor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.Razor.LanguageClient.WrapWithTag;
using Roslyn.Test.Utilities;
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
            expected: """
                <div>
                    <p></p>
                </div>
                """,
            htmlResponse: new VSInternalWrapWithTagResponse(
                LspFactory.CreateSingleLineRange(start: (1, 4), length: 0),
                [LspFactory.CreateTextEdit(position: (1, 4), "<p></p>")]
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
            expected: """
                <div>
                    <span>@currentCount</span>
                </div>
                """,
            htmlResponse: new VSInternalWrapWithTagResponse(
                LspFactory.CreateSingleLineRange(start: (1, 5), length: 13),
                [LspFactory.CreateTextEdit(1, 4, 1, 17, "<span>@currentCount</span>")]
            ));
    }

    [Fact]
    public async Task HtmlWithTildes_FixesTextEdits()
    {
        await VerifyWrapWithTagAsync(
            input: """
                <div>
                    @[||]currentCount
                </div>
                """,
            htmlDocument: """
                <div>
                    /*~~~~~~~~~*/
                </div>
                """,
            expected: """
                <div>
                    <span>@currentCount</span>
                </div>
                """,
            htmlResponse: new VSInternalWrapWithTagResponse(
                LspFactory.CreateSingleLineRange(start: (1, 5), length: 13),
                [LspFactory.CreateTextEdit(1, 4, 1, 17, "<span>/*~~~~~~~~~*/</span>")]
            ));
    }

    private async Task VerifyWrapWithTagAsync(TestCode input, string? expected, VSInternalWrapWithTagResponse? htmlResponse, string? htmlDocument = null)
    {
        var document = CreateProjectAndRazorDocument(input.Text);
        var sourceText = await document.GetTextAsync(DisposalToken);

        var requestInvoker = new TestHtmlRequestInvoker([(LanguageServerConstants.RazorWrapWithTagEndpoint, htmlResponse)]);

        var documentUri = document.CreateUri();
        var documentManager = new TestDocumentManager();
        if (htmlDocument is not null)
        {
            var snapshot = new StringTextSnapshot(htmlDocument);
            var htmlSnapshot = new HtmlVirtualDocumentSnapshot(documentUri, snapshot, hostDocumentSyncVersion: 1, state: null);
            var documentSnapshot = new TestLSPDocumentSnapshot(documentUri, version: 1, htmlSnapshot);
            documentManager.AddDocument(documentUri, documentSnapshot);
        }

        var endpoint = new CohostWrapWithTagEndpoint(RemoteServiceInvoker, requestInvoker, documentManager, LoggerFactory);

        var request = new VSInternalWrapWithTagParams(
            sourceText.GetRange(input.Span),
            "div",
            new FormattingOptions(),
            new VersionedTextDocumentIdentifier()
            {
                DocumentUri = new(documentUri)
            });

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(request, document, DisposalToken);

        if (expected is null)
        {
            Assert.Null(result);
        }
        else
        {
            Assert.NotNull(result);

            var changedDoc = sourceText.WithChanges(result.TextEdits.Select(sourceText.GetTextChange));
            AssertEx.EqualOrDiff(expected, changedDoc.ToString());
        }
    }
}
