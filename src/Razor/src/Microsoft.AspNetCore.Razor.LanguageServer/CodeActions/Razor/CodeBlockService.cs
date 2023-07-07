// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;
internal static class CodeBlockService
{
    /// <summary>
    ///  Creates a <see cref="TextEdit"/> that will place the formatted generated method within a @code block in the file.
    /// </summary>
    /// <param name="code">The <see cref="RazorCodeDocument"/> of the file where the generated method will be placed.</param>
    /// <param name="generatedMethod">
    ///  The skeleton of the generated method where a '0' should be placed anywhere that needs to have some initial indenting, '1' for the inner indent of the method, and a '2' for where the method name should be placed.
    ///  It should look something like: 0public void 2(){\r\n1throw new NotImplementedException();\r\n0}
    /// </param>
    /// <param name="methodName">The name of the method that is being generated.</param>
    /// <returns>A <see cref="TextEdit"/> that will place the formatted generated method within a @code block in the file.</returns>
    public static TextEdit CreateFormattedTextEdit(RazorCodeDocument code, string generatedMethod, string methodName)
    {
        var indentSize = code.GetCodeGenerationOptions().IndentSize;
        var csharpCodeBlock = code.GetSyntaxTree().Root.DescendantNodes().Where(e => e.Kind == SyntaxKind.CSharpCodeBlock && e.GetContent().StartsWith("@code") && e.GetContent().Contains('{') && e.GetContent().Contains('}')).FirstOrDefault();
        if (csharpCodeBlock is null)
        {
            // Generate the code within an @code block at the end of the file.
            var textWithCodeBlock = $"@code {{\r\n{GenerateMethodIndentService.AddIndentation(generatedMethod, indentSize, startingIndent: 0)}\r\n}}";
            var lastCharacterLocation = code.Source.Lines.GetLocation(code.Source.Length - 1);
            var insertCharacterIndex = 0;
            if (lastCharacterLocation.LineIndex == code.Source.Lines.Count - 1 && !IsLineEmpty(code.Source, lastCharacterLocation))
            {
                // The last line of the file is not empty so we need to place the code at the end of that line with an '\r\n' at the beginning.
                insertCharacterIndex = lastCharacterLocation.CharacterIndex + 1;
                textWithCodeBlock = $"\r\n{textWithCodeBlock}";
            }

            var eof = new Position(code.Source.Lines.Count - 1, insertCharacterIndex);
            return new TextEdit() { Range = new Range { Start = eof, End = eof }, NewText = textWithCodeBlock.Replace("2", methodName) };
        }

        var codeBlockStartLocation = code.Source.Lines.GetLocation(csharpCodeBlock.Span.Start);
        var codeBlockOpenBraceIndex = codeBlockStartLocation.AbsoluteIndex;
        while (code.Source[codeBlockOpenBraceIndex] != '{')
        {
            codeBlockOpenBraceIndex++;
        }

        var openBraceLocation = code.Source.Lines.GetLocation(codeBlockOpenBraceIndex);
        var codeBlockEndBraceLocation = code.Source.Lines.GetLocation(csharpCodeBlock.Span.End);
        var previousLine = code.Source.Lines.GetLocation(codeBlockEndBraceLocation.AbsoluteIndex - codeBlockEndBraceLocation.CharacterIndex - 1);
        var insertLineLocation = openBraceLocation.LineIndex == codeBlockEndBraceLocation.LineIndex || !IsLineEmpty(code.Source, previousLine) ? codeBlockEndBraceLocation : previousLine;
        var formattedGeneratedMethod = FormatMethodInCodeBlock(code.Source, codeBlockStartLocation, openBraceLocation, codeBlockEndBraceLocation, insertLineLocation, indentSize, generatedMethod);
        var insertPosition = new Position(insertLineLocation.LineIndex, openBraceLocation.LineIndex == codeBlockEndBraceLocation.LineIndex ? codeBlockEndBraceLocation.CharacterIndex - 1 : 0);
        return new TextEdit() { Range = new Range { Start = insertPosition, End = insertPosition }, NewText = formattedGeneratedMethod.Replace("2", methodName) };
    }

    private static string FormatMethodInCodeBlock(RazorSourceDocument source, SourceLocation codeBlockStartLocation, SourceLocation openBraceLocation, SourceLocation codeBlockEndLocation, SourceLocation insertLocation, int indentSize, string method)
    {
        var formattedGeneratedMethod = GenerateMethodIndentService.AddIndentation(method, indentSize, codeBlockStartLocation.CharacterIndex);
        if (openBraceLocation.LineIndex == codeBlockEndLocation.LineIndex)
        {
            // The @code block's '{' and '}' are on the same line, we'll need to add a new line to both the beginning and end of the generated code.
            return $"\r\n{formattedGeneratedMethod}\r\n";
        }

        if (insertLocation.LineIndex == codeBlockEndLocation.LineIndex)
        {
            // We will be inserting the code on the same line as the '}' of the code block. Make sure to add a new line to separate these.
            formattedGeneratedMethod += "\r\n";
        }

        if (insertLocation.LineIndex - 1 == openBraceLocation.LineIndex)
        {
            // There is no other code in the @code block, no need to continue formatting.
            return formattedGeneratedMethod;
        }

        // There is other code that exists in the @code block. Look at what is above the line we are going to insert at.
        // If there is code above it, we need to add a new line at the beginning the generated code.
        var previousLine = source.Lines.GetLocation(insertLocation.AbsoluteIndex - insertLocation.CharacterIndex - 1);
        if (!IsLineEmpty(source, previousLine))
        {
            formattedGeneratedMethod = $"\r\n{formattedGeneratedMethod}";
        }

        return formattedGeneratedMethod;
    }

    /// <summary>
    ///  Determines whether the line is empty.
    /// </summary>
    /// <param name="source">The RazorSourceDocument of where the line in question lives.</param>
    /// <param name="endLineLocation">
    ///  The <see cref="CodeAnalysis.SourceLocation"/> of the end of the line in question.
    /// </param>
    /// <returns>true if the line is empty, otherwise false.</returns>
    private static bool IsLineEmpty(RazorSourceDocument source, SourceLocation endLineLocation)
    {
        var beginningLineIndex = endLineLocation.AbsoluteIndex - endLineLocation.CharacterIndex;
        for(var i = endLineLocation.AbsoluteIndex; i >= beginningLineIndex; i--)
        {
            if (source[i] != '\r' && source[i] != '\n' && source[i] != ' ')
            {
                return false;
            }
        }

        return true;
    }
}
