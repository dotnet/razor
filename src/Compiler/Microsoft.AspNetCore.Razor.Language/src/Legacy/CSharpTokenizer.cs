// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using SyntaxToken = Microsoft.AspNetCore.Razor.Language.Syntax.InternalSyntax.SyntaxToken;
using SyntaxFactory = Microsoft.AspNetCore.Razor.Language.Syntax.InternalSyntax.SyntaxFactory;
using CSharpSyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

internal class CSharpTokenizer : Tokenizer
{
    private readonly SourceTextLexer _lexer;

    public CSharpTokenizer(SeekableTextReader source)
        : base(source)
    {
        base.CurrentState = StartState;

        _lexer = CodeAnalysis.CSharp.SyntaxFactory.CreateLexer(source.SourceText, null);
    }

    protected override int StartState => (int)CSharpTokenizerState.Data;

    private new CSharpTokenizerState? CurrentState => (CSharpTokenizerState?)base.CurrentState;

    public override SyntaxKind RazorCommentKind => SyntaxKind.RazorCommentLiteral;

    public override SyntaxKind RazorCommentTransitionKind => SyntaxKind.RazorCommentTransition;

    public override SyntaxKind RazorCommentStarKind => SyntaxKind.RazorCommentStar;

    protected override StateResult Dispatch()
    {
        switch (CurrentState)
        {
            case CSharpTokenizerState.Data:
                return Data();
            case CSharpTokenizerState.AfterRazorCommentTransition:
                return AfterRazorCommentTransition();
            case CSharpTokenizerState.EscapedRazorCommentTransition:
                return EscapedRazorCommentTransition();
            case CSharpTokenizerState.RazorCommentBody:
                return RazorCommentBody();
            case CSharpTokenizerState.StarAfterRazorCommentBody:
                return StarAfterRazorCommentBody();
            case CSharpTokenizerState.AtTokenAfterRazorCommentBody:
                return AtTokenAfterRazorCommentBody();
            default:
                Debug.Fail("Invalid TokenizerState");
                return default(StateResult);
        }
    }

    // Optimize memory allocation by returning constants for the most frequent cases
    protected override string GetTokenContent(SyntaxKind type)
    {
        Debug.Assert(type != SyntaxKind.CSharpOperator, "CSharpOperator should be handled by getting the interned text from C#");
        var tokenLength = Buffer.Length;

        if (tokenLength == 1)
        {
            switch (type)
            {
                case SyntaxKind.NewLine:
                    if (Buffer[0] == '\n')
                    {
                        return "\n";
                    }
                    break;
                case SyntaxKind.Whitespace:
                    if (Buffer[0] == ' ')
                    {
                        return " ";
                    }
                    if (Buffer[0] == '\t')
                    {
                        return "\t";
                    }
                    break;
                case SyntaxKind.NumericLiteral:
                    Debug.Fail("This should be handled by using the C# lexer's interned string in NumericLiteral()");
                    return base.GetTokenContent(type);
                case SyntaxKind.Not:
                case SyntaxKind.LeftParenthesis:
                case SyntaxKind.RightParenthesis:
                case SyntaxKind.Comma:
                case SyntaxKind.Dot:
                case SyntaxKind.Colon:
                case SyntaxKind.Semicolon:
                case SyntaxKind.QuestionMark:
                case SyntaxKind.RightBracket:
                case SyntaxKind.LeftBracket:
                case SyntaxKind.LeftBrace:
                case SyntaxKind.RightBrace:
                case SyntaxKind.LessThan:
                case SyntaxKind.Assign:
                case SyntaxKind.GreaterThan:
                    Debug.Fail("This should be handled by using the C# lexer's interned string in Operator()");
                    return base.GetTokenContent(type);
                case SyntaxKind.Transition:
                    return "@";

            }
        }
        else if (tokenLength == 2)
        {
            switch (type)
            {
                case SyntaxKind.NewLine:
                    return "\r\n";
                case SyntaxKind.DoubleColon:
                case SyntaxKind.Equals:
                    Debug.Fail("This should be handled by using the C# lexer's interned string in Operator()");
                    return base.GetTokenContent(type);
            }
        }

        return base.GetTokenContent(type);
    }

    protected override SyntaxToken CreateToken(string content, SyntaxKind kind, RazorDiagnostic[] errors)
    {
        return SyntaxFactory.Token(kind, content, errors);
    }

    private StateResult Data()
    {
        if (SyntaxFacts.IsNewLine(CurrentCharacter) || SyntaxFacts.IsWhitespace(CurrentCharacter))
        {
            return Stay(Trivia());
        }
        else if (SyntaxFacts.IsIdentifierStartCharacter(CurrentCharacter))
        {
            return Identifier();
        }
        else if (char.IsDigit(CurrentCharacter))
        {
            return NumericLiteral();
        }
        switch (CurrentCharacter)
        {
            case '@':
                return AtToken();
            case '\'':
                return TokenizedExpectedStringOrCharacterLiteral(CSharpSyntaxKind.CharacterLiteralToken, SyntaxKind.CharacterLiteral, expectedPrefix: "\'", expectedPostfix: "\'");
            case '"':
                return TokenizedExpectedStringOrCharacterLiteral(CSharpSyntaxKind.StringLiteralToken, SyntaxKind.StringLiteral, expectedPrefix: "\"", expectedPostfix: "\"");
            case '$':
                switch (Peek())
                {
                    case '"':
                        return TokenizedExpectedStringOrCharacterLiteral(CSharpSyntaxKind.InterpolatedStringStartToken, SyntaxKind.StringLiteral, expectedPrefix: "$\"", expectedPostfix: "\"");
                    case '@' when Peek(2) == '"':
                        return TokenizedExpectedStringOrCharacterLiteral(CSharpSyntaxKind.InterpolatedVerbatimStringStartToken, SyntaxKind.StringLiteral, expectedPrefix: "$@\"", expectedPostfix: "\"");
                }
                goto default;
            case '.':
                if (char.IsDigit(Peek()))
                {
                    return NumericLiteral();
                }
                return Stay(Operator());
            case '/' when Peek() is '/' or '*':
                return Stay(Trivia());
            default:
                return Stay(Operator());
        }
    }

    private StateResult AtToken()
    {
        switch (Peek())
        {
            case '"':
                return TokenizedExpectedStringOrCharacterLiteral(CSharpSyntaxKind.StringLiteralToken, SyntaxKind.StringLiteral, expectedPrefix: "@\"", expectedPostfix: "\"");
            case '$' when Peek(2) == '"':
                return TokenizedExpectedStringOrCharacterLiteral(CSharpSyntaxKind.InterpolatedStringStartToken, SyntaxKind.StringLiteral, expectedPrefix: "@$\"", expectedPostfix: "\"");
        }

        TakeCurrent();
        if (CurrentCharacter == '*')
        {
            return Transition(
                CSharpTokenizerState.AfterRazorCommentTransition,
                EndToken(SyntaxKind.RazorCommentTransition));
        }
        else if (CurrentCharacter == '@')
        {
            // Could be escaped comment transition
            return Transition(
                CSharpTokenizerState.EscapedRazorCommentTransition,
                EndToken(SyntaxKind.Transition));
        }

        return Stay(EndToken(SyntaxKind.Transition));
    }

    private StateResult EscapedRazorCommentTransition()
    {
        TakeCurrent();
        return Transition(CSharpTokenizerState.Data, EndToken(SyntaxKind.Transition));
    }

    private SyntaxToken Operator()
    {
        var curPosition = Source.Position;
        var token = _lexer.LexSyntax(curPosition);

        // Don't include trailing trivia; we handle that differently than Roslyn
        var finalPosition = curPosition + token.Span.Length;

        for (; curPosition < finalPosition; curPosition++)
        {
            TakeCurrent();
        }

        SyntaxKind kind;
        string content;
        switch (token.RawKind)
        {
            case (int)CSharpSyntaxKind.ExclamationToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.Not;
                break;
            case (int)CSharpSyntaxKind.OpenParenToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.LeftParenthesis;
                break;
            case (int)CSharpSyntaxKind.CloseParenToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.RightParenthesis;
                break;
            case (int)CSharpSyntaxKind.CommaToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.Comma;
                break;
            case (int)CSharpSyntaxKind.DotToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.Dot;
                break;
            case (int)CSharpSyntaxKind.ColonColonToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.DoubleColon;
                break;
            case (int)CSharpSyntaxKind.ColonToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.Colon;
                break;
            case (int)CSharpSyntaxKind.OpenBraceToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.LeftBrace;
                break;
            case (int)CSharpSyntaxKind.CloseBraceToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.RightBrace;
                break;
            case (int)CSharpSyntaxKind.LessThanToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.LessThan;
                break;
            case (int)CSharpSyntaxKind.GreaterThanToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.GreaterThan;
                break;
            case (int)CSharpSyntaxKind.EqualsToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.Assign;
                break;
            case (int)CSharpSyntaxKind.OpenBracketToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.LeftBracket;
                break;
            case (int)CSharpSyntaxKind.CloseBracketToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.RightBracket;
                break;
            case (int)CSharpSyntaxKind.QuestionToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.QuestionMark;
                break;
            case (int)CSharpSyntaxKind.SemicolonToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.Semicolon;
                break;
            case <= (int)CSharpSyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken and >= (int)CSharpSyntaxKind.TildeToken:
                TakeTokenContent(token, out content);
                kind = SyntaxKind.CSharpOperator;
                break;
            default:
                kind = SyntaxKind.Marker;
                content = Buffer.ToString();
                Buffer.Clear();
                break;
        }

        return EndToken(content, kind);

        void TakeTokenContent(CodeAnalysis.SyntaxToken token, out string content)
        {
            // Use the already-interned string from the C# lexer, rather than realizing the buffer, to ensure that
            // we don't allocate a new string for every operator token.
            content = token.ValueText;
            Debug.Assert(content == Buffer.ToString());
            Buffer.Clear();
        }
    }

    private StateResult TokenizedExpectedStringOrCharacterLiteral(CSharpSyntaxKind expectedCSharpTokenKind, SyntaxKind razorTokenKind, string expectedPrefix, string expectedPostfix)
    {
        var curPosition = Source.Position;
        var csharpToken = _lexer.LexSyntax(curPosition);
        // Don't include trailing trivia; we handle that differently than Roslyn
        var finalPosition = curPosition + csharpToken.Span.Length;

        for (; curPosition < finalPosition; curPosition++)
        {
            TakeCurrent();
        }

        // If the token is the expected kind and has the expected prefix or doesn't have the expected postfix, then it's unterminated.
        // This is a case like `"test` (which doesn't end in the expected postfix), or `"` (which ends in the expected postfix, but
        // exactly matches the expected prefix).
        if (CodeAnalysis.CSharpExtensions.IsKind(csharpToken, expectedCSharpTokenKind)
            && (csharpToken.Text == expectedPrefix || !csharpToken.Text.EndsWith(expectedPostfix, StringComparison.Ordinal)))
        {
            CurrentErrors.Add(
                RazorDiagnosticFactory.CreateParsing_UnterminatedStringLiteral(
                    new SourceSpan(CurrentStart, contentLength: expectedPrefix.Length /* " */)));
        }

        return Transition(CSharpTokenizerState.Data, EndToken(razorTokenKind));
    }

    private SyntaxToken Trivia()
    {
        Debug.Assert((CurrentCharacter == '/' && Peek() is '*' or '/')
                     || SyntaxFacts.IsWhitespace(CurrentCharacter)
                     || SyntaxFacts.IsNewLine(CurrentCharacter));
        var curPosition = Source.Position;
        var nextToken = _lexer.LexSyntax(curPosition);
        Debug.Assert(nextToken.HasLeadingTrivia);
        var leadingTrivia = nextToken.LeadingTrivia[0];

        // Use FullSpan here because doc comment trivias exclude the leading `///` or `/**` and the trailing `*/`
        var finalPosition = curPosition + leadingTrivia.FullSpan.Length;

        for (; curPosition < finalPosition; curPosition++)
        {
            TakeCurrent();
        }

        if (nextToken.IsKind(CSharpSyntaxKind.EndOfFileToken) && leadingTrivia.Kind() is CSharpSyntaxKind.MultiLineCommentTrivia or CSharpSyntaxKind.MultiLineDocumentationCommentTrivia &&
            !leadingTrivia.ToFullString().EndsWith("*/", StringComparison.Ordinal))
        {
            CurrentErrors.Add(
                RazorDiagnosticFactory.CreateParsing_BlockCommentNotTerminated(
                    new SourceSpan(CurrentStart, contentLength: 1 /* end of file */)));
        }

        var tokenType = leadingTrivia.Kind() switch
        {
            CSharpSyntaxKind.WhitespaceTrivia => SyntaxKind.Whitespace,
            CSharpSyntaxKind.EndOfLineTrivia => SyntaxKind.NewLine,
            CSharpSyntaxKind.SingleLineCommentTrivia or CSharpSyntaxKind.MultiLineCommentTrivia or CSharpSyntaxKind.MultiLineDocumentationCommentTrivia or CSharpSyntaxKind.SingleLineDocumentationCommentTrivia => SyntaxKind.CSharpComment,
            _ => throw new InvalidOperationException("Unexpected trivia kind."),
        };

        return EndToken(tokenType);
    }

    // CSharp Spec §2.4.4
    private StateResult NumericLiteral()
    {
        var curPosition = Source.Position;
        var csharpToken = _lexer.LexSyntax(curPosition);
        // Don't include trailing trivia; we handle that differently than Roslyn
        var finalPosition = curPosition + csharpToken.Span.Length;

        for (; curPosition < finalPosition; curPosition++)
        {
            TakeCurrent();
        }

        Buffer.Clear();
        return Transition(CSharpTokenizerState.Data, EndToken(csharpToken.Text, SyntaxKind.NumericLiteral));
    }

    private StateResult Identifier()
    {
        var curPosition = Source.Position;
        var csharpToken = _lexer.LexSyntax(curPosition);
        // Don't include trailing trivia; we handle that differently than Roslyn
        var finalPosition = curPosition + csharpToken.Span.Length;

        for (; curPosition < finalPosition; curPosition++)
        {
            TakeCurrent();
        }

        var type = SyntaxKind.Identifier;
        if (!csharpToken.IsKind(CSharpSyntaxKind.IdentifierToken) || SourceTextLexer.GetContextualKind(csharpToken) != CSharpSyntaxKind.IdentifierToken)
        {
            type = SyntaxKind.Keyword;
        }

        var token = EndToken(csharpToken.Text, type);

        Buffer.Clear();
        return Transition(CSharpTokenizerState.Data, token);
    }

    private StateResult Transition(CSharpTokenizerState state, SyntaxToken result)
    {
        return Transition((int)state, result);
    }

    internal static CSharpSyntaxKind GetTokenKeywordKind(SyntaxToken token)
    {
        if (token is null)
        {
            return CSharpSyntaxKind.None;
        }

        var content = token.Content;
        return SyntaxFacts.GetKeywordKind(content) is var kind and not CSharpSyntaxKind.None
            ? kind
            : SyntaxFacts.GetContextualKeywordKind(content);
    }

    private enum CSharpTokenizerState
    {
        Data,

        // Razor Comments - need to be the same for HTML and CSharp
        AfterRazorCommentTransition = RazorCommentTokenizerState.AfterRazorCommentTransition,
        EscapedRazorCommentTransition = RazorCommentTokenizerState.EscapedRazorCommentTransition,
        RazorCommentBody = RazorCommentTokenizerState.RazorCommentBody,
        StarAfterRazorCommentBody = RazorCommentTokenizerState.StarAfterRazorCommentBody,
        AtTokenAfterRazorCommentBody = RazorCommentTokenizerState.AtTokenAfterRazorCommentBody,
    }
}
