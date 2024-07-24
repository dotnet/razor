// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.VisualStudio.LanguageServer.Protocol;

internal static class VsLspFactory
{
    private static readonly Position s_emptyPosition = new(0, 0);

    private static readonly Range s_emptyRange = new()
    {
        Start = s_emptyPosition,
        End = s_emptyPosition
    };

    private static readonly Position s_undefinedPosition = new(-1, -1);

    private static readonly Range s_undefinedRange = new()
    {
        Start = s_undefinedPosition,
        End = s_undefinedPosition
    };

    public static Position EmptyPosition
    {
        get
        {
            var emptyPosition = s_emptyPosition;

            // Since Position is mutable, it's possible that something might modify it. If that happens, we should know!
            Debug.Assert(
                emptyPosition.Line == 0 &&
                emptyPosition.Character == 0,
                $"{nameof(VsLspFactory)}.{nameof(EmptyPosition)} has been corrupted. Current value: {emptyPosition.ToDisplayString()}");

            return emptyPosition;
        }
    }

    public static Range EmptyRange
    {
        get
        {
            var emptyRange = s_emptyRange;

            // Since Range is mutable, it's possible that something might modify it. If that happens, we should know!
            Debug.Assert(
                emptyRange.Start.Line == 0 &&
                emptyRange.Start.Character == 0 &&
                emptyRange.End.Line == 0 &&
                emptyRange.End.Character == 0,
                $"{nameof(VsLspFactory)}.{nameof(EmptyRange)} has been corrupted. Current value: {emptyRange.ToDisplayString()}");

            return emptyRange;
        }
    }

    public static Position UndefinedPosition
    {
        get
        {
            var undefinedPosition = s_undefinedPosition;

            // Since Position is mutable, it's possible that something might modify it. If that happens, we should know!
            Debug.Assert(
                undefinedPosition.Line == -1 &&
                undefinedPosition.Character == -1,
                $"{nameof(VsLspFactory)}.{nameof(UndefinedPosition)} has been corrupted. Current value: {undefinedPosition.ToDisplayString()}");

            return undefinedPosition;
        }
    }

    public static Range UndefinedRange
    {
        get
        {
            var undefinedRange = s_undefinedRange;

            // Since Range is mutable, it's possible that something might modify it. If that happens, we should know!
            Debug.Assert(
                undefinedRange.Start.Line == -1 &&
                undefinedRange.Start.Character == -1 &&
                undefinedRange.End.Line == -1 &&
                undefinedRange.End.Character == -1,
                $"{nameof(VsLspFactory)}.{nameof(UndefinedRange)} has been corrupted. Current value: {undefinedRange.ToDisplayString()}");

            return undefinedRange;
        }
    }

    public static Position CreatePosition(int line, int character)
        => (line, character) switch
        {
            (0, 0) => EmptyPosition,
            (-1, -1) => UndefinedPosition,
            _ => new(line, character)
        };

    public static Position CreatePosition(LinePosition linePosition)
        => CreatePosition(linePosition.Line, linePosition.Character);

    public static Range CreateRange(int startLine, int startCharacter, int endLine, int endCharacter)
        => (startLine, startCharacter, endLine, endCharacter) switch
        {
            (0, 0, 0, 0) => EmptyRange,
            (-1, -1, -1, -1) => UndefinedRange,
            _ => CreateRange(CreatePosition(startLine, startCharacter), CreatePosition(endLine, endCharacter))
        };

    public static Range CreateRange(Position start, Position end)
        => new() { Start = start, End = end };

    public static Range CreateRange(LinePosition start, LinePosition end)
        => CreateRange(start.Line, start.Character, end.Line, end.Character);

    public static Range CreateRange(LinePositionSpan span)
        => CreateRange(span.Start, span.End);

    public static Range CreateZeroWidthRange(int line, int character)
        => (line, character) switch
        {
            (0, 0) => EmptyRange,
            (-1, -1) => UndefinedRange,
            _ => CreateZeroWidthRange(CreatePosition(line, character))
        };

    public static Range CreateZeroWidthRange(Position position)
        => CreateRange(position, position);

    public static Range CreateZeroWidthRange(LinePosition position)
        => CreateRange(position, position);

    public static Range CreateSingleLineRange(int line, int character, int length)
        => CreateRange(line, character, line, character + length);

    public static Range CreateSingleLineRange(Position start, int length)
        => CreateRange(start, CreatePosition(start.Line, start.Character + length));

    public static Range CreateSingleLineRange(LinePosition start, int length)
        => CreateSingleLineRange(start.Line, start.Character, length);

    public static TextEdit CreateTextEdit(Range range, string newText)
        => new() { Range = range, NewText = newText };

    public static TextEdit CreateTextEdit(LinePositionSpan span, string newText)
        => CreateTextEdit(CreateRange(span), newText);

    public static TextEdit CreateTextEdit(int line, int character, string newText)
        => CreateTextEdit(CreateZeroWidthRange(line, character), newText);

    public static TextEdit CreateTextEdit(Position position, string newText)
        => CreateTextEdit(CreateZeroWidthRange(position), newText);

    public static TextEdit CreateTextEdit(LinePosition position, string newText)
        => CreateTextEdit(CreateZeroWidthRange(position.Line, position.Character), newText);

    public static TextEdit CreateTextEdit(int startLine, int startCharacter, int endLine, int endCharacter, string newText)
        => CreateTextEdit(CreateRange(startLine, startCharacter, endLine, endCharacter), newText);

    public static TextEdit CreateTextEdit(Position start, Position end, string newText)
        => CreateTextEdit(CreateRange(start, end), newText);

    public static TextEdit CreateTextEdit(LinePosition start, LinePosition end, string newText)
        => CreateTextEdit(CreateRange(start, end), newText);
}
