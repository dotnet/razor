// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost.CodeActions;

public class ExtractToCssTests(ITestOutputHelper testOutputHelper) : CohostCodeActionsEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task ExtractToCss()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <sty[||]le>
                    body {
                        background-color: red;
                    }
                </style>

                @code
                {
                    private int x = 1;
                }
                """,
            expected: """
                <div></div>



                @code
                {
                    private int x = 1;
                }
                """,
            codeActionName: LanguageServerConstants.CodeActions.ExtractToCssAction,
            additionalExpectedFiles: [
                (FileUri("File1.razor.css"), $$$"""
                    body {
                            background-color: red;
                        }
                    """)]);
    }

    [Fact]
    public async Task ExtractToCss_NotWithCSharp()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <sty[||]le>
                    body {
                        background-color: @red;
                    }
                </style>

                @code
                {
                    private int x = 1;
                }
                """,
            expected: null,
            codeActionName: LanguageServerConstants.CodeActions.ExtractToCssAction);
    }

    [Fact]
    public async Task ExtractToCss_ExistingFile()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <sty[||]le>
                    body {
                        background-color: red;
                    }
                </style>

                @code
                {
                    private int x = 1;
                }
                """,
            expected: """
                <div></div>



                @code
                {
                    private int x = 1;
                }
                """,
            codeActionName: LanguageServerConstants.CodeActions.ExtractToCssAction,
            additionalFiles: [
                (FilePath("File1.razor.css"), $$$"""
                    h1 {
                            color: blue;
                        }
                    """)],
            additionalExpectedFiles: [
                (FileUri("File1.razor.css"), $$$"""
                    h1 {
                            color: blue;
                        }

                    body {
                            background-color: red;
                        }
                    """)]);
    }

    [Fact]
    public async Task ExtractToCss_ExistingFile_LastLineEmpty()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <sty[||]le>
                    body {
                        background-color: red;
                    }
                </style>

                @code
                {
                    private int x = 1;
                }
                """,
            expected: """
                <div></div>



                @code
                {
                    private int x = 1;
                }
                """,
            codeActionName: LanguageServerConstants.CodeActions.ExtractToCssAction,
            additionalFiles: [
                (FilePath("File1.razor.css"), $$$"""
                    h1 {
                        color: blue;
                    }

                    """)],
            additionalExpectedFiles: [
                (FileUri("File1.razor.css"), $$$"""
                    h1 {
                        color: blue;
                    }


                    body {
                            background-color: red;
                        }
                    """)]);
    }

    [Fact]
    public async Task ExtractToCss_ExistingFile_LastLineEmpty_LF()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <sty[||]le>
                    body {
                        background-color: red;
                    }
                </style>

                @code
                {
                    private int x = 1;
                }
                """,
            expected: """
                <div></div>



                @code
                {
                    private int x = 1;
                }
                """,
            codeActionName: LanguageServerConstants.CodeActions.ExtractToCssAction,
            additionalFiles: [
                (FilePath("File1.razor.css"), $$$"""
                    h1 {
                        color: blue;
                    }

                    """.Replace("\r\n", "\n"))],
            additionalExpectedFiles: [
                // These tests only run on Windows, so Environment.NewLine in the resolver, and in these tests,
                // will always be "\r\n". We have to jump through some hoops to readably represent the resulting output.
                // Once these tests can run on other platforms too, we can remove this test entirely.
                (FileUri("File1.razor.css"), $$$"""
                    h1 {
                        color: blue;
                    }
                    CRLF
                    CRLF
                    body {CRLF
                            background-color: red;CRLF
                        }
                    """.Replace("\r\n", "\n").Replace("CRLF\n", "\r\n"))]);
    }

    [Fact]
    public async Task ExtractToCss_ExistingFile_Empty()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <sty[||]le>
                    body {
                        background-color: red;
                    }
                </style>

                @code
                {
                    private int x = 1;
                }
                """,
            expected: """
                <div></div>



                @code
                {
                    private int x = 1;
                }
                """,
            codeActionName: LanguageServerConstants.CodeActions.ExtractToCssAction,
            additionalFiles: [
                (FilePath("File1.razor.css"), "")],
            additionalExpectedFiles: [
                (FileUri("File1.razor.css"), $$$"""
                    body {
                            background-color: red;
                        }
                    """)]);
    }

    [Theory]
    [InlineData("[||]<style>", "</style>")]
    [InlineData("<[||]style>", "</style>")]
    [InlineData("<s[||]tyle>", "</style>")]
    [InlineData("<st[||]yle>", "</style>")]
    [InlineData("<sty[||]le>", "</style>")]
    [InlineData("<styl[||]e>", "</style>")]
    [InlineData("<style[||]>", "</style>")]
    [InlineData("<style>[||]", "</style>")]
    [InlineData("<style>", "[||]</style>")]
    [InlineData("<style>", "<[||]/style>")]
    [InlineData("<style>", "</[||]style>")]
    [InlineData("<style>", "</s[||]tyle>")]
    [InlineData("<style>", "</st[||]yle>")]
    [InlineData("<style>", "</sty[||]le>")]
    [InlineData("<style>", "</styl[||]e>")]
    [InlineData("<style>", "</style[||]>")]
    [InlineData("<style>", "</style>[||]")]
    public async Task WorkAtAnyCursorPosition(string startTag, string endTag)
    {
        await VerifyCodeActionAsync(
            input: $$"""
                <div></div>
                
                {{startTag}}
                    body {
                        background-color: red;
                    }
                {{endTag}}
                
                @code
                {
                    private int x = 1;
                }
                """,
            expected: """
                <div></div>



                @code
                {
                    private int x = 1;
                }
                """,
            codeActionName: LanguageServerConstants.CodeActions.ExtractToCssAction,
            additionalExpectedFiles: [
                (FileUri("File1.razor.css"), $$"""
                    body {
                            background-color: red;
                        }
                    """)]);
    }
}
