// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic;

internal struct SemanticRange : IComparable<SemanticRange>
{
    public SemanticRange(int kind, int startLine, int startCharacter, int endLine, int endCharacter, int modifier, bool fromRazor)
    {
        Kind = kind;
        Modifier = modifier;
        StartLine = startLine;
        StartCharacter = startCharacter;
        EndLine = endLine;
        EndCharacter = endCharacter;
        FromRazor = fromRazor;
    }


    public int StartLine { get; }

    public int StartCharacter { get; }

    public int EndLine { get; }

    public int EndCharacter { get; }

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
        var startCompare = StartLine.CompareTo(other.StartLine);
        if (startCompare != 0)
        {
            return startCompare;
        }

        startCompare = StartCharacter.CompareTo(other.StartCharacter);
        if (startCompare != 0)
        {
            return startCompare;
        }

        var endCompare = EndLine.CompareTo(other.EndLine);
        if (endCompare != 0)
        {
            return endCompare;
        }

        endCompare = EndCharacter.CompareTo(other.EndCharacter);
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
        => $"[Kind: {Kind}, StartLine: {StartLine}, StartCharacter: {StartCharacter}, EndLine: {EndLine}, EndCharacter: {EndCharacter}]";
}
