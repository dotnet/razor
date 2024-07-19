// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using RLSP = Roslyn.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal static class LinePositionExtensions
{
    public static Position ToPosition(this LinePosition linePosition)
        => new(linePosition.Line, linePosition.Character);

    public static bool TryGetAbsoluteIndex(this LinePosition position, SourceText text, out int absoluteIndex)
        => text.TryGetAbsoluteIndex(position, out absoluteIndex);

    public static RLSP.Position ToRLSPPosition(this LinePosition linePosition)
        => new RLSP.Position(linePosition.Line, linePosition.Character);

    public static bool TryGetAbsoluteIndex(this LinePosition position, SourceText text, ILogger logger, out int absoluteIndex)
        => text.TryGetAbsoluteIndex(position, logger, out absoluteIndex);

    public static int GetRequiredAbsoluteIndex(this LinePosition position, SourceText text)
        => text.GetRequiredAbsoluteIndex(position);

    public static int GetRequiredAbsoluteIndex(this LinePosition position, SourceText text, ILogger logger)
        => text.GetRequiredAbsoluteIndex(position, logger);
}
