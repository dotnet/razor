﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.TextDifferencing;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Razor.TextDifferencing;

public class SourceTextDifferTest : TestBase
{
    public SourceTextDifferTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    [Theory]
    [InlineData("asdf", ";lkj")]
    [InlineData("asdf", ";asd")]
    [InlineData("", "")]
    [InlineData("", "a")]
    [InlineData("a", "b")]
    [InlineData("a", "a")]
    [InlineData("a", "")]
    [InlineData("aabd", "abc")]
    [InlineData("aabd", "a")]
    [InlineData("aabd", "h")]
    [InlineData("aabd", "trtrt45rtt()")]
    [InlineData("trtrt4 5rtt()", "atbd")]
    [InlineData(@"trtrt4\n5rtt()", "atb\nd")]
    [InlineData(@"Hello\r\nWorld\r\n123", "Hola\r\nWorld\r\n\r\n1234")]
    public void GetMinimalTextChanges_ReturnsAccurateResults(string oldStr, string newStr)
    {
        // Arrange
        var oldText = CreateSourceText(oldStr, fixLineEndings: false);
        var newText = CreateSourceText(newStr, fixLineEndings: false);

        // Act 1
        var characterChanges = SourceTextDiffer.GetMinimalTextChanges(oldText, newText, DiffKind.Char);

        // Assert 1
        var changedText = oldText.WithChanges(characterChanges);
        Assert.Equal(newStr, changedText.ToString());

        // Act 2
        var lineChanges = SourceTextDiffer.GetMinimalTextChanges(oldText, newText, DiffKind.Char);

        // Assert 2
        changedText = oldText.WithChanges(lineChanges);
        Assert.Equal(newStr, changedText.ToString());
    }

    [Fact]
    public void GetMinimalTextChanges_ReturnsExpectedResults()
    {
        // Arrange
        var oldText = CreateSourceText("""
            <div>
              Hello!
            </div>
            """);

        var newText = CreateSourceText("""
            <div>
              Hola!
            </div>
            """);

        // Act 1
        var characterChanges = SourceTextDiffer.GetMinimalTextChanges(oldText, newText, DiffKind.Char);

        // Assert 1
        Assert.Collection(characterChanges,
            change => Assert.Equal(new TextChange(TextSpan.FromBounds(10, 11), "o"), change),
            change => Assert.Equal(new TextChange(TextSpan.FromBounds(12, 14), "a"), change));

        // Act 2
        var lineChanges = SourceTextDiffer.GetMinimalTextChanges(oldText, newText, DiffKind.Line);

        // Assert 2
        var change = Assert.Single(lineChanges);
        Assert.Equal(new TextChange(TextSpan.FromBounds(7, 17), "  Hola!\r\n"), change);
    }

    [Fact]
    public void GetMinimalTextChanges_MultiLineChange_ReturnsExpectedResults()
    {
        // Arrange
        var oldText = CreateSourceText("""
            These
            are
            multiple
            lines
            of
            text
            """);

        var newText = CreateSourceText("""
            THESE
            are
            MULTIPLE
            LINES
            OF
            text
            """);

        // Act 1
        var characterChanges = SourceTextDiffer.GetMinimalTextChanges(oldText, newText, DiffKind.Char);

        // Assert 1
        Assert.Collection(characterChanges,
            change => Assert.Equal(new TextChange(TextSpan.FromBounds(1, 5), "HESE"), change),
            change => Assert.Equal(new TextChange(TextSpan.FromBounds(12, 20), "MULTIPLE"), change),
            change => Assert.Equal(new TextChange(TextSpan.FromBounds(22, 27), "LINES"), change),
            change => Assert.Equal(new TextChange(TextSpan.FromBounds(29, 31), "OF"), change));

        // Act 2
        var lineChanges = SourceTextDiffer.GetMinimalTextChanges(oldText, newText, DiffKind.Line);

        // Assert 2
        Assert.Collection(lineChanges,
            change => Assert.Equal(new TextChange(TextSpan.FromBounds(0, 7), "THESE\r\n"), change),
            change => Assert.Equal(new TextChange(TextSpan.FromBounds(12, 33), "MULTIPLE\r\nLINES\r\nOF\r\n"), change));
    }

    private static SourceText CreateSourceText(string input, bool fixLineEndings = true)
    {
        if (fixLineEndings)
        {
            input = FixLineEndings(input);
        }

        return SourceText.From(input);
    }

    private static string FixLineEndings(string input)
        => input.Replace(Environment.NewLine, "\r\n");
}
