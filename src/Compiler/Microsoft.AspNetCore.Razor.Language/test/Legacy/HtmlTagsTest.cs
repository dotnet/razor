﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

public class HtmlTagsTest : ParserTestBase
{
    private static readonly string[] VoidElementNames = new[]
    {
            "area",
            "base",
            "br",
            "col",
            "command",
            "embed",
            "hr",
            "img",
            "input",
            "keygen",
            "link",
            "meta",
            "param",
            "source",
            "track",
            "wbr",
        };

    [Fact]
    public void EmptyTagNestsLikeNormalTag()
    {
        ParseDocumentTest("@{<p></> Bar}");
    }

    [Fact]
    public void EmptyTag()
    {
        // This can happen in situations where a user is in VS' HTML editor and they're modifying
        // the contents of an HTML tag.
        ParseDocumentTest("@{<></> Bar}");
    }

    [Fact]
    public void CommentTag()
    {
        ParseDocumentTest("@{<!--Foo--> Bar}");
    }

    [Fact]
    public void DocTypeTag()
    {
        ParseDocumentTest("@{<!DOCTYPE html> foo}");
    }

    [Fact]
    public void ProcessingInstructionTag()
    {
        ParseDocumentTest("@{<?xml version=\"1.0\" ?> foo}");
    }

    [Fact]
    public void ElementTags()
    {
        ParseDocumentTest("@{<p>Foo</p> Bar}");
    }

    [Fact]
    public void TextTags()
    {
        ParseDocumentTest("@{<text>Foo</text>}");
    }

    [Fact]
    public void CDataTag()
    {
        ParseDocumentTest("@{<![CDATA[Foo]]> Bar}");
    }

    [Fact]
    public void ScriptTag()
    {
        ParseDocumentTest("<script>foo < bar && quantity.toString() !== orderQty.val()</script>");
    }

    [Fact]
    public void ScriptTag_WithNestedMalformedTag()
    {
        ParseDocumentTest("<script>var four = 4; /* </ */</script>");
    }

    [Fact]
    public void ScriptTag_WithNestedEndTag()
    {
        ParseDocumentTest("<script></p></script>");
    }

    [Fact]
    public void ScriptTag_WithNestedBeginTag()
    {
        ParseDocumentTest("<script><p></script>");
    }

    [Fact]
    public void ScriptTag_WithNestedTag()
    {
        ParseDocumentTest("<script><p></p></script>");
    }

    [Fact]
    public void ScriptTag_Incomplete()
    {
        ParseDocumentTest("<script type=");
    }

    [Fact]
    public void ScriptTag_Invalid()
    {
        ParseDocumentTest("@{ <script></script @ > }");
    }

    [Fact]
    public void VoidElementFollowedByContent()
    {
        // Arrange
        var content = new StringBuilder();
        foreach (var tagName in VoidElementNames)
        {
            content.AppendLine("@{");
            content.AppendLine("<" + tagName + ">var x = true;");
            content.AppendLine("}");
        }

        // Act & Assert
        ParseDocumentTest(content.ToString());
    }

    [Fact]
    public void VoidElementFollowedByOtherTag()
    {
        // Arrange
        var content = new StringBuilder();
        foreach (var tagName in VoidElementNames)
        {
            content.AppendLine(@"{");
            content.AppendLine("<" + tagName + "><other> var x = true;");
            content.AppendLine("}");
        }

        // Act & Assert
        ParseDocumentTest(content.ToString());
    }

    [Fact]
    public void VoidElementFollowedByCloseTag()
    {
        // Arrange
        var content = new StringBuilder();
        foreach (var tagName in VoidElementNames)
        {
            content.AppendLine("@{");
            content.AppendLine("<" + tagName + "> </" + tagName + ">var x = true;");
            content.AppendLine("}");
        }

        // Act & Assert
        ParseDocumentTest(content.ToString());
    }

    [Fact]
    public void IncompleteVoidElementEndTag()
    {
        // Arrange
        var content = new StringBuilder();
        foreach (var tagName in VoidElementNames)
        {
            content.AppendLine("@{");
            content.AppendLine("<" + tagName + "></" + tagName);
            content.AppendLine("}");
        }

        // Act & Assert
        ParseDocumentTest(content.ToString());
    }
}
