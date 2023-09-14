// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions;

internal static class LinePositionExtensions
{
    public static Position AsPosition(this LinePosition linePosition)
        => new Position(linePosition.Line, linePosition.Character);
}
