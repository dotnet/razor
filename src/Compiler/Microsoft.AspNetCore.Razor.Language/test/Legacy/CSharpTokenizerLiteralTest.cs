// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.Razor.Language.Syntax.InternalSyntax;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

public class CSharpTokenizerLiteralTest : CSharpTokenizerTestBase
{
    private new SyntaxToken IgnoreRemaining => (SyntaxToken)base.IgnoreRemaining;

    [Fact]
    public void Simple_Integer_Literal_Is_Recognized()
    {
        TestSingleToken("01189998819991197253", SyntaxKind.NumericLiteral);
    }

    [Fact]
    public void Integer_Type_Suffix_Is_Recognized()
    {
        TestSingleToken("42U", SyntaxKind.NumericLiteral);
        TestSingleToken("42u", SyntaxKind.NumericLiteral);

        TestSingleToken("42L", SyntaxKind.NumericLiteral);
        TestSingleToken("42l", SyntaxKind.NumericLiteral);

        TestSingleToken("42UL", SyntaxKind.NumericLiteral);
        TestSingleToken("42Ul", SyntaxKind.NumericLiteral);

        TestSingleToken("42uL", SyntaxKind.NumericLiteral);
        TestSingleToken("42ul", SyntaxKind.NumericLiteral);

        TestSingleToken("42LU", SyntaxKind.NumericLiteral);
        TestSingleToken("42Lu", SyntaxKind.NumericLiteral);

        TestSingleToken("42lU", SyntaxKind.NumericLiteral);
        TestSingleToken("42lu", SyntaxKind.NumericLiteral);
    }

    [Fact]
    public void Trailing_Letter_Is_Not_Part_Of_Integer_Literal_If_Not_Type_Sufix()
    {
        TestTokenizer("42a", SyntaxFactory.Token(SyntaxKind.NumericLiteral, "42"), IgnoreRemaining);
    }

    [Fact]
    public void Simple_Hex_Literal_Is_Recognized()
    {
        TestSingleToken("0x0123456789ABCDEF", SyntaxKind.NumericLiteral);
    }

    [Fact]
    public void Integer_Type_Suffix_Is_Recognized_In_Hex_Literal()
    {
        TestSingleToken("0xDEADBEEFU", SyntaxKind.NumericLiteral);
        TestSingleToken("0xDEADBEEFu", SyntaxKind.NumericLiteral);

        TestSingleToken("0xDEADBEEFL", SyntaxKind.NumericLiteral);
        TestSingleToken("0xDEADBEEFl", SyntaxKind.NumericLiteral);

        TestSingleToken("0xDEADBEEFUL", SyntaxKind.NumericLiteral);
        TestSingleToken("0xDEADBEEFUl", SyntaxKind.NumericLiteral);

        TestSingleToken("0xDEADBEEFuL", SyntaxKind.NumericLiteral);
        TestSingleToken("0xDEADBEEFul", SyntaxKind.NumericLiteral);

        TestSingleToken("0xDEADBEEFLU", SyntaxKind.NumericLiteral);
        TestSingleToken("0xDEADBEEFLu", SyntaxKind.NumericLiteral);

        TestSingleToken("0xDEADBEEFlU", SyntaxKind.NumericLiteral);
        TestSingleToken("0xDEADBEEFlu", SyntaxKind.NumericLiteral);
    }

    [Fact]
    public void Trailing_Letter_Is_Not_Part_Of_Hex_Literal_If_Not_Type_Suffix()
    {
        TestTokenizer("0xDEADBEEFz", SyntaxFactory.Token(SyntaxKind.NumericLiteral, "0xDEADBEEF"), IgnoreRemaining);
    }

    [Fact]
    public void Binary_Literal_Is_Recognized()
    {
        TestSingleToken("0b01010101", SyntaxKind.NumericLiteral);
    }

    [Fact]
    public void Integer_Type_Suffix_Is_Recognized_In_Binary_Literal()
    {
        TestSingleToken("0b01010101U", SyntaxKind.NumericLiteral);
        TestSingleToken("0b01010101u", SyntaxKind.NumericLiteral);

        TestSingleToken("0b01010101L", SyntaxKind.NumericLiteral);
        TestSingleToken("0b01010101l", SyntaxKind.NumericLiteral);

        TestSingleToken("0b01010101UL", SyntaxKind.NumericLiteral);
        TestSingleToken("0b01010101Ul", SyntaxKind.NumericLiteral);

        TestSingleToken("0b01010101uL", SyntaxKind.NumericLiteral);
        TestSingleToken("0b01010101ul", SyntaxKind.NumericLiteral);

        TestSingleToken("0b01010101LU", SyntaxKind.NumericLiteral);
        TestSingleToken("0b01010101Lu", SyntaxKind.NumericLiteral);

        TestSingleToken("0b01010101lU", SyntaxKind.NumericLiteral);
        TestSingleToken("0b01010101lu", SyntaxKind.NumericLiteral);
    }

    [Fact]
    public void Trailing_Letter_Is_Not_Part_Of_Binary_Literal_If_Not_Type_Suffix()
    {
        TestTokenizer("0b01010101z", SyntaxFactory.Token(SyntaxKind.NumericLiteral, "0b01010101"), IgnoreRemaining);
    }

    [Fact]
    public void Dot_Followed_By_Non_Digit_Is_Not_Part_Of_Real_Literal()
    {
        TestTokenizer("3.a", SyntaxFactory.Token(SyntaxKind.NumericLiteral, "3"), IgnoreRemaining);
    }

    [Fact]
    public void Simple_Real_Literal_Is_Recognized()
    {
        TestTokenizer("3.14159", SyntaxFactory.Token(SyntaxKind.NumericLiteral, "3.14159"));
    }

    [Fact]
    public void Real_Literal_Between_Zero_And_One_Is_Recognized()
    {
        TestTokenizer(".14159", SyntaxFactory.Token(SyntaxKind.NumericLiteral, ".14159"));
    }

    [Fact]
    public void Integer_With_Real_Type_Suffix_Is_Recognized()
    {
        TestSingleToken("42F", SyntaxKind.NumericLiteral);
        TestSingleToken("42f", SyntaxKind.NumericLiteral);
        TestSingleToken("42D", SyntaxKind.NumericLiteral);
        TestSingleToken("42d", SyntaxKind.NumericLiteral);
        TestSingleToken("42M", SyntaxKind.NumericLiteral);
        TestSingleToken("42m", SyntaxKind.NumericLiteral);
    }

    [Fact]
    public void Integer_With_Exponent_Is_Recognized()
    {
        TestSingleToken("1e10", SyntaxKind.NumericLiteral);
        TestSingleToken("1E10", SyntaxKind.NumericLiteral);
        TestSingleToken("1e+10", SyntaxKind.NumericLiteral);
        TestSingleToken("1E+10", SyntaxKind.NumericLiteral);
        TestSingleToken("1e-10", SyntaxKind.NumericLiteral);
        TestSingleToken("1E-10", SyntaxKind.NumericLiteral);
    }

    [Fact]
    public void Real_Number_With_Type_Suffix_Is_Recognized()
    {
        TestSingleToken("3.14F", SyntaxKind.NumericLiteral);
        TestSingleToken("3.14f", SyntaxKind.NumericLiteral);
        TestSingleToken("3.14D", SyntaxKind.NumericLiteral);
        TestSingleToken("3.14d", SyntaxKind.NumericLiteral);
        TestSingleToken("3.14M", SyntaxKind.NumericLiteral);
        TestSingleToken("3.14m", SyntaxKind.NumericLiteral);
    }

    [Fact]
    public void Real_Number_With_Exponent_Is_Recognized()
    {
        TestSingleToken("3.14E10", SyntaxKind.NumericLiteral);
        TestSingleToken("3.14e10", SyntaxKind.NumericLiteral);
        TestSingleToken("3.14E+10", SyntaxKind.NumericLiteral);
        TestSingleToken("3.14e+10", SyntaxKind.NumericLiteral);
        TestSingleToken("3.14E-10", SyntaxKind.NumericLiteral);
        TestSingleToken("3.14e-10", SyntaxKind.NumericLiteral);
    }

    [Fact]
    public void Real_Number_With_Exponent_And_Type_Suffix_Is_Recognized()
    {
        TestSingleToken("3.14E+10F", SyntaxKind.NumericLiteral);
    }

    [Fact]
    public void Single_Character_Literal_Is_Recognized()
    {
        TestSingleToken("'f'", SyntaxKind.CharacterLiteral);
    }

    [Fact]
    public void Multi_Character_Literal_Is_Recognized()
    {
        TestSingleToken("'foo'", SyntaxKind.CharacterLiteral);
    }

    [Fact]
    public void Character_Literal_Is_Terminated_By_EOF_If_Unterminated()
    {
        TestSingleToken("'foo bar", SyntaxKind.CharacterLiteral);
    }

    [Fact]
    public void Character_Literal_Not_Terminated_By_Escaped_Quote()
    {
        TestSingleToken("'foo\\'bar'", SyntaxKind.CharacterLiteral);
    }

    [Fact]
    public void Character_Literal_Is_Terminated_By_EOL_If_Unterminated()
    {
        TestTokenizer("'foo\n", SyntaxFactory.Token(SyntaxKind.CharacterLiteral, "'foo"), IgnoreRemaining);
    }

    [Fact]
    public void Character_Literal_Eats_EOL_When_Escaped()
    {
        TestTokenizer("'foo\\\n", SyntaxFactory.Token(SyntaxKind.CharacterLiteral, "'foo\\\n"), IgnoreRemaining);
    }

    [Fact]
    public void Character_Literal_Eats_EOL_When_Escaped_And_Followed_By_Stuff()
    {
        TestTokenizer("'foo\\\nflarg", SyntaxFactory.Token(SyntaxKind.CharacterLiteral, "'foo\\\nflarg"), IgnoreRemaining);
    }

    [Fact]
    public void Character_Literal_Eats_CR_When_Escaped()
    {
        TestTokenizer("'foo\\\r\n", SyntaxFactory.Token(SyntaxKind.CharacterLiteral, "'foo\\\r"), IgnoreRemaining);
    }

    [Fact]
    public void Character_Literal_Eats_CR_When_Escaped_And_Followed_By_Stuff()
    {
        TestTokenizer($"'foo\\\r\nflarg", SyntaxFactory.Token(SyntaxKind.CharacterLiteral, "'foo\\\r"), IgnoreRemaining);
    }

    [Fact]
    public void Character_Literal_Allows_Escaped_Escape()
    {
        TestTokenizer("'foo\\\\'blah", SyntaxFactory.Token(SyntaxKind.CharacterLiteral, "'foo\\\\'"), IgnoreRemaining);
    }

    [Fact]
    public void Character_Literal_Allows_Trailing_Comments()
    {
        TestTokenizer("'f' // This is a comment",
            SyntaxFactory.Token(SyntaxKind.CharacterLiteral, "'f'"),
            SyntaxFactory.Token(SyntaxKind.Whitespace, " "),
            SyntaxFactory.Token(SyntaxKind.CSharpComment, "// This is a comment"));
    }

    [Fact]
    public void Multi_Character_Literal_Allows_Trailing_Comments()
    {
        TestTokenizer("'foo' // This is a comment",
            SyntaxFactory.Token(SyntaxKind.CharacterLiteral, "'foo'"),
            SyntaxFactory.Token(SyntaxKind.Whitespace, " "),
            SyntaxFactory.Token(SyntaxKind.CSharpComment, "// This is a comment"));
    }

    [Fact]
    public void String_Literal_Is_Recognized()
    {
        TestSingleToken("\"foo\"", SyntaxKind.StringLiteral);
    }

    [Fact]
    public void String_Literal_Is_Terminated_By_EOF_If_Unterminated()
    {
        TestSingleToken("\"foo bar", SyntaxKind.StringLiteral);
    }

    [Fact]
    public void String_Literal_Not_Terminated_By_Escaped_Quote()
    {
        TestSingleToken("\"foo\\\"bar\"", SyntaxKind.StringLiteral);
    }

    [Fact]
    public void String_Literal_Is_Terminated_By_EOL_If_Unterminated()
    {
        TestTokenizer("\"foo\n", SyntaxFactory.Token(SyntaxKind.StringLiteral, "\"foo"), IgnoreRemaining);
    }

    [Fact]
    public void String_Literal_Terminated_By_EOL_Even_When_Last_Char_Is_Slash()
    {
        TestTokenizer("\"foo\\\n", SyntaxFactory.Token(SyntaxKind.StringLiteral, "\"foo\\\n"), IgnoreRemaining);
    }

    [Fact]
    public void String_Literal_Terminated_By_EOL_Even_When_Last_Char_Is_Slash_And_Followed_By_Stuff()
    {
        TestTokenizer("\"foo\\\nflarg", SyntaxFactory.Token(SyntaxKind.StringLiteral, "\"foo\\\nflarg"), IgnoreRemaining);
    }

    [Fact]
    public void String_Literal_Eats_Escaped_CR()
    {
        TestTokenizer("\"foo\\\r\n", SyntaxFactory.Token(SyntaxKind.StringLiteral, "\"foo\\\r"), IgnoreRemaining);
    }

    [Fact]
    public void String_Literal_Eats_Escaped_CR_And_Followed_By_Stuff()
    {
        TestTokenizer($"\"foo\\\r\nflarg", SyntaxFactory.Token(SyntaxKind.StringLiteral, "\"foo\\\r"), IgnoreRemaining);
    }

    [Fact]
    public void String_Literal_Allows_Escaped_Escape()
    {
        TestTokenizer("\"foo\\\\\"blah", SyntaxFactory.Token(SyntaxKind.StringLiteral, "\"foo\\\\\""), IgnoreRemaining);
    }

    [Fact]
    public void Verbatim_String_Literal_Can_Contain_Newlines()
    {
        TestSingleToken("@\"foo\nbar\nbaz\"", SyntaxKind.StringLiteral);
    }

    [Fact]
    public void Verbatim_String_Literal_Not_Terminated_By_Escaped_Double_Quote()
    {
        TestSingleToken("@\"foo\"\"bar\"", SyntaxKind.StringLiteral);
    }

    [Fact]
    public void Verbatim_String_Literal_Is_Terminated_By_Slash_Double_Quote()
    {
        TestTokenizer("@\"foo\\\"bar\"", SyntaxFactory.Token(SyntaxKind.StringLiteral, "@\"foo\\\""), IgnoreRemaining);
    }

    [Fact]
    public void Verbatim_String_Literal_Is_Terminated_By_EOF()
    {
        TestSingleToken("@\"foo", SyntaxKind.StringLiteral);
    }

    [Fact]
    public void String_Literal_Allows_Trailing_Comments()
    {
        TestTokenizer("\"foo\" // This is a comment",
            SyntaxFactory.Token(SyntaxKind.StringLiteral, "\"foo\""),
            SyntaxFactory.Token(SyntaxKind.Whitespace, " "),
            SyntaxFactory.Token(SyntaxKind.CSharpComment, "// This is a comment"));
    }

    [Fact]
    public void Verbatim_String_Literal_Allows_Trailing_Comments()
    {
        TestTokenizer("@\"foo\" // This is a comment",
            SyntaxFactory.Token(SyntaxKind.StringLiteral, "@\"foo\""),
            SyntaxFactory.Token(SyntaxKind.Whitespace, " "),
            SyntaxFactory.Token(SyntaxKind.CSharpComment, "// This is a comment"));
    }

    [Fact]
    public void Interpolated_String_Is_Recognized()
    {
        TestTokenizer("""
            $"Hello, {name}!"
            """,
            SyntaxFactory.Token(SyntaxKind.StringLiteral, """
            $"Hello, {name}!"
            """));
    }

    [Fact]
    public void Interpolated_String_Allows_Nested_Strings()
    {
        TestTokenizer("""
            $"Hello, {"world!"}!"
            """,
            SyntaxFactory.Token(SyntaxKind.StringLiteral, """
            $"Hello, {"world!"}!"
            """));
    }

    [Fact]
    public void Interpolated_String_Allows_Escaped_Curly_Braces()
    {
        TestTokenizer("""
            $"Hello, {{name}}!"
            """,
            SyntaxFactory.Token(SyntaxKind.StringLiteral, """
            $"Hello, {{name}}!"
            """));
    }

    [Fact]
    public void Interpolated_String_Allows_Newlines_In_Interpolation_Hole()
    {
        TestTokenizer("""
            $"Hello, {name
                + 1}!"
            """,
            SyntaxFactory.Token(SyntaxKind.StringLiteral, """
            $"Hello, {name
                + 1}!"
            """));
    }

    [Fact]
    public void Interpolated_String_Newlines_Terminate_Content()
    {
        TestTokenizer("""
            $"Hello, {name + 1}
            !"
            """,
            SyntaxFactory.Token(SyntaxKind.StringLiteral, """
            $"Hello, {name + 1}
            """), IgnoreRemaining);
    }

    [Fact]
    public void Interpolated_String_EndOfFile_In_Interpolation_Hole_Ends_String()
    {
        TestTokenizer("""
            $"Hello, {name + 1
            """,
            SyntaxFactory.Token(SyntaxKind.StringLiteral, """
            $"Hello, {name + 1
            """));
    }

    [Fact]
    public void Interpolated_String_Allows_Comment_In_Interpolation_Hole()
    {
        TestTokenizer("""
            $"Hello, {name + 1 // Test!
              }!"
            """,
            SyntaxFactory.Token(SyntaxKind.StringLiteral, """
            $"Hello, {name + 1 // Test!
              }!"
            """));
    }

    [Theory]
    [InlineData("$@")]
    [InlineData("@$")]
    public void Verbatim_Interpolated_String_Is_Recognized(string prefix)
    {
        TestTokenizer($$"""
            {{prefix}}"Hello, {name}!"
            """,
            SyntaxFactory.Token(SyntaxKind.StringLiteral, $$"""
            {{prefix}}"Hello, {name}!"
            """));
    }

    [Theory]
    [InlineData("$@")]
    [InlineData("@$")]
    public void Verbatim_Interpolated_String_Allows_Nested_Strings(string prefix)
    {
        TestTokenizer($$"""
            {{prefix}}"Hello, {"world!"}!"
            """,
            SyntaxFactory.Token(SyntaxKind.StringLiteral, $$"""
            {{prefix}}"Hello, {"world!"}!"
            """));
    }

    [Theory]
    [InlineData("$@")]
    [InlineData("@$")]
    public void Verbatim_Interpolated_String_Allows_Escaped_Curly_Braces(string prefix)
    {
        TestTokenizer($$$"""
            {{{prefix}}}"Hello, {{name}}!"
            """,
            SyntaxFactory.Token(SyntaxKind.StringLiteral, $$$"""
            {{{prefix}}}"Hello, {{name}}!"
            """));
    }

    [Theory]
    [InlineData("$@")]
    [InlineData("@$")]
    public void Verbatim_Interpolated_String_Allows_Newlines_In_Interpolation_Hole(string prefix)
    {
        TestTokenizer($$"""
            {{prefix}}"Hello, {name
                + 1}!"
            """,
            SyntaxFactory.Token(SyntaxKind.StringLiteral, $$"""
            {{prefix}}"Hello, {name
                + 1}!"
            """));
    }

    [Theory]
    [InlineData("$@")]
    [InlineData("@$")]
    public void Verbatim_Interpolated_String_Allows_Newlines_In_Content(string prefix)
    {
        TestTokenizer($$"""
            {{prefix}}"Hello, {name + 1}
            !"
            """,
            SyntaxFactory.Token(SyntaxKind.StringLiteral, $$"""
            {{prefix}}"Hello, {name + 1}
            !"
            """), IgnoreRemaining);
    }

    [Theory]
    [InlineData("$@")]
    [InlineData("@$")]
    public void Verbatim_Interpolated_String_EndOfFile_In_Interpolation_Hole_Ends_String(string prefix)
    {
        TestTokenizer($$"""
            {{prefix}}"Hello, {name + 1
            """,
            SyntaxFactory.Token(SyntaxKind.StringLiteral, $$"""
            {{prefix}}"Hello, {name + 1
            """));
    }

    [Theory]
    [InlineData("$@")]
    [InlineData("@$")]
    public void Verbatim_Interpolated_String_Allows_Comment_In_Interpolation_Hole(string prefix)
    {
        TestTokenizer($$"""
            {{prefix}}"Hello, {name + 1 // Test!
              }!"
            """,
            SyntaxFactory.Token(SyntaxKind.StringLiteral, $$"""
            {{prefix}}"Hello, {name + 1 // Test!
              }!"
            """));
    }
}
