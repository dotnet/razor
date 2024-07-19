// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Text;

internal static class TextSpanExtensions
{
    internal static TextSpan TrimLeadingWhitespace(this TextSpan span, SourceText text)
    {
        ArgHelper.ThrowIfNull(text);

        for (var i = 0; i < span.Length; ++i)
        {
            if (!char.IsWhiteSpace(text[span.Start + i]))
            {
                return new TextSpan(span.Start + i, span.Length - i);
            }
        }

        return span;
    }

    public static Range ToRange(this TextSpan span, SourceText text)
    {
        var (start, end) = text.GetLinesAndOffsets(span);

        var range = new Range
        {
            Start = new(start.line, start.offset),
            End = new(end.line, end.offset)
        };

        return range;
    }
}
