// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions;

internal static class LinePositionSpanExtensions
{
    public static Range AsRange(this LinePositionSpan linePositionSpan)
        => new Range
        {
            Start = linePositionSpan.Start.AsPosition(),
            End = linePositionSpan.End.AsPosition()
        };

    public static TextSpan AsTextSpan(this LinePositionSpan linePositionSpan, SourceText sourceText)
        => sourceText.GetTextSpan(linePositionSpan.Start.Line, linePositionSpan.Start.Character, linePositionSpan.End.Line, linePositionSpan.End.Character);
}
