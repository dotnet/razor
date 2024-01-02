// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Common;
internal static class SourceTextHelper
{
    public static bool IsLinePositionValid(this SourceText sourceText, LinePosition linePosition, ILogger? logger)
    {
        var textLines = sourceText.Lines;
        if (linePosition.Line >= textLines.Count)
        {
            Debug.Fail($"Invalid request for position on line {linePosition.Line} in a document with {textLines.Count} line(s).");
            logger?.LogError("Invalid request for position on line {lineNumber} in a document with {lineCount} line(s).", linePosition.Line, textLines.Count);
            return false;
        }

        var textLine = textLines[linePosition.Line];
        if (linePosition.Character > textLine.SpanIncludingLineBreak.Length)
        {
            Debug.Fail($"Invalid request for position at column {linePosition.Character} on a line with {textLine.SpanIncludingLineBreak.Length} character(s).");
            logger?.LogError("Invalid request for position at column {columnNumber} on a line with {characterCount} character(s).", linePosition.Character, textLine.SpanIncludingLineBreak.Length);
            return false;
        }

        return true;
    }
}
