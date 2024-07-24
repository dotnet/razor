// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServer.Protocol;

internal static partial class VsLspExtensions
{
    public static void Deconstruct(this Position position, out int line, out int character)
        => (line, character) = (position.Line, position.Character);

    public static LinePosition ToLinePosition(this Position position)
        => new(position.Line, position.Character);

    public static Range ToZeroWidthRange(this Position position)
        => VsLspFactory.CreateZeroWidthRange(position);

    public static bool TryGetAbsoluteIndex(this Position position, SourceText text, out int absoluteIndex)
    {
        ArgHelper.ThrowIfNull(position);
        ArgHelper.ThrowIfNull(text);

        return text.TryGetAbsoluteIndex(position, out absoluteIndex);
    }

    public static bool TryGetAbsoluteIndex(this Position position, SourceText text, ILogger logger, out int absoluteIndex)
    {
        ArgHelper.ThrowIfNull(position);
        ArgHelper.ThrowIfNull(text);

        return text.TryGetAbsoluteIndex(position, logger, out absoluteIndex);
    }

    public static int GetRequiredAbsoluteIndex(this Position position, SourceText text)
        => text.GetRequiredAbsoluteIndex(position);

    public static int GetRequiredAbsoluteIndex(this Position position, SourceText text, ILogger logger)
        => text.GetRequiredAbsoluteIndex(position, logger);

    public static int CompareTo(this Position position, Position other)
    {
        ArgHelper.ThrowIfNull(position);
        ArgHelper.ThrowIfNull(other);

        var result = position.Line.CompareTo(other.Line);
        return result != 0 ? result : position.Character.CompareTo(other.Character);
    }

    public static bool IsValid(this Position position, SourceText text)
    {
        ArgHelper.ThrowIfNull(position);
        ArgHelper.ThrowIfNull(text);

        return text.TryGetAbsoluteIndex(position.Line, position.Character, logger: null, out _);
    }

    public static string ToDisplayString(this Position position)
        => $"({position.Line}, {position.Character})";
}
