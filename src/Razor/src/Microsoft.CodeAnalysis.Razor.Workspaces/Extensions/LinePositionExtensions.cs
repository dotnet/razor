// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor.Logging;
using RLSP = Roslyn.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Text;

internal static class LinePositionExtensions
{
    public static void Deconstruct(this LinePosition linePosition, out int line, out int character)
        => (line, character) = (linePosition.Line, linePosition.Character);

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
