﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Text;

internal static class LinePositionSpanExtensions
{
    public static void Deconstruct(this LinePositionSpan linePositionSpan, out LinePosition start, out LinePosition end)
        => (start, end) = (linePositionSpan.Start, linePositionSpan.End);

    public static void Deconstruct(this LinePositionSpan linePositionSpan, out int startLine, out int startCharacter, out int endLine, out int endCharacter)
        => (startLine, startCharacter, endLine, endCharacter) = (linePositionSpan.Start.Line, linePositionSpan.Start.Character, linePositionSpan.End.Line, linePositionSpan.End.Character);

    public static bool OverlapsWith(this LinePositionSpan span, LinePositionSpan other)
    {
        var overlapStart = span.Start;
        if (span.Start.CompareTo(other.Start) < 0)
        {
            overlapStart = other.Start;
        }

        var overlapEnd = span.End;
        if (span.End.CompareTo(other.End) > 0)
        {
            overlapEnd = other.End;
        }

        // Empty ranges do not overlap with any range.
        return overlapStart.CompareTo(overlapEnd) < 0;
    }

    public static bool LineOverlapsWith(this LinePositionSpan span, LinePositionSpan other)
    {
        var overlapStart = span.Start.Line < other.Start.Line
            ? other.Start.Line
            : span.Start.Line;

        var overlapEnd = span.End.Line > other.End.Line
            ? other.End.Line
            : span.End.Line;

        return overlapStart <= overlapEnd;
    }

    public static bool Contains(this LinePositionSpan span, LinePositionSpan other)
    {
        return span.Start <= other.Start && span.End >= other.End;
    }
}
