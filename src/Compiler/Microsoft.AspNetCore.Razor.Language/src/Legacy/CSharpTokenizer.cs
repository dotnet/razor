// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
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

    private static readonly Dictionary<string, CSharpKeyword> _keywords = new Dictionary<string, CSharpKeyword>(StringComparer.Ordinal)
        {
            { "await", CSharpKeyword.Await },
            { "abstract", CSharpKeyword.Abstract },
            { "byte", CSharpKeyword.Byte },
            { "class", CSharpKeyword.Class },
            { "delegate", CSharpKeyword.Delegate },
            { "event", CSharpKeyword.Event },
            { "fixed", CSharpKeyword.Fixed },
            { "if", CSharpKeyword.If },
            { "internal", CSharpKeyword.Internal },
            { "new", CSharpKeyword.New },
            { "override", CSharpKeyword.Override },
            { "readonly", CSharpKeyword.Readonly },
            { "short", CSharpKeyword.Short },
            { "struct", CSharpKeyword.Struct },
            { "try", CSharpKeyword.Try },
            { "unsafe", CSharpKeyword.Unsafe },
            { "volatile", CSharpKeyword.Volatile },
            { "as", CSharpKeyword.As },
            { "do", CSharpKeyword.Do },
            { "is", CSharpKeyword.Is },
            { "params", CSharpKeyword.Params },
            { "ref", CSharpKeyword.Ref },
            { "switch", CSharpKeyword.Switch },
            { "ushort", CSharpKeyword.Ushort },
            { "while", CSharpKeyword.While },
            { "case", CSharpKeyword.Case },
            { "const", CSharpKeyword.Const },
            { "explicit", CSharpKeyword.Explicit },
            { "float", CSharpKeyword.Float },
            { "null", CSharpKeyword.Null },
            { "sizeof", CSharpKeyword.Sizeof },
            { "typeof", CSharpKeyword.Typeof },
            { "implicit", CSharpKeyword.Implicit },
            { "private", CSharpKeyword.Private },
            { "this", CSharpKeyword.This },
            { "using", CSharpKeyword.Using },
            { "extern", CSharpKeyword.Extern },
            { "return", CSharpKeyword.Return },
            { "stackalloc", CSharpKeyword.Stackalloc },
            { "uint", CSharpKeyword.Uint },
            { "base", CSharpKeyword.Base },
            { "catch", CSharpKeyword.Catch },
            { "continue", CSharpKeyword.Continue },
            { "double", CSharpKeyword.Double },
            { "for", CSharpKeyword.For },
            { "in", CSharpKeyword.In },
            { "lock", CSharpKeyword.Lock },
            { "object", CSharpKeyword.Object },
            { "protected", CSharpKeyword.Protected },
            { "static", CSharpKeyword.Static },
            { "false", CSharpKeyword.False },
            { "public", CSharpKeyword.Public },
            { "sbyte", CSharpKeyword.Sbyte },
            { "throw", CSharpKeyword.Throw },
            { "virtual", CSharpKeyword.Virtual },
            { "decimal", CSharpKeyword.Decimal },
            { "else", CSharpKeyword.Else },
            { "operator", CSharpKeyword.Operator },
            { "string", CSharpKeyword.String },
            { "ulong", CSharpKeyword.Ulong },
            { "bool", CSharpKeyword.Bool },
            { "char", CSharpKeyword.Char },
            { "default", CSharpKeyword.Default },
            { "foreach", CSharpKeyword.Foreach },
            { "long", CSharpKeyword.Long },
            { "void", CSharpKeyword.Void },
            { "enum", CSharpKeyword.Enum },
            { "finally", CSharpKeyword.Finally },
            { "int", CSharpKeyword.Int },
            { "out", CSharpKeyword.Out },
            { "sealed", CSharpKeyword.Sealed },
            { "true", CSharpKeyword.True },
            { "goto", CSharpKeyword.Goto },
            { "unchecked", CSharpKeyword.Unchecked },
            { "interface", CSharpKeyword.Interface },
            { "break", CSharpKeyword.Break },
            { "checked", CSharpKeyword.Checked },
            { "namespace", CSharpKeyword.Namespace },
            { "when", CSharpKeyword.When },
            { "where", CSharpKeyword.Where }
        };

    public CSharpTokenizer(SeekableTextReader source)
        : base(source)
    {
        base.CurrentState = StartState;

        _lexer = CodeAnalysis.CSharp.SyntaxFactory.CreateLexer(source.SourceText);
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
            case CSharpTokenizerState.QuotedCharacterLiteral:
                return TokenizedExpectedStringOrCharacterLiteral(CSharpSyntaxKind.CharacterLiteralToken, SyntaxKind.CharacterLiteral, "\'", "\'");
            case CSharpTokenizerState.QuotedStringLiteral:
                return TokenizedExpectedStringOrCharacterLiteral(CSharpSyntaxKind.StringLiteralToken, SyntaxKind.StringLiteral, "\"", "\"");
            case CSharpTokenizerState.VerbatimStringLiteral:
                return TokenizedExpectedStringOrCharacterLiteral(CSharpSyntaxKind.StringLiteralToken, SyntaxKind.StringLiteral, "@\"", "\"");
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
                case SyntaxKind.NumericLiteral:
                    switch (Buffer[0])
                    {
                        case '0':
                            return "0";
                        case '1':
                            return "1";
                        case '2':
                            return "2";
                        case '3':
                            return "3";
                        case '4':
                            return "4";
                        case '5':
                            return "5";
                        case '6':
                            return "6";
                        case '7':
                            return "7";
                        case '8':
                            return "8";
                        case '9':
                            return "9";
                    }
                    break;
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
                return Transition(CSharpTokenizerState.QuotedCharacterLiteral);
            case '"':
                return Transition(CSharpTokenizerState.QuotedStringLiteral);
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
        if (Peek() == '"')
        {
            return Transition(CSharpTokenizerState.VerbatimStringLiteral);
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

    private StateResult TokenizedExpectedStringOrCharacterLiteral(CSharpSyntaxKind expectedCSharpTokenKind, SyntaxKind razorTokenKind, string expectedPrefix, string expectedPostFix)
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
            && (csharpToken.Text == expectedPrefix || !csharpToken.Text.EndsWith(expectedPostFix, StringComparison.Ordinal)))
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

        return Transition(CSharpTokenizerState.Data, EndToken(SyntaxKind.NumericLiteral));
    }

    // CSharp Spec §2.4.2
    private StateResult Identifier()
    {
        Debug.Assert(SyntaxFacts.IsIdentifierStartCharacter(CurrentCharacter));
        TakeCurrent();
        TakeUntil(c => !SyntaxFacts.IsIdentifierPartCharacter(c));
        SyntaxToken token = null;
        if (HaveContent)
        {
            CSharpKeyword keyword;
            var type = SyntaxKind.Identifier;
            var tokenContent = Buffer.ToString();
            if (_keywords.TryGetValue(tokenContent, out keyword))
            {
                type = SyntaxKind.Keyword;
            }

            token = SyntaxFactory.Token(type, tokenContent);

            Buffer.Clear();
            CurrentErrors.Clear();
        }

        return Stay(token);
    }

    private StateResult Transition(CSharpTokenizerState state)
    {
        return Transition((int)state, result: null);
    }

    private StateResult Transition(CSharpTokenizerState state, SyntaxToken result)
    {
        return Transition((int)state, result);
    }

    internal static CSharpKeyword? GetTokenKeyword(SyntaxToken token)
    {
        if (token != null && _keywords.TryGetValue(token.Content, out var keyword))
        {
            return keyword;
        }

        return null;
    }

    private enum CSharpTokenizerState
    {
        Data,
        QuotedCharacterLiteral,
        QuotedStringLiteral,
        VerbatimStringLiteral,

        // Razor Comments - need to be the same for HTML and CSharp
        AfterRazorCommentTransition = RazorCommentTokenizerState.AfterRazorCommentTransition,
        EscapedRazorCommentTransition = RazorCommentTokenizerState.EscapedRazorCommentTransition,
        RazorCommentBody = RazorCommentTokenizerState.RazorCommentBody,
        StarAfterRazorCommentBody = RazorCommentTokenizerState.StarAfterRazorCommentBody,
        AtTokenAfterRazorCommentBody = RazorCommentTokenizerState.AtTokenAfterRazorCommentBody,
    }
}
