// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

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

    public static Range ToRange(this TextSpan span, SourceText sourceText)
    {
        ArgHelper.ThrowIfNull(sourceText);

        sourceText.GetLinesAndOffsets(span, out var startLine, out var startChar, out var endLine, out var endChar);

        var range = new Range
        {
            Start = new Position(startLine, startChar),
            End = new Position(endLine, endChar)
        };

        return range;
    }
}
