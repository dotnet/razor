﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.Test.Legacy;

public class CodeBlockEditHandlerTest
{
    [Fact]
    public void IsAcceptableReplacement_AcceptableReplacement_ReturnsTrue()
    {
        // Arrange
        var span = GetSpan(SourceLocation.Zero, "Hello {world}.");
        var change = new SourceChange(new SourceSpan(0, 5), "H3ll0");

        // Act
        var result = CodeBlockEditHandler.IsAcceptableReplacement(span, change);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsAcceptableReplacement_AcceptableReplacement_WithMarkup_ReturnsTrue()
    {
        // Arrange
        var span = GetSpan(SourceLocation.Zero, "Hello <div>@world</div>.");
        var change = new SourceChange(new SourceSpan(0, 5), "H3ll0");

        // Act
        var result = CodeBlockEditHandler.IsAcceptableReplacement(span, change);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsAcceptableReplacement_ChangeModifiesInvalidContent_ReturnsFalse()
    {
        // Arrange
        var span = GetSpan(SourceLocation.Zero, "Hello {world}.");
        var change = new SourceChange(new SourceSpan(6, 1), "!");

        // Act
        var result = CodeBlockEditHandler.IsAcceptableReplacement(span, change);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsAcceptableReplacement_ChangeAddsOpenAngle_ReturnsFalse()
    {
        // Arrange
        var span = GetSpan(SourceLocation.Zero, "Hello <div></div>.");
        var change = new SourceChange(new SourceSpan(6, 1), "<");

        // Act
        var result = CodeBlockEditHandler.IsAcceptableReplacement(span, change);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsAcceptableReplacement_ChangeToComment_ReturnsFalse()
    {
        // Arrange
        var span = GetSpan(SourceLocation.Zero, "Hello @");
        var change = new SourceChange(new SourceSpan(6, 1), "@*");

        // Act
        var result = CodeBlockEditHandler.IsAcceptableReplacement(span, change);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsAcceptableReplacement_ChangeContainsInvalidContent_ReturnsFalse()
    {
        // Arrange
        var span = GetSpan(SourceLocation.Zero, "Hello {world}.");
        var change = new SourceChange(new SourceSpan(0, 0), "{");

        // Act
        var result = CodeBlockEditHandler.IsAcceptableReplacement(span, change);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsAcceptableReplacement_NotReplace_ReturnsFalse()
    {
        // Arrange
        var span = GetSpan(SourceLocation.Zero, "Hello {world}.");
        var change = new SourceChange(new SourceSpan(0, 5), string.Empty);

        // Act
        var result = CodeBlockEditHandler.IsAcceptableReplacement(span, change);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsAcceptableDeletion_ValidChange_ReturnsTrue()
    {
        // Arrange
        var span = GetSpan(SourceLocation.Zero, "Hello {world}.");
        var change = new SourceChange(new SourceSpan(0, 5), string.Empty);

        // Act
        var result = CodeBlockEditHandler.IsAcceptableDeletion(span, change);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsAcceptableDeletion_InvalidChange_ReturnsFalse()
    {
        // Arrange
        var span = GetSpan(SourceLocation.Zero, "Hello {world}.");
        var change = new SourceChange(new SourceSpan(5, 3), string.Empty);

        // Act
        var result = CodeBlockEditHandler.IsAcceptableDeletion(span, change);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsAcceptableDeletion_NotDelete_ReturnsFalse()
    {
        // Arrange
        var span = GetSpan(SourceLocation.Zero, "world");
        var change = new SourceChange(new SourceSpan(0, 0), "hello");

        // Act
        var result = CodeBlockEditHandler.IsAcceptableDeletion(span, change);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ModifiesInvalidContent_ValidContent_ReturnsFalse()
    {
        // Arrange
        var span = GetSpan(SourceLocation.Zero, "Hello {world}.");
        var change = new SourceChange(new SourceSpan(0, 5), string.Empty);

        // Act
        var result = CodeBlockEditHandler.ModifiesInvalidContent(span, change);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ModifiesInvalidContent_InvalidContent_ReturnsTrue()
    {
        // Arrange
        var span = GetSpan(SourceLocation.Zero, "Hello {world}.");
        var change = new SourceChange(new SourceSpan(5, 7), string.Empty);

        // Act
        var result = CodeBlockEditHandler.ModifiesInvalidContent(span, change);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsAcceptableInsertion_ValidChange_ReturnsTrue()
    {
        // Arrange
        var change = new SourceChange(new SourceSpan(0, 0), "hello");

        // Act
        var result = CodeBlockEditHandler.IsAcceptableInsertion(change);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsAcceptableInsertion_InvalidChange_ReturnsFalse()
    {
        // Arrange
        var change = new SourceChange(new SourceSpan(0, 0), "{");

        // Act
        var result = CodeBlockEditHandler.IsAcceptableInsertion(change);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsAcceptableInsertion_InvalidChange_Transition_ReturnsFalse()
    {
        // Arrange
        var change = new SourceChange(new SourceSpan(0, 0), "@");

        // Act
        var result = CodeBlockEditHandler.IsAcceptableInsertion(change);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsAcceptableInsertion_InvalidChange_TemplateTransition_ReturnsFalse()
    {
        // Arrange
        var change = new SourceChange(new SourceSpan(0, 0), "<");

        // Act
        var result = CodeBlockEditHandler.IsAcceptableInsertion(change);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsAcceptableInsertion_NotInsert_ReturnsFalse()
    {
        // Arrange
        var change = new SourceChange(new SourceSpan(0, 2), string.Empty);

        // Act
        var result = CodeBlockEditHandler.IsAcceptableInsertion(change);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData("if (true) { }")]
    [InlineData("@<div></div>")]
    [InlineData("<div></div>")]
    [InlineData("*")]
    public void ContainsInvalidContent_InvalidContent_ReturnsTrue(string content)
    {
        // Arrange
        var change = new SourceChange(new SourceSpan(0, 0), content);

        // Act
        var result = CodeBlockEditHandler.ContainsInvalidContent(change);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("var x = true;")]
    [InlineData("if (true) Console.WriteLine('!')")]
    public void ContainsInvalidContent_ValidContent_ReturnsFalse(string content)
    {
        // Arrange
        var change = new SourceChange(new SourceSpan(0, 0), content);

        // Act
        var result = CodeBlockEditHandler.ContainsInvalidContent(change);

        // Assert
        Assert.False(result);
    }

    private static SyntaxNode GetSpan(SourceLocation start, string content)
    {
        var builder = SyntaxListBuilder<SyntaxToken>.Create();
        var tokens = CSharpLanguageCharacteristics.Instance.TokenizeString(content).ToArray();
        foreach (var token in tokens)
        {
            builder.Add((SyntaxToken)token.CreateRed());
        }
        var node = SyntaxFactory.CSharpStatementLiteral(builder.ToList(), SpanChunkGenerator.Null);

        return node;
    }
}
