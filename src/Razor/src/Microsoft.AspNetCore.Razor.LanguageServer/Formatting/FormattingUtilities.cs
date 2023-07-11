// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
internal class FormattingUtilities
{
    /// <summary>
    ///  Adds indenting to the method.
    /// </summary>
    /// <param name="method">
    ///  The method to add indenting to. The method should be marked with '0' where an indent is wanted and a '1' where an additional level of indent is wanted.
    /// </param>
    /// <param name="insertSpaces">Whether spaces are used for indentation.</param>
    /// <param name="indentSize">The size of the indent.</param>
    /// <param name="startingIndent">The size of the any existing indent.</param>
    /// <returns>The indented method.</returns>
    public static string AddIndentationToMethod(string method, bool insertSpaces, int indentSize, int startingIndent)
    {
        var indent = GetIndentationString(startingIndent + indentSize, insertSpaces, indentSize);
        var innerIndent = GetIndentationString(startingIndent + indentSize + indentSize, insertSpaces, indentSize);
        return method.Replace("0", indent).Replace("1", innerIndent);
    }

    /// <summary>
    /// Given a <paramref name="indentation"/> amount of characters, generate a string representing the configured indentation.
    /// </summary>
    /// <param name="indentation">An amount of characters to represent the indentation.</param>
    /// <param name="insertSpaces">Whether spaces are used for indentation.</param>
    /// <param name="tabSize">The size of a tab.</param>
    /// <returns>A whitespace string representation indentation.</returns>
    public static string GetIndentationString(int indentation, bool insertSpaces, int tabSize)
    {
        if (insertSpaces)
        {
            return new string(' ', indentation);
        }

        var tabs = indentation / tabSize;
        var tabPrefix = new string('\t', tabs);

        var spaces = indentation % tabSize;
        var spaceSuffix = new string(' ', spaces);

        var combined = string.Concat(tabPrefix, spaceSuffix);
        return combined;
    }
}
