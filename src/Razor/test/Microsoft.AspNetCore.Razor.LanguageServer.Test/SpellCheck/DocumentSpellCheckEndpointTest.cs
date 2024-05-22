﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.SpellCheck;

public class DocumentSpellCheckEndpointTest(ITestOutputHelper testOutput) : SingleServerDelegatingEndpointTestBase(testOutput)
{
    [Fact]
    public async Task Handle_Attributes()
    {
        var input = $$"""
                <SurveyPrompt Title="[|Hello|][| there|]" />
                <SurveyPrompt @bind-Title="InputValue" />

                <form @onsubmit="DoSubmit" required></form>

                <input type="[|checkbox|]" checked></input>

                @code
                {
                    private string? [|InputValue|] { get; set; }
                }
                """;

        // Need to put this in the right namespace, to match the tag helper defined in our test json
        var surveyPrompt = """
                @namespace BlazorApp1.Shared

                <div></div>

                @code
                {
                    [Parameter]
                    public string Title { get; set; }
                }
                """;

        await ValidateSpellCheckRangesAsync(input, filePath: "file.razor", [("SurveyPrompt.razor", surveyPrompt)]);
    }

    [Fact]
    public async Task Handle_NoCSharp()
    {
        var input = """
            <div>[|

                Eat more chickin.

            |]</div>
            """;

        await ValidateSpellCheckRangesAsync(input);
    }

    [Fact]
    public async Task Handle_NoRazor()
    {
        var input = """
            @functions
            {
                void [|M|]()
                {
                    [|// Eat more chickin|]
                }
            }
            """;

        await ValidateSpellCheckRangesAsync(input);
    }

    [Fact]
    public async Task Handle_OnlyWhitespace()
    {
        var input = """


            """;

        await ValidateSpellCheckRangesAsync(input);
    }

    [Fact]
    public async Task Handle_EmptyComment()
    {
        var input = """

            @**@

            """;

        await ValidateSpellCheckRangesAsync(input);
    }

    [Fact]
    public async Task Handle_OnlyNonSpellChecked()
    {
        var input = """

            <script>
                // no spell checking of script tags
            </script>
            
            <style>
                // no spell checking of style tags
            </style>
            
            """;

        await ValidateSpellCheckRangesAsync(input);
    }

    [Fact]
    public async Task Handle()
    {
        var input = """
            @page [|"this is charp"|]

            <div>[|

                Eat more chickin.

            |]</div>

            <script>
                // no spell checking of script tags
                @([|"unless they contain csharp"|])
            </script>

            <style>
                // no spell checking of style tags
                @([|"unless they contain csharp"|])
            </style>

            @{ var [|x|] = [|"csharp"|];

            @*[| Eat more chickin. |]*@

            <div class="[|fush|]" />

            @functions
            {
                void [|M|]()
                {
                    [|// Eat more chickin|]
                }
            }
            """;

        await ValidateSpellCheckRangesAsync(input);
    }

    private async Task ValidateSpellCheckRangesAsync(string originalInput, string? filePath = null, IEnumerable<(string filePath, string contents)>? additionalRazorDocuments = null)
    {
        TestFileMarkupParser.GetSpans(originalInput, out var testInput, out ImmutableArray<TextSpan> spans);

        var codeDocument = CreateCodeDocument(testInput, filePath: filePath);
        var sourceText = codeDocument.GetSourceText();
        var razorFilePath = "file://C:/path/test.razor";
        var uri = new Uri(razorFilePath);
        var languageServer = await CreateLanguageServerAsync(codeDocument, razorFilePath, additionalRazorDocuments);
        var documentContext = CreateDocumentContext(uri, codeDocument);
        var requestContext = new RazorRequestContext(documentContext, null!, "lsp/method", uri: null);

        var endpoint = new DocumentSpellCheckEndpoint(DocumentMappingService, LanguageServerFeatureOptions, languageServer);

        var request = new VSInternalDocumentSpellCheckableParams
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri }
        };

        var response = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        var ranges = response?.First().Ranges;
        Assert.NotNull(ranges);

        // To make for easier test failure analysis, we convert the ranges back to the test input, so we can show a diff
        // rather than "Expected 23, got 53" and leave the developer to deal with what that means.
        // As a bonus, this also ensures the ranges array has the right number of elements (ie, multiple of 3)
        var absoluteRanges = new List<(int Start, int End)>();
        var absoluteStart = 0;
        for (var i = 0; i < ranges.Length; i += 3)
        {
            var kind = ranges[i];
            var start = ranges[i + 1];
            var length = ranges[i + 2];

            absoluteStart += start;
            absoluteRanges.Add((absoluteStart, absoluteStart + length));
            absoluteStart += length;
        }

        // Make sure the response is sorted correctly, or the IDE will complain
        Assert.True(absoluteRanges.SequenceEqual(absoluteRanges.OrderBy(r => r.Start)), "Results are not in order!");

        absoluteRanges.Reverse();

        var actual = testInput;
        foreach (var (start, end) in absoluteRanges)
        {
            actual = actual.Insert(end, "|]").Insert(start, "[|");
        }

        AssertEx.EqualOrDiff(originalInput, actual);
    }
}
