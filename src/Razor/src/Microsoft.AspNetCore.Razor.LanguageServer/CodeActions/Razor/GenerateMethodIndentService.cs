// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;
internal class GenerateMethodIndentService
{
    /// <summary>
    ///  Adds indenting to the method.
    /// </summary>
    /// <param name="method">
    ///  The method to add indenting to. The method should be marked with '0' where an indent is wanted and a '1' where an additional level of indent is wanted.
    /// </param>
    /// <param name="indentSize">The size of the indent.</param>
    /// <param name="startingIndent">The size of the any existing indent.</param>
    /// <returns>The indented method.</returns>
    public static string AddIndentation(string method, int indentSize, int startingIndent)
    {
        var indent = string.Empty;
        for (var i = 0; i < startingIndent + indentSize; i++)
        {
            indent += " ";
        }

        var innerIndent = indent;
        for (var i = 0; i < indentSize; i++)
        {
            innerIndent += " ";
        }

        return method.Replace("0", indent).Replace("1", innerIndent);
    }
}
