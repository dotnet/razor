// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
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
    ///  The skeleton of the generated method where a '0' should be placed anywhere that needs to have some initial indenting, '1' for the inner indent of the method, and a '$$MethodName$$' for where the method name should be placed.
    ///  It should look something like: 0public void $$MethodName$$(){\r\n1throw new NotImplementedException();\r\n0}
    /// </param>
    /// <param name="methodName">The name of the method that is being generated.</param>
    /// <param name="options">The <see cref="RazorLSPOptions"/> that contains information about indentation.</param>
    /// <returns>A <see cref="TextEdit"/> that will place the formatted generated method within a @code block in the file.</returns>
    public static TextEdit CreateFormattedTextEdit(RazorCodeDocument code, string generatedMethod, string methodName, RazorLSPOptions options)
    {
        var codeBlocks = code.GetSyntaxTree().Root.DescendantNodes()
            .Where(e => e is CSharpCodeBlockSyntax block && block.Children.FirstOrDefault() is RazorDirectiveSyntax directive && directive.Body is RazorDirectiveBodySyntax directiveBody && directiveBody.Keyword.GetContent().Equals("code"))
            .Select(e => ((RazorDirectiveSyntax)((CSharpCodeBlockSyntax)e).Children.FirstOrDefault()).Body)
            .FirstOrDefault();
        var csharpCodeBlock = (codeBlocks as RazorDirectiveBodySyntax)?.CSharpCode;
        if (csharpCodeBlock is null || !csharpCodeBlock.Children.TryGetOpenBraceNode(out var openBrace) || !csharpCodeBlock.Children.TryGetCloseBraceNode(out var closeBrace))
        {
            // Generate the code within an @code block at the end of the file.
            var textWithCodeBlock = $"@code {{{Environment.NewLine}{FormattingUtilities.AddIndentationToMethod(generatedMethod, options.InsertSpaces, options.TabSize, startingIndent: 0)}{Environment.NewLine}}}";
            var lastCharacterLocation = code.Source.Lines.GetLocation(code.Source.Length - 1);
            var insertCharacterIndex = 0;
            if (lastCharacterLocation.LineIndex == code.Source.Lines.Count - 1 && !IsLineEmpty(code.Source, lastCharacterLocation))
            {
                // The last line of the file is not empty so we need to place the code at the end of that line with a new line at the beginning.
                insertCharacterIndex = lastCharacterLocation.CharacterIndex + 1;
                textWithCodeBlock = $"{Environment.NewLine}{textWithCodeBlock}";
            }

            var eof = new Position(code.Source.Lines.Count - 1, insertCharacterIndex);
            return new TextEdit() { Range = new Range { Start = eof, End = eof }, NewText = textWithCodeBlock.Replace("$$MethodName$$", methodName) };
        }

        var openBraceLocation = openBrace.GetSourceLocation(code.Source);
        var closeBraceLocation = closeBrace.GetSourceLocation(code.Source);
        var previousLine = code.Source.Lines.GetLocation(closeBraceLocation.AbsoluteIndex - closeBraceLocation.CharacterIndex - 1);
        var insertLineLocation = openBraceLocation.LineIndex == closeBraceLocation.LineIndex || !IsLineEmpty(code.Source, previousLine) ? closeBraceLocation : previousLine;
        int codeBlockBeginningIndex = csharpCodeBlock.GetSourceLocation(code.Source).CharacterIndex - 5;
        var formattedGeneratedMethod = FormatMethodInCodeBlock(code.Source, codeBlockBeginningIndex, openBraceLocation.LineIndex, closeBraceLocation.LineIndex, insertLineLocation, options, generatedMethod);
        var insertPosition = new Position(insertLineLocation.LineIndex, openBraceLocation.LineIndex == closeBraceLocation.LineIndex ? closeBraceLocation.CharacterIndex : 0);
        return new TextEdit() { Range = new Range { Start = insertPosition, End = insertPosition }, NewText = formattedGeneratedMethod.Replace("$$MethodName$$", methodName) };
    }

    private static string FormatMethodInCodeBlock(RazorSourceDocument source, int codeBlockStartIndex, int openBraceLineIndex, int closeBraceLineIndex, SourceLocation insertLocation, RazorLSPOptions options, string method)
    {
        var formattedGeneratedMethod = FormattingUtilities.AddIndentationToMethod(method, options.InsertSpaces, options.TabSize, codeBlockStartIndex);
        if (openBraceLineIndex == closeBraceLineIndex)
        {
            // The @code block's '{' and '}' are on the same line, we'll need to add a new line to both the beginning and end of the generated code.
            return $"{Environment.NewLine}{formattedGeneratedMethod}{Environment.NewLine}";
        }

        if (insertLocation.LineIndex == closeBraceLineIndex)
        {
            // We will be inserting the code on the same line as the '}' of the code block. Make sure to add a new line to separate these.
            formattedGeneratedMethod += Environment.NewLine;
        }

        if (insertLocation.LineIndex - 1 == openBraceLineIndex)
        {
            // There is no other code in the @code block, no need to continue formatting.
            return formattedGeneratedMethod;
        }

        // There is other code that exists in the @code block. Look at what is above the line we are going to insert at.
        // If there is code above it, we need to add a new line at the beginning the generated code.
        var previousLine = source.Lines.GetLocation(insertLocation.AbsoluteIndex - insertLocation.CharacterIndex - 1);
        if (!IsLineEmpty(source, previousLine))
        {
            formattedGeneratedMethod = $"{Environment.NewLine}{formattedGeneratedMethod}";
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
            if (!char.IsWhiteSpace(source[i]))
            {
                return false;
            }
        }

        return true;
    }
}
