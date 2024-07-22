// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using RLSP = Roslyn.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Text;

internal static class LinePositionSpanExtensions
{
    public static void Deconstruct(this LinePositionSpan linePositionSpan, out LinePosition start, out LinePosition end)
        => (start, end) = (linePositionSpan.Start, linePositionSpan.End);

    public static void Deconstruct(this LinePositionSpan linePositionSpan, out int startLine, out int startCharacter, out int endLine, out int endCharacter)
        => (startLine, startCharacter, endLine, endCharacter) = (linePositionSpan.Start.Line, linePositionSpan.Start.Character, linePositionSpan.End.Line, linePositionSpan.End.Character);

    public static RLSP.Range ToRLSPRange(this LinePositionSpan linePositionSpan)
        => new RLSP.Range
        {
            Start = linePositionSpan.Start.ToRLSPPosition(),
            End = linePositionSpan.End.ToRLSPPosition()
        };

    public static TextSpan ToTextSpan(this LinePositionSpan linePositionSpan, SourceText sourceText)
        => sourceText.GetTextSpan(linePositionSpan.Start.Line, linePositionSpan.Start.Character, linePositionSpan.End.Line, linePositionSpan.End.Character);

    public static bool OverlapsWith(this LinePositionSpan range, LinePositionSpan other)
    {
        var overlapStart = range.Start;
        if (range.Start.CompareTo(other.Start) < 0)
        {
            overlapStart = other.Start;
        }

        var overlapEnd = range.End;
        if (range.End.CompareTo(other.End) > 0)
        {
            overlapEnd = other.End;
        }

        // Empty ranges do not overlap with any range.
        return overlapStart.CompareTo(overlapEnd) < 0;
    }
}
