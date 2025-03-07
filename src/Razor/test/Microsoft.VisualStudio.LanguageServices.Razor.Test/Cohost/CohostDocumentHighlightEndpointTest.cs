﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
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

        await VerifyDocumentHighlightsAsync(input, htmlResponse: [new DocumentHighlight()]);
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

        await VerifyDocumentHighlightsAsync(input);
    }

    [Fact]
    public async Task Inject()
    {
        var input = """
                @inject [|IDis$$posable|] Disposable

                <div>
                </div>

                @code
                {
                    [|IDisposable|].Dispose()
                    {
                    }
                }
                """;

        await VerifyDocumentHighlightsAsync(input);
    }

    private async Task VerifyDocumentHighlightsAsync(string input, DocumentHighlight[]? htmlResponse = null)
    {
        TestFileMarkupParser.GetPositionAndSpans(input, out var source, out int cursorPosition, out ImmutableArray<TextSpan> spans);
        var document = CreateProjectAndRazorDocument(source);
        var inputText = await document.GetTextAsync(DisposalToken);
        var position = inputText.GetPosition(cursorPosition);

        var requestInvoker = new TestLSPRequestInvoker([(Methods.TextDocumentDocumentHighlightName, htmlResponse)]);

        var endpoint = new CohostDocumentHighlightEndpoint(RemoteServiceInvoker, TestHtmlDocumentSynchronizer.Instance, requestInvoker);

        var request = new DocumentHighlightParams()
        {
            TextDocument = new TextDocumentIdentifier() { Uri = document.CreateUri() },
            Position = position
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(request, document, DisposalToken);

        if (spans.Length == 0)
        {
            Assert.Same(result, htmlResponse);
            return;
        }

        Assert.NotNull(result);

        var actual = TestFileMarkupParser.CreateTestFile(source, cursorPosition, result.SelectAsArray(h => inputText.GetTextSpan(h.Range)));

        AssertEx.EqualOrDiff(input, actual);
    }
}
