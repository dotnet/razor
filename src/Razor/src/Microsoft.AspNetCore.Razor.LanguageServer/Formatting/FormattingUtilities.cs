// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting;

internal static class FormattingUtilities
{
    public static readonly string Indent = "$$indent$$";
    public static readonly string InnerIndent = "$$innerIndent$$";

    /// <summary>
    ///  Adds indenting to the method.
    /// </summary>
    /// <param name="method">
    ///  The method to add indenting to. The method should be marked with <see cref="Indent"/> where an indent is wanted
    ///  and <see cref="InnerIndent"/> where an additional level of indent is wanted.
    /// </param>
    /// <param name="options">The <see cref="RazorLSPOptions"/> that contains information about indenting.</param>
    /// <param name="startingIndent">The size of the any existing indent.</param>
    /// <returns>The indented method.</returns>
    public static string AddIndentationToMethod(string method, RazorLSPOptions options, int startingIndent)
    {
        var indentSize = options.TabSize;
        var insertSpaces = options.InsertSpaces;
        var indent = GetIndentationString(startingIndent + indentSize, insertSpaces, indentSize);
        var innerIndent = GetIndentationString(startingIndent + indentSize + indentSize, insertSpaces, indentSize);
        return method.Replace(Indent, indent).Replace(InnerIndent, innerIndent);
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
