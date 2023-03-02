﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Globalization;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

public class CSharpSectionTest : ParserTestBase
{
    [Fact]
    public void CapturesNewlineImmediatelyFollowing()
    {
        ParseDocumentTest(
            "@section" + Environment.NewLine,
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void CapturesWhitespaceToEndOfLineInSectionStatementMissingOpenBrace()
    {
        ParseDocumentTest(
            "@section Foo         " + Environment.NewLine + "    ",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void CapturesWhitespaceToEndOfLineInSectionStatementMissingName()
    {
        ParseDocumentTest(
            "@section         " + Environment.NewLine + "    ",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void IgnoresSectionUnlessAllLowerCase()
    {
        ParseDocumentTest(
            "@Section foo",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void ReportsErrorAndTerminatesSectionBlockIfKeywordNotFollowedByIdentifierStartChar()
    {
        // ParseSectionBlockReportsErrorAndTerminatesSectionBlockIfKeywordNotFollowedByIdentifierStartCharacter
        ParseDocumentTest(
            "@section 9 { <p>Foo</p> }",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void ReportsErrorAndTerminatesSectionBlockIfNameNotFollowedByOpenBrace()
    {
        // ParseSectionBlockReportsErrorAndTerminatesSectionBlockIfNameNotFollowedByOpenBrace
        ParseDocumentTest(
            "@section foo-bar { <p>Foo</p> }",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void ParserOutputsErrorOnNestedSections()
    {
        ParseDocumentTest(
            "@section foo { @section bar { <p>Foo</p> } }",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void ParserOutputsErrorOnMultipleNestedSections()
    {
        ParseDocumentTest(
            "@section foo { @section bar { <p>Foo</p> @section baz { } } }",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void ParserDoesNotOutputErrorOtherNestedDirectives()
    {
        // This isn't a real scenario but we just want to verify we don't show misleading errors.
        ParseDocumentTest(
            "@section foo { @inherits Bar }",
            new[] { SectionDirective.Directive, InheritsDirective.Directive });
    }

    [Fact]
    public void HandlesEOFAfterOpenBrace()
    {
        ParseDocumentTest(
            "@section foo {",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void HandlesEOFAfterOpenContent1()
    {

        ParseDocumentTest(
            "@section foo { ",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void HandlesEOFAfterOpenContent2()
    {

        ParseDocumentTest(
            "@section foo {\n",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void HandlesEOFAfterOpenContent3()
    {

        ParseDocumentTest(
            "@section foo {abc",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void HandlesEOFAfterOpenContent4()
    {

        ParseDocumentTest(
            "@section foo {\n abc",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void HandlesUnterminatedSection()
    {
        ParseDocumentTest(
            "@section foo { <p>Foo{}</p>",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void HandlesUnterminatedSectionWithNestedIf()
    {
        // Arrange
        var newLine = Environment.NewLine;
        var spaces = "    ";

        // Act & Assert
        ParseDocumentTest(
            string.Format(
                CultureInfo.InvariantCulture,
                "@section Test{0}{{{0}{1}@if(true){0}{1}{{{0}{1}{1}<p>Hello World</p>{0}{1}}}",
                newLine,
                spaces),
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void ReportsErrorAndAcceptsWhitespaceToEOLIfSectionNotFollowedByOpenBrace()
    {
        // ParseSectionBlockReportsErrorAndAcceptsWhitespaceToEndOfLineIfSectionNotFollowedByOpenBrace
        ParseDocumentTest(
            "@section foo      " + Environment.NewLine,
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void AcceptsOpenBraceMultipleLinesBelowSectionName()
    {
        ParseDocumentTest(
            "@section foo      "
            + Environment.NewLine
            + Environment.NewLine
            + Environment.NewLine
            + Environment.NewLine
            + Environment.NewLine
            + Environment.NewLine
            + "{" + Environment.NewLine
            + "<p>Foo</p>" + Environment.NewLine
            + "}",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void ParsesNamedSectionCorrectly()
    {
        ParseDocumentTest(
            "@section foo { <p>Foo</p> }",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void DoesNotRequireSpaceBetweenSectionNameAndOpenBrace()
    {
        ParseDocumentTest(
            "@section foo{ <p>Foo</p> }",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void BalancesBraces()
    {
        ParseDocumentTest(
            "@section foo { <script>(function foo() { return 1; })();</script> }",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void AllowsBracesInCSharpExpression()
    {
        ParseDocumentTest(
            "@section foo { I really want to render a close brace, so here I go: @(\"}\") }",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void SectionIsCorrectlyTerminatedWhenCloseBraceImmediatelyFollowsCodeBlock()
    {
        ParseDocumentTest(
            "@section Foo {" + Environment.NewLine
            + "@if(true) {" + Environment.NewLine
            + "}" + Environment.NewLine
            + "}",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void SectionCorrectlyTerminatedWhenCloseBraceFollowsCodeBlockNoWhitespace()
    {
        // SectionIsCorrectlyTerminatedWhenCloseBraceImmediatelyFollowsCodeBlockNoWhitespace
        ParseDocumentTest(
            "@section Foo {" + Environment.NewLine
            + "@if(true) {" + Environment.NewLine
            + "}}",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void CorrectlyTerminatesWhenCloseBraceImmediatelyFollowsMarkup()
    {
        ParseDocumentTest(
            "@section foo {something}",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void ParsesComment()
    {
        ParseDocumentTest(
            "@section s {<!-- -->}",
            new[] { SectionDirective.Directive });
    }

    // This was a user reported bug (codeplex #710), the section parser wasn't handling
    // comments.
    [Fact]
    public void ParsesCommentWithDelimiters()
    {
        ParseDocumentTest(
            "@section s {<!-- > \" '-->}",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void CommentRecoversFromUnclosedTag()
    {
        ParseDocumentTest(
            "@section s {" + Environment.NewLine + "<a" + Environment.NewLine + "<!--  > \" '-->}",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void ParsesXmlProcessingInstruction()
    {
        ParseDocumentTest(
            "@section s { <? xml bleh ?>}",
            new[] { SectionDirective.Directive });
    }

    [Fact]
    public void _WithDoubleTransition1()
    {
        ParseDocumentTest("@section s {<span foo='@@' />}", new[] { SectionDirective.Directive });
    }

    [Fact]
    public void _WithDoubleTransition2()
    {
        ParseDocumentTest("@section s {<span foo='@DateTime.Now @@' />}", new[] { SectionDirective.Directive });
    }

}
