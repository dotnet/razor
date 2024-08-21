// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Text;

using VsLspPosition = Microsoft.VisualStudio.LanguageServer.Protocol.Position;

namespace Roslyn.LanguageServer.Protocol;

internal static partial class RoslynLspExtensions
{
    public static LinePosition ToLinePosition(this Position position)
        => new(position.Line, position.Character);

    public static string ToDisplayString(this Position position)
        => $"({position.Line}, {position.Character})";

    public static VsLspPosition ToVsLspPosition(this Position position)
        => new ()
        {
            Character = position.Character,
            Line = position.Line
        };
}
