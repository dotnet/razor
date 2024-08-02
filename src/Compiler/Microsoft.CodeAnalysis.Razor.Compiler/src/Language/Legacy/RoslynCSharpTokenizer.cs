// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using SyntaxToken = Microsoft.AspNetCore.Razor.Language.Syntax.InternalSyntax.SyntaxToken;
using SyntaxFactory = Microsoft.AspNetCore.Razor.Language.Syntax.InternalSyntax.SyntaxFactory;
using CSharpSyntaxKind = Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using CSharpSyntaxToken = Microsoft.CodeAnalysis.SyntaxToken;
using CSharpSyntaxTriviaList = Microsoft.CodeAnalysis.SyntaxTriviaList;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

#pragma warning disable RSEXPERIMENTAL003 // SyntaxTokenParser is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
internal class RoslynCSharpTokenizer : CSharpTokenizer
{
    private readonly SyntaxTokenParser _roslynTokenParser;
    private readonly List<(int position, SyntaxTokenParser.Result result)> _resultCache = new();
    private (CSharpSyntaxTriviaList.Enumerator enumerator, bool isLeading)? _currentCSharpTokenTriviaEnumerator;

    public RoslynCSharpTokenizer(SeekableTextReader source)
        : base(source)
    {
        base.CurrentState = StartState;

        // PROTOTYPE
        _roslynTokenParser = CodeAnalysis.CSharp.SyntaxFactory.CreateTokenParser(source.SourceText, null);
    }

    protected override int StartState => (int)RoslynCSharpTokenizerState.Start;

    private new RoslynCSharpTokenizerState? CurrentState
    {
        get => (RoslynCSharpTokenizerState?)base.CurrentState;
        set => base.CurrentState = (int?)value;
    }

    public override SyntaxKind RazorCommentKind => SyntaxKind.RazorCommentLiteral;

    public override SyntaxKind RazorCommentTransitionKind => SyntaxKind.RazorCommentTransition;

    public override SyntaxKind RazorCommentStarKind => SyntaxKind.RazorCommentStar;

    internal override void StartingBlock()
    {
        _roslynTokenParser.SkipForwardTo(Source.Position);
    }

    internal override void EndingBlock()
    {
        // We should always be transitioning to the other parser in response to content. This means that we'll have parsed a token, and be in the TriviaForCSharpToken
        // state. The other possibility is that the parser looked at the current token, and put it back to let the other parser handle it; in this case, we should be in
        // the Start state. In order to ensure that we properly handle the trailing trivia (because the other parser will handle the trailing trivia on the node we found,
        // if any), we need to reset back before the start of that node, skip the content, and reset our state back to Start for when we're called back next.
        if (CurrentState == RoslynCSharpTokenizerState.Start)
        {
            return;
        }

        Debug.Assert(CurrentState == RoslynCSharpTokenizerState.TriviaForCSharpToken, $"Unexpected state: {CurrentState}");
        Debug.Assert(_currentCSharpTokenTriviaEnumerator is (_, isLeading: false));
        Debug.Assert(_resultCache.Count > 0);

        var (_, result) = _resultCache[^1];
        _roslynTokenParser.ResetTo(result);
        _resultCache.RemoveAt(_resultCache.Count - 1);
        var lastToken = result.Token;
        if (lastToken.HasLeadingTrivia)
        {
            // If the previous token did indeed have leading trivia, we need to make sure to take it into account so that any preprocessor directives are seen by the
            // roslyn token parser
            _ = GetNextResult(NextResultType.LeadingTrivia);
        }

        _roslynTokenParser.SkipForwardTo(lastToken.Span.End);
        CurrentState = RoslynCSharpTokenizerState.Start;
    }

    protected override StateResult Dispatch()
    {
        switch (CurrentState)
        {
            case RoslynCSharpTokenizerState.Start:
                return Start();
            case RoslynCSharpTokenizerState.TriviaForCSharpToken:
                return Trivia();
            case RoslynCSharpTokenizerState.Token:
                return Token();
            case RoslynCSharpTokenizerState.OnRazorCommentStar:
                return OnRazorCommentStar();
            case RoslynCSharpTokenizerState.AfterRazorCommentTransition:
                return AfterRazorCommentTransition();
            case RoslynCSharpTokenizerState.RazorCommentBody:
                return RazorCommentBody();
            case RoslynCSharpTokenizerState.StarAfterRazorCommentBody:
                return StarAfterRazorCommentBody();
            case RoslynCSharpTokenizerState.AtTokenAfterRazorCommentBody:
                Debug.Assert(_currentCSharpTokenTriviaEnumerator is not null);
                return AtTokenAfterRazorCommentBody(nextState: (int)RoslynCSharpTokenizerState.TriviaForCSharpToken);
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

    private StateResult Start()
    {
        var leadingTriviaResult = GetNextResult(NextResultType.LeadingTrivia);
        Debug.Assert(leadingTriviaResult.ContextualKind == CSharpSyntaxKind.None);
        Debug.Assert(leadingTriviaResult.Token.IsKind(CSharpSyntaxKind.None));

        if (leadingTriviaResult.Token.HasLeadingTrivia)
        {
            _currentCSharpTokenTriviaEnumerator = (leadingTriviaResult.Token.LeadingTrivia.GetEnumerator(), isLeading: true);
            return Transition(RoslynCSharpTokenizerState.TriviaForCSharpToken, null);
        }
        else
        {
            return Transition(RoslynCSharpTokenizerState.Token, null);
        }
    }

    private StateResult Token()
    {
        if (SyntaxFacts.IsNewLine(CurrentCharacter) || SyntaxFacts.IsWhitespace(CurrentCharacter))
        {
            Assumed.Unreachable();
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
                return TokenizedExpectedStringOrCharacterLiteral(StringOrCharacterKind.Character);
            case '"':
                return TokenizedExpectedStringOrCharacterLiteral(StringOrCharacterKind.String_Or_Raw_String);
            case '$':
                switch (Peek())
                {
                    case '"' or '$':
                        return TokenizedExpectedStringOrCharacterLiteral(StringOrCharacterKind.Interpolated_Or_Raw_Interpolated_String);
                    case '@' when Peek(2) == '"':
                        return TokenizedExpectedStringOrCharacterLiteral(StringOrCharacterKind.Verbatim_Interpolated_Dollar_First_String);
                }
                goto default;
            case '.':
                if (char.IsDigit(Peek()))
                {
                    return NumericLiteral();
                }
                return Operator();
            case '/' when Peek() is '/' or '*':
                return Assumed.Unreachable<StateResult>();
            default:
                return Operator();
        }
    }

    private StateResult AtToken()
    {
        AssertCurrent('@');
        switch (Peek())
        {
            case '"':
                return TokenizedExpectedStringOrCharacterLiteral(StringOrCharacterKind.Verbatim_String);
            case '$' when Peek(2) is '"':
                return TokenizedExpectedStringOrCharacterLiteral(StringOrCharacterKind.Verbatim_Interpolated_At_First_String);
            case '*':
                return Assumed.Unreachable<StateResult>();
            case '@':
                // Escaped razor transition. Likely will error in the parser.
                AddResetPoint();
                TakeCurrent();
                _roslynTokenParser.SkipForwardTo(Source.Position);
                AssertCurrent('@');
                return Transition(RoslynCSharpTokenizerState.Token, EndToken(SyntaxKind.Transition));
            default:
                // Either a standard razor transition or a C# identifier. The parser will take care of stitching together the transition and the
                // identifier if it's the latter case.
                AddResetPoint();
                TakeCurrent();
                _roslynTokenParser.SkipForwardTo(Source.Position);
                var trailingTrivia = GetNextResult(NextResultType.TrailingTrivia);
                _currentCSharpTokenTriviaEnumerator = (trailingTrivia.Token.TrailingTrivia.GetEnumerator(), isLeading: false);
                return Transition(RoslynCSharpTokenizerState.TriviaForCSharpToken, EndToken(SyntaxKind.Transition));
        }
    }

    private StateResult Operator()
    {
        var curPosition = Source.Position;
        var result = GetNextResult(NextResultType.Token);
        var token = result.Token;

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

        _currentCSharpTokenTriviaEnumerator = (token.TrailingTrivia.GetEnumerator(), isLeading: false);
        return Transition(RoslynCSharpTokenizerState.TriviaForCSharpToken, EndToken(content, kind));

        void TakeTokenContent(CSharpSyntaxToken token, out string content)
        {
            // Use the already-interned string from the C# lexer, rather than realizing the buffer, to ensure that
            // we don't allocate a new string for every operator token.
            content = token.ValueText;
            Debug.Assert(content == Buffer.ToString());
            Buffer.Clear();
        }
    }

    private StateResult TokenizedExpectedStringOrCharacterLiteral(StringOrCharacterKind expectedStringKind)
    {
        var curPosition = Source.Position;
        var result = GetNextResult(NextResultType.Token);
        var csharpToken = result.Token;
        // Don't include trailing trivia; we handle that differently than Roslyn
        var finalPosition = curPosition + csharpToken.Span.Length;
        (string expectedPrefix, string expectedPostfix, bool lookForPrePostFix) = expectedStringKind switch
        {
            StringOrCharacterKind.Character => ("'", "'", false),
            StringOrCharacterKind.Verbatim_String => ("@\"", "\"", false),
            StringOrCharacterKind.Verbatim_Interpolated_At_First_String => ("@$\"", "\"", false),
            StringOrCharacterKind.Verbatim_Interpolated_Dollar_First_String => ("$@\"", "\"", false),
            StringOrCharacterKind.String_Or_Raw_String when csharpToken.Text is "\"\"" => ("\"", "\"", false),
            StringOrCharacterKind.Interpolated_Or_Raw_Interpolated_String when csharpToken.Text is "$\"\"" => ("$\"", "\"", false),
            StringOrCharacterKind.String_Or_Raw_String or StringOrCharacterKind.Interpolated_Or_Raw_Interpolated_String => ("", "", true),
            _ => throw new InvalidOperationException($"Unexpected expectedStringKind: {expectedStringKind}."),
        };

        for (; curPosition < finalPosition; curPosition++)
        {
            if (lookForPrePostFix)
            {
                lookForPrePostFix = handleCurrentCharacterPrefixPostfix();
            }

            TakeCurrent();
        }

        // If the token is the expected kind and has the expected prefix or doesn't have the expected postfix, then it's unterminated.
        // This is a case like `"test` (which doesn't end in the expected postfix), or `"` (which ends in the expected postfix, but
        // exactly matches the expected prefix).
        if (lookForPrePostFix || csharpToken.Text == expectedPrefix || !csharpToken.Text.EndsWith(expectedPostfix!, StringComparison.Ordinal))
        {
            CurrentErrors.Add(
                RazorDiagnosticFactory.CreateParsing_UnterminatedStringLiteral(
                    new SourceSpan(CurrentStart, contentLength: expectedPrefix?.Length ?? 0 /* " */)));
        }

        _currentCSharpTokenTriviaEnumerator = (csharpToken.TrailingTrivia.GetEnumerator(), isLeading: false);
        var razorTokenKind = expectedStringKind == StringOrCharacterKind.Character ? SyntaxKind.CharacterLiteral : SyntaxKind.StringLiteral;
        return Transition(RoslynCSharpTokenizerState.TriviaForCSharpToken, EndToken(razorTokenKind));

        bool handleCurrentCharacterPrefixPostfix()
        {
            switch (expectedStringKind)
            {
                case StringOrCharacterKind.String_Or_Raw_String:
                    // We can either have a normal string or a raw string. Add to the prefix/postfix until we find a non-" character
                    if (CurrentCharacter != '"')
                    {
                        Debug.Assert(expectedPrefix != null);
                        Debug.Assert(expectedPostfix != null);
                        Debug.Assert(expectedPrefix == expectedPostfix);
                        return false;
                    }

                    expectedPrefix += '"';
                    expectedPostfix += '"';
                    return true;

                case StringOrCharacterKind.Interpolated_Or_Raw_Interpolated_String:
                    // Start with the leading $'s
                    if (expectedPrefix is "" or [.., '$'])
                    {
                        if (CurrentCharacter == '$')
                        {
                            expectedPrefix += '$';
                            return true;
                        }
                        else if (CurrentCharacter == '"')
                        {
                            expectedPrefix += '"';
                            expectedPostfix += '"';
                            return true;
                        }
                        else
                        {
                            // We expect roslyn to have ended parsing, so we should never get here
                            return Assumed.Unreachable<bool>();
                        }
                    }

                    Debug.Assert(expectedPrefix[^1] == '"');
                    if (CurrentCharacter == '"')
                    {
                        expectedPrefix += '"';
                        expectedPostfix += '"';
                        return true;
                    }
                    else
                    {
                        return false;
                    }

                case StringOrCharacterKind.Character:
                case StringOrCharacterKind.Verbatim_String:
                case StringOrCharacterKind.Verbatim_Interpolated_At_First_String:
                case StringOrCharacterKind.Verbatim_Interpolated_Dollar_First_String:
                default:
                    return Assumed.Unreachable<bool>();
            }
        }
    }


    private StateResult Trivia()
    {
        Debug.Assert(_currentCSharpTokenTriviaEnumerator is not null);
        var (triviaEnumerator, isLeading) = _currentCSharpTokenTriviaEnumerator.Value;

        if (!triviaEnumerator.MoveNext())
        {
            _currentCSharpTokenTriviaEnumerator = null;
            if (!isLeading)
            {
                return Transition(RoslynCSharpTokenizerState.Start, null);
            }
            else
            {
                return Transition(RoslynCSharpTokenizerState.Token, null);
            }
        }

        // Need to make sure the class state is correct, since structs are copied
        _currentCSharpTokenTriviaEnumerator = (triviaEnumerator, isLeading);

        var curPosition = Source.Position;
        var trivia = triviaEnumerator.Current;
        var triviaString = trivia.ToFullString();

        // We handle razor comments with dedicated nodes
        if (trivia.IsKind(CSharpSyntaxKind.MultiLineCommentTrivia) && triviaString.StartsWith("@*", StringComparison.Ordinal))
        {
            Debug.Assert(CurrentCharacter == '@');
            TakeCurrent();

            return Transition(
                RoslynCSharpTokenizerState.OnRazorCommentStar,
                EndToken(SyntaxKind.RazorCommentTransition));
        }

        // Use FullSpan here because doc comment trivias exclude the leading `///` or `/**` and the trailing `*/`
        var finalPosition = curPosition + trivia.FullSpan.Length;

        for (; curPosition < finalPosition; curPosition++)
        {
            TakeCurrent();
        }

        if (EndOfFile
            && trivia.Kind() is CSharpSyntaxKind.MultiLineCommentTrivia or CSharpSyntaxKind.MultiLineDocumentationCommentTrivia
            && !triviaString.EndsWith("*/", StringComparison.Ordinal))
        {
            CurrentErrors.Add(
                RazorDiagnosticFactory.CreateParsing_BlockCommentNotTerminated(
                    new SourceSpan(CurrentStart, contentLength: 1 /* end of file */)));
        }

        // PROTOTYPE: Handle preprocessor directives
        var tokenType = trivia.Kind() switch
        {
            CSharpSyntaxKind.WhitespaceTrivia => SyntaxKind.Whitespace,
            CSharpSyntaxKind.EndOfLineTrivia => SyntaxKind.NewLine,
            CSharpSyntaxKind.SingleLineCommentTrivia or CSharpSyntaxKind.MultiLineCommentTrivia or CSharpSyntaxKind.MultiLineDocumentationCommentTrivia or CSharpSyntaxKind.SingleLineDocumentationCommentTrivia => SyntaxKind.CSharpComment,
            var kind => throw new InvalidOperationException($"Unexpected trivia kind: {kind}."),
        };

        return Stay(EndToken(tokenType));
    }

    private StateResult OnRazorCommentStar()
    {
        AssertCurrent('*');
        TakeCurrent();

        return Transition(
            RoslynCSharpTokenizerState.RazorCommentBody,
            EndToken(SyntaxKind.RazorCommentStar));
    }

    // CSharp Spec §2.4.4
    private StateResult NumericLiteral()
    {
        var curPosition = Source.Position;
        var result = GetNextResult(NextResultType.Token);
        var csharpToken = result.Token;
        // Don't include trailing trivia; we handle that differently than Roslyn
        var finalPosition = curPosition + csharpToken.Span.Length;

        for (; curPosition < finalPosition; curPosition++)
        {
            TakeCurrent();
        }

        Buffer.Clear();
        _currentCSharpTokenTriviaEnumerator = (csharpToken.TrailingTrivia.GetEnumerator(), isLeading: false);
        return Transition(RoslynCSharpTokenizerState.TriviaForCSharpToken, EndToken(csharpToken.Text, SyntaxKind.NumericLiteral));
    }

    private StateResult Identifier()
    {
        var curPosition = Source.Position;
        var result = GetNextResult(NextResultType.Token);
        var csharpToken = result.Token;
        // Don't include trailing trivia; we handle that differently than Roslyn
        var finalPosition = curPosition + csharpToken.Span.Length;

        for (; curPosition < finalPosition; curPosition++)
        {
            TakeCurrent();
        }

        var type = SyntaxKind.Identifier;
        if (!csharpToken.IsKind(CSharpSyntaxKind.IdentifierToken) || result.ContextualKind is not (CSharpSyntaxKind.None or CSharpSyntaxKind.IdentifierToken))
        {
            type = SyntaxKind.Keyword;
        }

        var token = EndToken(csharpToken.Text, type);

        Buffer.Clear();
        _currentCSharpTokenTriviaEnumerator = (csharpToken.TrailingTrivia.GetEnumerator(), isLeading: false);
        return Transition(RoslynCSharpTokenizerState.TriviaForCSharpToken, token);
    }

    private StateResult Transition(RoslynCSharpTokenizerState state, SyntaxToken? result)
    {
        return Transition((int)state, result);
    }

    internal override CSharpSyntaxKind? GetTokenKeyword(SyntaxToken token)
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

    private SyntaxTokenParser.Result GetNextResult(NextResultType expectedType)
    {
        var nextResult = expectedType switch
        {
            NextResultType.LeadingTrivia => _roslynTokenParser.ParseLeadingTrivia(),
            NextResultType.Token => _roslynTokenParser.ParseNextToken(),
            NextResultType.TrailingTrivia => _roslynTokenParser.ParseTrailingTrivia(),
            _ => Assumed.Unreachable<SyntaxTokenParser.Result>()
        };

        Debug.Assert(_resultCache.All(r => r.position <= nextResult.Token.FullSpan.Start));

        if (_resultCache.Count > 0 && _resultCache[^1].position == nextResult.Token.FullSpan.Start)
        {
            // This can happen when there was no leading or trailing trivia for this token. We don't need to maintain both the previous
            // result and the current result, as the current result fully subsumes it.
            Debug.Assert(_resultCache[^1].result is { Token.FullSpan.Length: 0 });
            Debug.Assert(!nextResult.Token.HasLeadingTrivia);
            _resultCache[^1] = (nextResult.Token.FullSpan.Start, nextResult);
        }
        else
        {
            _resultCache.Add((nextResult.Token.FullSpan.Start, nextResult));
        }

        return nextResult;
    }

    private void AddResetPoint()
    {
        // We want to make it easy to reset the tokenizer back to just before this token; we can do that very simply by trying to parse
        // leading trivia, which gives us a reset point. We know that we can't have any leading trivia, since we're on an `@` character.
        var nextResult = GetNextResult(NextResultType.LeadingTrivia);
        Debug.Assert(nextResult.Token.IsKind(CSharpSyntaxKind.None));
        Debug.Assert(nextResult.Token.FullSpan.Length == 0);
    }

    public override void Reset(int position)
    {
        // Most common reset point is the last parsed token, so just try that first.
        Debug.Assert(_resultCache.Count > 0);

        var lastIndex = _resultCache.Count - 1;
        var lastResult = _resultCache[lastIndex];
        if (lastResult.position == position)
        {
            _resultCache.RemoveAt(lastIndex);
            _roslynTokenParser.ResetTo(lastResult.result);
        }
        else
        {
            // Not the last one, so binary search
            var index = _resultCache.BinarySearch((position, default), ResultCacheSearcher.Instance);

            if (index >= 0)
            {
                // Found an exact match
                var resetResult = _resultCache[index].result;
                _roslynTokenParser.ResetTo(resetResult);
                _resultCache.RemoveRange(index, _resultCache.Count - index);
            }
            else
            {
                // Reset to the one before reset point, then skip forward
                index = ~index - 1;
                // We know there was at least one element in the list, so BinarySearch returned either the element with a position further ahead of where we want to reset to, or the length of the list.
                // In either case, we know that index is valid.
                Debug.Assert(index >= 0);

                // We don't want to actually remove the result from the cache in this point: the parser could later ask to reset further back in this same result. This mostly happens for trivia, where the
                // parser may ask to put multiple tokens back, each part of the same roslyn trivia piece. However, it is not _only_ for trivia, so we can't assert that. The parser may decide, for example,
                // to split the @ from a token, reset the tokenizer after the @, and then keep going.
                var resetResult = _resultCache[index].result;
                _roslynTokenParser.ResetTo(resetResult);
                _roslynTokenParser.SkipForwardTo(position);

                if (index + 1 < _resultCache.Count)
                {
                    // Any results further ahead than the position we reset to are no longer valid, so we want to remove them.
                    // We need to keep the position we reset to, just in case the parser asks to reset to it again.
                    _resultCache.RemoveRange(index + 1, _resultCache.Count - index - 1);
                }
            }

        }

        CurrentState = RoslynCSharpTokenizerState.Start;
    }

    public override void Dispose()
    {
        base.Dispose();
        _roslynTokenParser.Dispose();
    }

    private enum NextResultType
    {
        LeadingTrivia,
        Token,
        TrailingTrivia,
    }

    private enum StringOrCharacterKind
    {
        Character,
        String_Or_Raw_String,
        Interpolated_Or_Raw_Interpolated_String,
        Verbatim_String,
        Verbatim_Interpolated_At_First_String,
        Verbatim_Interpolated_Dollar_First_String,
        Verbatim_Interpolated_String,
    }

    private enum RoslynCSharpTokenizerState
    {
        Start,
        Token,
        TriviaForCSharpToken,

        // Razor Comments - need to be the same for HTML and CSharp
        OnRazorCommentStar,
        AfterRazorCommentTransition = RazorCommentTokenizerState.AfterRazorCommentTransition,
        RazorCommentBody = RazorCommentTokenizerState.RazorCommentBody,
        StarAfterRazorCommentBody = RazorCommentTokenizerState.StarAfterRazorCommentBody,
        AtTokenAfterRazorCommentBody = RazorCommentTokenizerState.AtTokenAfterRazorCommentBody,
    }

    private sealed class ResultCacheSearcher : IComparer<(int position, SyntaxTokenParser.Result result)>
    {
        public static ResultCacheSearcher Instance { get; } = new ResultCacheSearcher();

        public int Compare((int position, SyntaxTokenParser.Result result) x, (int position, SyntaxTokenParser.Result result) y)
        {
            return x.position.CompareTo(y.position);
        }
    }
}
