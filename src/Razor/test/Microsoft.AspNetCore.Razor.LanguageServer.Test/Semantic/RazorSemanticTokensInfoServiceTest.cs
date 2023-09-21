// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Mef;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic;

// Sets the FileName static variable.
// Finds the test method name using reflection, and uses
// that to find the expected input/output test files as Embedded resources.
[IntializeTestFile]
[UseExportProvider]
public class RazorSemanticTokensInfoServiceTest : SemanticTokenTestBase
{
    public RazorSemanticTokensInfoServiceTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharp_RazorIfNotReady(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                <p></p>@{
                    var d = "t";
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = new ProvideSemanticTokensResponse(tokens: Array.Empty<int>(), hostDocumentSyncVersion: 1);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens, documentVersion: 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharpBlock_HTML(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @{
                    var d = "t";
                    <p>HTML @d</p>
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharp_Nested_HTML(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <!--@{var d = "string";@<a></a>}-->
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharp_VSCodeWorks(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                @{ var d = }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = new ProvideSemanticTokensResponse(tokens: Array.Empty<int>(), hostDocumentSyncVersion: 1);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens, documentVersion: 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharp_Explicit(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @using System
                @addTagHelper *, TestAssembly
                @(DateTime.Now)
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharp_Implicit(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                @{ var d = "txt";}
                @d
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharp_VersionMismatch(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                @{ var d = }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens, documentVersion: 21);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharp_FunctionAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                @{ var d = }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharp_StaticModifier(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @code
                {
                    static int x = 1;
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_MultipleBlankLines(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly

                <p>first
                second</p>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_IncompleteTag(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                <str class='
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.Empty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_MinimizedHTMLAttribute(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                <p attr />
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.Empty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_MinimizedHTMLAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText = """
                @addTagHelper *, TestAssembly
                <input/>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_HTMLCommentAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <!-- comment with comma's -->
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_PartialHTMLCommentAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <!-- comment
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_HTMLIncludesBang(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <!input/>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_HalfOfCommentAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                @* comment
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_NoAttributesAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <test1></test1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_WithAttributeAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <test1 bool-val='true'></test1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_MinimizedAttribute_BoundAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <test1 bool-val></test1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_MinimizedAttribute_NotBoundAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <test1 notbound></test1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_IgnoresNonTagHelperAttributesAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <test1 bool-val='true' class='display:none'></test1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_TagHelpersNotAvailableInRazorAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <test1 bool-val='true' class='display:none'></test1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_DoesNotApplyOnNonTagHelpersAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <p bool-val='true'></p>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_MinimizedDirectiveAttributeParameters(bool usePreciseSemanticTokenRanges)
    {
        // Capitalized, non-well-known-HTML elements should not be marked as TagHelpers
        var documentText =
            """
                @addTagHelper *, TestAssembly
                }<NotATagHelp @minimized:something />
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_ComponentAttributeAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <Component1 bool-val=""true""></Component1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_ComponentAttribute_DoesntGetABackground(bool usePreciseSemanticTokenRanges)
    {
        // Need C# around the component for the C# range to be valid, to correctly validate the attribute handling
        var documentText =
            """
                @DateTime.Now

                <Component1 Title=""Hi there I'm a string""></Component1>

                @DateTime.Now
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens, withCSharpBackground: true);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_DirectiveAttributesParametersAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <Component1 @test:something='Function'></Component1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_NonComponentsDoNotShowInRazorAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <test1 bool-val='true'></test1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_DirectivesAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @addTagHelper *, TestAssembly
                <Component1 @test='Function'></Component1>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_HandleTransitionEscape(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @@text
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.Empty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_DoNotColorNonTagHelpersAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                <p @test='Function'></p>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.Empty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_DoesNotApplyOnNonTagHelpersAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText = """
                @addTagHelpers *, TestAssembly
                <p></p>
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_CodeDirectiveAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @code {}
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.Empty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_CodeDirectiveBodyAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText = """
                @using System
                @code {
                    public void SomeMethod()
                    {
                        @DateTime.Now
                    }
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_UsingDirective(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @using System.Threading
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_FunctionsDirectiveAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @functions {}
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.Empty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_NestedTextDirectives(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @using System
                @functions {
                    private void BidsByShipment(string generatedId, int bids)
                    {
                        if (bids > 0)
                        {
                            <a class=""Thing"">
                                @if(bids > 0)
                                {
                                    <text>@DateTime.Now</text>
                                }
                            </a>
                        }
                    }
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_NestedTransitions(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @using System
                @functions {
                    Action<object> abc = @<span></span>;
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_CommentAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText = """
                @* A comment *@
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.Empty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_MultiLineCommentMidlineAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                <a />@* kdl
                skd
                slf*@
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.Empty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_MultiLineCommentWithBlankLines(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @* kdl

                skd
                    
                        sdfasdfasdf
                slf*@
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.Empty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [WorkItem("https://github.com/dotnet/razor/issues/8176")]
    public async Task GetSemanticTokens_Razor_MultiLineCommentWithBlankLines_LF(bool usePreciseSemanticTokenRanges)
    {
        var documentText = "@* kdl\n\nskd\n    \n        sdfasdfasdf\nslf*@";

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.Empty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_Razor_MultiLineCommentAsync(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @*stuff
                things *@
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: false);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: false, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.Empty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharp_Static(bool usePreciseSemanticTokenRanges)
    {
        var documentText = """
                @using System
                @code
                {
                    private static bool _isStatic;

                    public void M()
                    {
                        if (_isStatic)
                        {
                        }
                    }
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharp_LargeFile(bool usePreciseSemanticTokenRanges)
    {
        var start = """
                @page
                @model SampleApp.Pages.ErrorModel
                @using System

                <!DOCTYPE html>
                <html lang="en">

                <head>
                    <meta charset="utf-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
                    <title>Error</title>
                    <link href="~/css/bootstrap/bootstrap.min.css" rel="stylesheet" />
                    <link href="~/css/site.css" rel="stylesheet" asp-append-version="true" />
                </head>

                <body>
                """;
        var middle = """
                    <div class="@cssClass">
                        <div class="content px-4">
                            @using System
                            <h1 class="text-danger">Error.</h1>
                            <h2 class="text-danger">An error occurred while processing your request.</h2>

                            @if (Model.ShowRequestId)
                            {
                                <p>
                                    <strong>Request ID:</strong> <code>@Model.RequestId</code>
                                </p>
                            }

                            <h3>Development Mode</h3>
                            @if (true)
                            {
                                <p>
                                    Swapping to the <strong>@DateTime.Now</strong> environment displays detailed information about the error that occurred.
                                </p>
                            }
                            <p>
                                @if (false)
                                {
                                    <strong>The Development environment shouldn't be enabled for deployed applications.</strong>
                                }
                                It can result in displaying sensitive information from exceptions to end users.
                                @if (true)
                                {
                                    <text>For local debugging, enable the <strong>@Environment.NewLine</strong> environment by setting the <strong>ASPNETCORE_ENVIRONMENT</strong> environment variable to <strong>Development</strong>
                                    and restarting the app.</text>
                                }
                            </p>
                        </div>
                    </div>
                """;
        var end = """
                </body>

                </html>
                """;

        var builder = new StringBuilder();
        builder.AppendLine(start);
        for (var i = 0; i < 100; i++)
        {
            builder.AppendLine(middle);
        }

        builder.AppendLine(end);

        var documentText = builder.ToString();
        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharp_Static_WithBackground(bool usePreciseSemanticTokenRanges)
    {
        var documentText = """
                @using System
                @code
                {
                    private static bool
                        _isStatic;

                    public void M()
                    {
                        if (_isStatic)
                        {
                        }
                    }
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens, withCSharpBackground: true);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharp_Tabs_Static_WithBackground(bool usePreciseSemanticTokenRanges)
    {
        var documentText = """
                @using System
                @code
                {
                	private static bool
                		_isStatic;

                	public void M()
                	{
                		if (_isStatic)
                		{
                		}
                	}
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens, withCSharpBackground: true);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharp_WithBackground(bool usePreciseSemanticTokenRanges)
    {
        var documentText = """
                @using System
                @code
                {
                    private static bool _isStatic;

                    public void M()
                    {
                        if (_isStatic)
                        {
                        }
                    }
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens, withCSharpBackground: true);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharp_WitRenderFragment(bool usePreciseSemanticTokenRanges)
    {
        var documentText = """
                <div>This is some HTML</div>
                @code
                {
                    public void M()
                    {
                        RenderFragment x = @<div>This is some HTML</div>;
                    }
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharp_WitRenderFragmentAndBackground(bool usePreciseSemanticTokenRanges)
    {
        var documentText = """
                <div>This is some HTML</div>
                @code
                {
                    public void M()
                    {
                        RenderFragment x = @<div>This is some HTML</div>;
                    }
                }
                """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens, withCSharpBackground: true);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetSemanticTokens_CSharp_ExplicitStatement_WithBackground(bool usePreciseSemanticTokenRanges)
    {
        var documentText = """
            @DateTime.Now

            @("hello" + "\\n" + "world" + Environment.NewLine + "how are you?")
            """;

        var razorRange = GetRange(documentText);
        var csharpTokens = await GetCSharpSemanticTokensResponseAsync(usePreciseSemanticTokenRanges, documentText, razorRange, isRazorFile: true);
        await AssertSemanticTokensAsync(usePreciseSemanticTokenRanges, documentText, isRazorFile: true, razorRange, csharpTokens: csharpTokens, withCSharpBackground: true);
        Assert.NotNull(csharpTokens.Tokens);
        Assert.NotEmpty(csharpTokens.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetMappedCSharpRanges_MinimalRangeVsSmallDisjointRanges_DisjointRangesAreSmaller(bool usePreciseSemanticTokenRanges)
    {
        var documentText =
            """
                @using System
                @functions {
                    Action<object> abc = @<span></span>;
                }
                """;

        var razorRange = GetRange(documentText);
        var codeDocument = CreateCodeDocument(documentText, isRazorFile: true, DefaultTagHelpers);
        var csharpDocumentUri = new Uri("C:\\TestSolution\\TestProject\\TestDocument.cs");
        var csharpSourceText = codeDocument.GetCSharpSourceText();
        var sourceText = codeDocument.GetSourceText();

        if (usePreciseSemanticTokenRanges)
        {
            var expectedCsharpRangeLengths = new int[] { 12, 27, 3 };
            Assert.True(RazorSemanticTokensInfoService.TryGetSortedCSharpRanges(codeDocument, razorRange, out var csharpRanges));
            Assert.Equal(3, csharpRanges.Length);
            for (var i = 0; i < csharpRanges.Length; i++)
            {
                var csharpRange = csharpRanges[i];
                var textSpan = csharpRange.ToTextSpan(csharpSourceText);
                Assert.Equal(expectedCsharpRangeLengths[i], textSpan.Length);
            }
        }
        else
        {
            var expectedCsharpRangeLength = 970;
            Assert.True(RazorSemanticTokensInfoService.TryGetMinimalCSharpRange(codeDocument, razorRange, out var csharpRange));
            var textSpan = csharpRange.ToTextSpan(csharpSourceText);
            Assert.Equal(expectedCsharpRangeLength, textSpan.Length);
        }
    }

    private async Task AssertSemanticTokensAsync(
        bool usePreciseSemanticTokenRanges,
        string documentText,
        bool isRazorFile,
        Range range,
        IRazorSemanticTokensInfoService? service = null,
        ProvideSemanticTokensResponse? csharpTokens = null,
        int documentVersion = 0,
        bool withCSharpBackground = false)
    {
        await AssertSemanticTokensAsync(
            usePreciseSemanticTokenRanges,
            new DocumentContentVersion[]
            {
                new DocumentContentVersion(documentText, documentVersion)
            },
            isRazorArray: new bool[] { isRazorFile },
            range,
            service,
            csharpTokens,
            documentVersion,
            withCSharpBackground);
    }

    private async Task AssertSemanticTokensAsync(
        bool usePreciseSemanticTokenRanges,
        DocumentContentVersion[] documentTexts,
        bool[] isRazorArray,
        Range range,
        IRazorSemanticTokensInfoService? service,
        ProvideSemanticTokensResponse? csharpTokens,
        int documentVersion,
        bool withCSharpBackground)
    {
        // Arrange
        if (csharpTokens is null)
        {
            csharpTokens = new ProvideSemanticTokensResponse(tokens: null, -1);
        }

        var (documentContexts, textDocumentIdentifiers) = CreateDocumentContext(
            documentTexts, isRazorArray, DefaultTagHelpers, documentVersion: documentVersion);

        if (service is null)
        {
            service = await GetDefaultRazorSemanticTokenInfoServiceAsync(
                usePreciseSemanticTokenRanges, documentContexts, csharpTokens, withCSharpBackground);
        }

        var textDocumentIdentifier = textDocumentIdentifiers.Dequeue();
        var documentContext = documentContexts.Peek();
        var correlationId = Guid.Empty;

        // Act
        var tokens = await service.GetSemanticTokensAsync(textDocumentIdentifier, range, documentContext, TestRazorSemanticTokensLegend.Instance, correlationId, DisposalToken);

        // Assert
        var sourceText = await documentContext.GetSourceTextAsync(DisposalToken);
        AssertSemanticTokensMatchesBaseline(sourceText, tokens?.Data);
    }

    private async Task<IRazorSemanticTokensInfoService> GetDefaultRazorSemanticTokenInfoServiceAsync(
        bool usePreciseSemanticTokenRanges,
        Queue<VersionedDocumentContext> documentSnapshots,
        ProvideSemanticTokensResponse? csharpTokens,
        bool withCSharpBackground)
    {
        var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
        languageServer
            .Setup(l => l.SendRequestAsync<SemanticTokensParams, ProvideSemanticTokensResponse?>(
                CustomMessageNames.RazorProvideSemanticTokensRangeEndpoint,
                It.IsAny<SemanticTokensParams>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(csharpTokens);

        var documentContextFactory = new TestDocumentContextFactory(documentSnapshots);
        var documentMappingService = new RazorDocumentMappingService(FilePathService, documentContextFactory, LoggerFactory);

        var configurationSyncService = new Mock<IConfigurationSyncService>(MockBehavior.Strict);

        var options = RazorLSPOptions.Default with { ColorBackground = withCSharpBackground };
        configurationSyncService
            .Setup(c => c.GetLatestOptionsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<RazorLSPOptions?>(options));

        var optionsMonitorCache = new OptionsCache<RazorLSPOptions>();

        var optionsMonitor = TestRazorLSPOptionsMonitor.Create(
            configurationSyncService.Object,
            optionsMonitorCache);

        await optionsMonitor.UpdateAsync(CancellationToken.None);

        var featureOptions = Mock.Of<LanguageServerFeatureOptions>(options =>
            options.DelegateToCSharpOnDiagnosticPublish == true &&
            options.UsePreciseSemanticTokenRanges == usePreciseSemanticTokenRanges &&
            options.CSharpVirtualDocumentSuffix == ".ide.g.cs" &&
            options.HtmlVirtualDocumentSuffix == "__virtual.html",
            MockBehavior.Strict);

        return new RazorSemanticTokensInfoService(
            languageServer.Object,
            documentMappingService,
            optionsMonitor,
            featureOptions,
            LoggerFactory);
    }

    private static Range GetRange(string text)
    {
        var lines = text.Split(Environment.NewLine);

        var range = new Range
        {
            Start = new Position { Line = 0, Character = 0 },
            End = new Position { Line = lines.Length, Character = 0 }
        };

        return range;
    }

    private class TestInitializeManager : IInitializeManager<InitializeParams, InitializeResult>
    {
        public InitializeParams GetInitializeParams()
        {
            throw new NotImplementedException();
        }

        public InitializeResult GetInitializeResult()
        {
            throw new NotImplementedException();
        }

        public void SetInitializeParams(InitializeParams request)
        {
            throw new NotImplementedException();
        }
    }

    private class TestDocumentContextFactory : DocumentContextFactory
    {
        private readonly Queue<VersionedDocumentContext> _documentContexts;

        public TestDocumentContextFactory(Queue<VersionedDocumentContext> documentContexts)
        {
            _documentContexts = documentContexts;
        }

        protected override DocumentContext? TryCreateCore(Uri documentUri, VSProjectContext? projectContext, bool versioned)
        {
            var document = _documentContexts.Count == 1 ? _documentContexts.Peek() : _documentContexts.Dequeue();

            return document;
        }
    }
}
