// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Text;
using OSharp = OmniSharp.Extensions.LanguageServer.Protocol.Models;
using VS = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    internal static class RangeExtensions
    {
        public static readonly OSharp.Range UndefinedRange = new()
        {
            Start = new OSharp.Position(-1, -1),
            End = new OSharp.Position(-1, -1)
        };

        public static bool OverlapsWith(this OSharp.Range range!!, OSharp.Range other!!)
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

        public static bool LineOverlapsWith(this OSharp.Range range!!, OSharp.Range other!!)
        {
            var overlapStart = range.Start.Line;
            if (range.Start.Line.CompareTo(other.Start.Line) < 0)
            {
                overlapStart = other.Start.Line;
            }

            var overlapEnd = range.End.Line;
            if (range.End.Line.CompareTo(other.End.Line) > 0)
            {
                overlapEnd = other.End.Line;
            }

            return overlapStart.CompareTo(overlapEnd) <= 0;
        }

        public static OSharp.Range? Overlap(this OSharp.Range range!!, OSharp.Range other!!)
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
            if (overlapStart.CompareTo(overlapEnd) < 0)
            {
                return new OSharp.Range(overlapStart, overlapEnd);
            }

            return null;
        }

        public static bool Contains(this OSharp.Range range!!, OSharp.Range other!!)
        {
            return range.Start.CompareTo(other.Start) <= 0 && range.End.CompareTo(other.End) >= 0;
        }

        public static TextSpan AsTextSpan(this OSharp.Range range!!, SourceText sourceText!!)
        {
            if (range.Start.Line >= sourceText.Lines.Count)
            {
                throw new ArgumentOutOfRangeException($"Range start line {range.Start.Line} matches or exceeds SourceText boundary {sourceText.Lines.Count}.");
            }

            if (range.End.Line >= sourceText.Lines.Count)
            {
                throw new ArgumentOutOfRangeException($"Range end line {range.End.Line} matches or exceeds SourceText boundary {sourceText.Lines.Count}.");
            }

            var start = sourceText.Lines[range.Start.Line].Start + range.Start.Character;
            var end = sourceText.Lines[range.End.Line].Start + range.End.Character;

            var length = end - start;
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException($"{range} resolved to zero or negative length.");
            }

            return new TextSpan(start, length);
        }

        public static Language.Syntax.TextSpan AsRazorTextSpan(this OSharp.Range range!!, SourceText sourceText!!)
        {
            if (range.Start.Line >= sourceText.Lines.Count)
            {
                throw new ArgumentOutOfRangeException($"Range start line {range.Start.Line} matches or exceeds SourceText boundary {sourceText.Lines.Count}.");
            }

            if (range.End.Line >= sourceText.Lines.Count)
            {
                throw new ArgumentOutOfRangeException($"Range end line {range.End.Line} matches or exceeds SourceText boundary {sourceText.Lines.Count}.");
            }

            var start = sourceText.Lines[range.Start.Line].Start + range.Start.Character;
            var end = sourceText.Lines[range.End.Line].Start + range.End.Character;

            var length = end - start;
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException($"{range} resolved to zero or negative length.");
            }

            return new Language.Syntax.TextSpan(start, length);
        }

        public static bool IsUndefined(this OSharp.Range range!!)
        {
            return range == UndefinedRange;
        }

        public static VS.Range AsVSRange(this OSharp.Range range!!)
        {
            return new VS.Range
            {
                Start = new VS.Position
                {
                    Line = range.Start.Line,
                    Character = range.Start.Character
                },
                End = new VS.Position
                {
                    Line = range.End.Line,
                    Character = range.End.Character
                }
            };
        }
    }
}
