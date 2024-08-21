// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Text;

using VsLspRange = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Roslyn.LanguageServer.Protocol;

internal static partial class RoslynLspExtensions
{
    public static LinePositionSpan ToLinePositionSpan(this Range range)
        => new(range.Start.ToLinePosition(), range.End.ToLinePosition());

    public static string ToDisplayString(this Range range)
        => $"{range.Start.ToDisplayString()}-{range.End.ToDisplayString()}";

    public static VsLspRange ToVsLspRange(this Range range)
        => new()
        {
            Start = range.Start.ToVsLspPosition(),
            End = range.End.ToVsLspPosition()
        };
}
