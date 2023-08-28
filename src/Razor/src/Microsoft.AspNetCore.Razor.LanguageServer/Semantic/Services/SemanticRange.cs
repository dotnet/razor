// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic;

internal struct SemanticRange : IComparable<SemanticRange>
{
    public SemanticRange(int kind, Range range, int modifier, bool fromRazor)
    : this(kind, new RazorRange() { EndCharacter = range.End.Character, EndLine = range.End.Line, StartCharacter = range.Start.Character, StartLine = range.Start.Line }, modifier, fromRazor)
    {
    }

    public SemanticRange(int kind, int startLine, int startCharacter, int endLine, int endCharacter, int modifier, bool fromRazor)
    : this(kind, new RazorRange() { EndCharacter = endCharacter, EndLine = endLine, StartCharacter = startCharacter, StartLine = startLine }, modifier, fromRazor)
    {
    }

    public SemanticRange(int kind, RazorRange razorRange, int modifier, bool fromRazor)
    {
        Kind = kind;
        Modifier = modifier;
        Range = razorRange;
        FromRazor = fromRazor;
    }


    public RazorRange Range { get; }

    public int Kind { get; }

    public int Modifier { get; }

    /// <summary>
    /// If we produce a token, and a delegated server produces a token, we want to prefer ours, so we use this flag to help our
    /// sort algorithm, that way we can avoid the perf hit of actually finding duplicates, and just take the first instance that
    /// covers a range.
    /// </summary>
    public bool FromRazor { get; }

    public int CompareTo(SemanticRange other)
    {
        var startCompare = Range.StartLine.CompareTo(other.Range.StartLine);
        if (startCompare != 0)
        {
            return startCompare;
        }

        startCompare = Range.StartCharacter.CompareTo(other.Range.StartCharacter);
        if (startCompare != 0)
        {
            return startCompare;
        }

        var endCompare = Range.EndLine.CompareTo(other.Range.EndLine);
        if (endCompare != 0)
        {
            return endCompare;
        }

        endCompare = Range.EndCharacter.CompareTo(other.Range.EndCharacter);
        if (endCompare != 0)
        {
            return endCompare;
        }

        // If we have ranges that are the same, we want a Razor produced token to win over a non-Razor produced token
        if (FromRazor && !other.FromRazor)
        {
            return -1;
        }
        else if (other.FromRazor && !FromRazor)
        {
            return 1;
        }

        return 0;
    }

    public override string ToString()
        => $"[Kind: {Kind}, Range: {Range}]";
}
