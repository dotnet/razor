// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostDocumentHighlightEndpointTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task Local()
    {
        var input = """
                <div></div>

                @{
                    var $$[|myVariable|] = "Hello";

                    var length = [|myVariable|].Length;
                }
                """;

        await VerifyDocumentHighlightsAsync(input);
    }

    [Fact]
    public async Task Method()
    {
        var input = """
                <div></div>

                @code
                {
                    void [|Method|]()
                    {
                        $$[|Method|]();
                    }
                }
                """;

        await VerifyDocumentHighlightsAsync(input);
    }

    [Fact]
    public async Task AttributeToField()
    {
        var input = """
                <div>
                    <div class="@$$[|_className|]">
                    </div>
                </div>

                @code
                {
                    private string [|_className|] = "hello";
                }
                """;

        await VerifyDocumentHighlightsAsync(input);
    }

    [Fact]
    public async Task FieldToAttribute()
    {
        var input = """
                <div>
                    <div class="@[|_className|]">
                    </div>
                </div>

                @code
                {
                    private string $$[|_className|] = "hello";
                }
                """;

        await VerifyDocumentHighlightsAsync(input);
    }

    [Fact]
    public async Task Html()
    {
        var input = """
                <div>
                    <di$$v class="@_className">
                    </div>
                </div>

                @code
                {
                    private string _className = "hello";
                }
                """;

        await VerifyDocumentHighlightsAsync(input);
    }

    [Fact]
    public async Task Razor()
    {
        var input = """
                @in$$ject IDisposable Disposable

                <div>
                    <div class="@_className">
                    </div>
                </div>

                @code
                {
                    private string _className = "hello";
                }
                """;

        await VerifyDocumentHighlightsAsync(input, expectEmptyArray: true);
    }

    private async Task VerifyDocumentHighlightsAsync(string input, bool expectEmptyArray = false)
    {
        TestFileMarkupParser.GetPositionAndSpans(input, out var source, out int cursorPosition, out ImmutableArray<TextSpan> spans);
        var document = CreateProjectAndRazorDocument(source);
        var inputText = await document.GetTextAsync(DisposalToken);
        inputText.GetLineAndOffset(cursorPosition, out var lineIndex, out var characterIndex);

        var htmlResponse = new[] { new DocumentHighlight() };
        var requestInvoker = new TestLSPRequestInvoker([(Methods.TextDocumentDocumentHighlightName, htmlResponse)]);

        var endpoint = new CohostDocumentHighlightEndpoint(RemoteServiceInvoker, TestHtmlDocumentSynchronizer.Instance, requestInvoker);

        var request = new DocumentHighlightParams()
        {
            TextDocument = new TextDocumentIdentifier() { Uri = document.CreateUri() },
            Position = new Position(lineIndex, characterIndex)
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(request, document, DisposalToken);

        Assert.NotNull(result);

        if (spans.Length == 0)
        {
            if (expectEmptyArray)
            {
                // No spans and expecting an empty array means this result is in a Razor context
                Assert.Empty(result);
            }
            else
            {
                // No spans but not expecting an empty array means we should have gotten a response from the Html server
                // so we just verify we got our fake one
                Assert.Same(htmlResponse, result);
            }

            return;
        }

        var actual = TestFileMarkupParser.CreateTestFile(source, cursorPosition, result.SelectAsArray(h => h.Range.ToTextSpan(inputText)));

        AssertEx.EqualOrDiff(input, actual);
    }
}
