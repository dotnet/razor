﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.Razor.Language.Syntax.InternalSyntax;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

public class DirectiveHtmlTokenizerTest : HtmlTokenizerTestBase
{
    [Fact]
    public void Next_ReturnsNull_WhenHtmlIsSeen()
    {
        TestTokenizer(
            "\r\n <div>Ignored</div>",
            SyntaxFactory.Token(SyntaxKind.NewLine, "\r\n"),
            SyntaxFactory.Token(SyntaxKind.Whitespace, " "),
            SyntaxFactory.Token(SyntaxKind.OpenAngle, "<"));
    }

    [Fact]
    public void Next_IncludesRazorComments_ReturnsNull_WhenHtmlIsSeen()
    {
        TestTokenizer(
            "\r\n @*included*@ <div>Ignored</div>",
            SyntaxFactory.Token(SyntaxKind.NewLine, "\r\n"),
            SyntaxFactory.Token(SyntaxKind.Whitespace, " "),
            SyntaxFactory.Token(SyntaxKind.RazorCommentTransition, "@"),
            SyntaxFactory.Token(SyntaxKind.RazorCommentStar, "*"),
            SyntaxFactory.Token(SyntaxKind.RazorCommentLiteral, "included"),
            SyntaxFactory.Token(SyntaxKind.RazorCommentStar, "*"),
            SyntaxFactory.Token(SyntaxKind.RazorCommentTransition, "@"),
            SyntaxFactory.Token(SyntaxKind.Whitespace, " "),
            SyntaxFactory.Token(SyntaxKind.OpenAngle, "<"));
    }

    internal override object CreateTokenizer(SeekableTextReader source)
    {
        return new DirectiveHtmlTokenizer(source);
    }
}
