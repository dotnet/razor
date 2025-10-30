// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.TextDifferencing;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.Formatting;

internal static class FormattingUtilities
{
    public const string Indent = "$$Indent$$";
    public const string InitialIndent = "$$InitialIndent$$";

    /// <summary>
    ///  Adds indenting to the method.
    /// </summary>
    /// <param name="method">
    ///  The method to add indenting to. The method should be marked with <see cref="Indent"/> where an indent is wanted
    /// </param>
    /// <param name="tabSize">
    ///  The indentation size
    /// </param>
    /// <param name="insertSpaces">
    ///  Use spaces for indentation.
    /// </param>
    /// <param name="startingIndent">
    ///  The size of the any existing indent.
    /// </param>
    /// <returns>The indented method.</returns>
    public static string AddIndentationToMethod(string method, int tabSize, bool insertSpaces, int startingIndent)
    {
        var initialIndent = GetIndentationString(startingIndent, insertSpaces, tabSize);
        var indent = GetIndentationString(tabSize, insertSpaces, tabSize);
        return method.Replace(InitialIndent, initialIndent).Replace(Indent, indent);
    }

    /// <summary>
    ///  Adds indenting to the method.
    /// </summary>
    /// <param name="method">
    ///  The method to add indenting to. The method should be marked with <see cref="Indent"/> where an indent is wanted
    ///  and <see cref="InitialIndent"/> where some initial indent is needed.
    /// </param>
    /// <param name="tabSize">
    ///  The indentation size
    /// </param>
    /// <param name="insertSpaces">
    /// Use spaces for indentation.
    /// </param>
    /// <param name="startAbsoluteIndex">
    ///  The absolute index of the beginning of the class in the C# file the method will be added to.
    /// </param>
    /// <param name="numCharacterBefore">
    ///  The number of characters on the line before where startAbsoluteIndex is in the source.
    /// </param>
    /// <param name="source">
    ///  The contents of the C# file.
    /// </param>
    /// <returns>The indented method.</returns>
    public static string AddIndentationToMethod(string method, int tabSize, bool insertSpaces, int startAbsoluteIndex, int numCharacterBefore, SourceText source)
    {
        var startingIndent = 0;
        for (var i = 1; i <= numCharacterBefore; i++)
        {
            if (source[startAbsoluteIndex - i] == '\t')
            {
                startingIndent += tabSize;
            }
            else
            {
                startingIndent++;
            }
        }

        return AddIndentationToMethod(method, tabSize, insertSpaces, startingIndent);
    }

    /// <summary>
    ///  Adds indenting to the method.
    /// </summary>
    /// <param name="method">
    ///  The method to add indenting to. The method should be marked with <see cref="Indent"/> where an indent is wanted
    ///  and <see cref="InitialIndent"/> where some initial indent is needed.
    /// </param>
    /// <param name="tabSize">
    ///  The indentation size
    /// </param>
    /// <param name="insertSpaces"></param>
    /// <param name="startAbsoluteIndex">
    ///  The absolute index of the beginning of the code block in the Razor file where the method will be added to.
    /// </param>
    /// <param name="numCharacterBefore">
    ///  The number of characters on the line before where startAbsoluteIndex is in the source.
    /// </param>
    /// <param name="source">
    ///  The <see cref="RazorSourceDocument"/> of the razor file the method is being added to.
    /// </param>
    /// <returns>The indented method.</returns>
    public static string AddIndentationToMethod(string method, int tabSize, bool insertSpaces, int startAbsoluteIndex, int numCharacterBefore, RazorSourceDocument source)
    {
        var startingIndent = 0;
        for (var i = 1; i <= numCharacterBefore; i++)
        {
            if (source.Text[startAbsoluteIndex - i] == '\t')
            {
                startingIndent += tabSize;
            }
            else
            {
                startingIndent++;
            }
        }

        return AddIndentationToMethod(method, tabSize, insertSpaces, startingIndent);
    }

    /// <summary>
    /// Counts the number of non-whitespace characters in a given span of text.
    /// </summary>
    /// <param name="text">The source text</param>
    /// <param name="start">Inclusive position for where to start counting</param>
    /// <param name="endExclusive">Exclusive for where to stop counting</param>
    public static int CountNonWhitespaceChars(SourceText text, int start, int endExclusive)
    {
        var count = 0;
        for (var i = start; i < endExclusive; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                count++;
            }
        }

        return count;
    }

    public static int GetIndentationLevel(TextLine line, int firstNonWhitespaceCharacterPosition, bool insertSpaces, int tabSize, out string additionalIndentation)
    {
        if (firstNonWhitespaceCharacterPosition > line.End)
        {
            throw new ArgumentOutOfRangeException(nameof(firstNonWhitespaceCharacterPosition), "The first non-whitespace character position must be within the line.");
        }

        // For spaces, the actual indentation needs to be divided by the tab size to get the level, and additional is the remainder
        var currentIndentationWidth = firstNonWhitespaceCharacterPosition - line.Start;
        if (insertSpaces)
        {
            var indentationLevel = currentIndentationWidth / tabSize;
            additionalIndentation = new string(' ', currentIndentationWidth % tabSize);
            return indentationLevel;
        }

        // For tabs, we just count the tabs, and additional is any spaces at the end.
        var tabCount = 0;
        var text = line.Text.AssumeNotNull();
        for (var i = line.Start; i < firstNonWhitespaceCharacterPosition; i++)
        {
            if (text[i] == '\t')
            {
                tabCount++;
            }
            else
            {
                Debug.Assert(text[i] == ' ');
                additionalIndentation = text.GetSubTextString(TextSpan.FromBounds(i, firstNonWhitespaceCharacterPosition));
                return tabCount;
            }
        }

        additionalIndentation = "";
        return tabCount;
    }

    /// <summary>
    /// Given a <paramref name="indentation"/> amount of characters, generate a string representing the configured indentation.
    /// </summary>
    /// <param name="indentation">An amount of characters to represent the indentation.</param>
    /// <param name="insertSpaces">Whether spaces are used for indentation.</param>
    /// <param name="tabSize">The size of a tab and indentation.</param>
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

    /// <summary>
    /// Unindents a span of text with a few caveats:
    ///
    /// 1. This assumes consistency in tabs/spaces for starting whitespace per line
    /// 2. This doesn't format the text, just attempts to remove leading whitespace in a uniform way
    /// 3. It will never remove non-whitespace
    ///
    /// This was made with extracting code into a new file in mind because it's not trivial to format that text and make
    /// sure the indentation is right. Use with caution.
    /// </summary>
    public static void NaivelyUnindentSubstring(SourceText text, TextSpan extractionSpan, System.Text.StringBuilder builder)
    {
        var extractedText = text.GetSubTextString(extractionSpan);
        var range = text.GetRange(extractionSpan);
        if (range.Start.Line == range.End.Line)
        {
            builder.Append(extractedText);
            return;
        }

        var extractedTextSpan = extractedText.AsSpan();
        var indentation = GetNormalizedIndentation(text, extractionSpan);

        foreach (var lineRange in GetLineRanges(extractedText))
        {
            var lineSpan = extractedTextSpan[lineRange];
            lineSpan = UnindentLine(lineSpan, indentation);

            foreach (var c in lineSpan)
            {
                builder.Append(c);
            }
        }

        //
        // Local Methods
        //

        static ReadOnlySpan<char> UnindentLine(ReadOnlySpan<char> line, int indentation)
        {
            var startCharacter = 0;
            while (startCharacter < indentation && IsWhitespace(line[startCharacter]))
            {
                startCharacter++;
            }

            return line[startCharacter..];
        }

        // Gets the smallest indentation of all the lines in a given span
        static int GetNormalizedIndentation(SourceText sourceText, TextSpan span)
        {
            var indentation = int.MaxValue;
            foreach (var line in sourceText.Lines)
            {
                if (!span.OverlapsWith(line.Span))
                {
                    continue;
                }

                indentation = Math.Min(indentation, GetIndentation(line));
            }

            return indentation;
        }

        static int GetIndentation(TextLine line)
        {
            if (line.Text is null)
            {
                return 0;
            }

            var indentation = 0;
            for (var position = line.Span.Start; position < line.Span.End; position++)
            {
                var c = line.Text[position];
                if (!IsWhitespace(c))
                {
                    break;
                }

                indentation++;
            }

            return indentation;
        }

        static bool IsWhitespace(char c)
            => c == ' ' || c == '\t';

        static ImmutableArray<Range> GetLineRanges(string text)
        {
            using var builder = new PooledArrayBuilder<Range>();
            var start = 0;
            var end = text.IndexOf('\n');
            while (true)
            {
                if (end == -1)
                {
                    builder.Add(new(start, text.Length));
                    break;
                }

                // end + 1 to include the new line
                builder.Add(new(start, end + 1));
                start = end + 1;
                if (start == text.Length)
                {
                    break;
                }

                end = text.IndexOf('\n', start);
            }

            return builder.ToImmutableAndClear();
        }
    }

    /// <inheritdoc cref="FixHtmlTextChanges(SourceText, ImmutableArray{TextChange})" />
    public static TextEdit[] FixHtmlTextEdits(SourceText htmlSourceText, TextEdit[] edits)
    {
        // Avoid computing a minimal diff if we don't need to
        if (!edits.Any(static e => e.NewText.Contains('~')))
            return edits;

        var changes = edits.SelectAsArray(htmlSourceText.GetTextChange);

        var fixedChanges = FixHtmlTextChanges(htmlSourceText, changes);
        return [.. fixedChanges.Select(htmlSourceText.GetTextEdit)];
    }

    /// <inheritdoc cref="FixHtmlTextChanges(SourceText, ImmutableArray{TextChange})" />
    public static SumType<TextEdit, AnnotatedTextEdit>[] FixHtmlTextEdits(SourceText htmlSourceText, SumType<TextEdit, AnnotatedTextEdit>[] edits)
    {
        // Avoid computing a minimal diff if we don't need to
        if (!edits.Any(static e => ((TextEdit)e).NewText.Contains('~')))
            return edits;

        var changes = edits.SelectAsArray(e => htmlSourceText.GetTextChange((TextEdit)e));

        var fixedChanges = FixHtmlTextChanges(htmlSourceText, changes);
        return [.. fixedChanges.Select(htmlSourceText.GetTextEdit)];
    }

    /// <summary>
    /// Computes a set of changes at word granularity to avoid Html edits that want to change C#
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is a lot of uncertainty when we're dealing with edits that come from the Html formatter
    /// because we are not responsible for it. It could make all sorts of strange edits, and it could
    /// structure those edits is all sorts of ways. eg, it could have individual character edits, or
    /// it could have a single edit that replaces a whole section of text, or the whole document.
    /// Since the Html formatter doesn't understand Razor, and in fact doesn't even format the actual
    /// Razor document directly (all C# is replaced), we have to be selective about what edits we will
    /// actually use, but being selective is tricky because we might be missing some intentional edits
    /// that the formatter made.
    /// </para>
    ///
    /// <para>
    /// To solve this, and work around various issues due to the Html formatter seeing a much simpler
    /// document that we are actually dealing with, the first thing we do is take the changes it suggests
    /// and apply them to the document it saw, then use our own algorithm to produce a set of changes
    /// that more closely match what we want to get out of it. Specifically, we only want to see changes
    /// to whitespace, or Html, not changes that include C#. Fortunately since we encode all C# as tildes
    /// it means we can do a word-based diff, and all C# will essentially be equal to all other C#, so
    /// won't appear in the diff.
    /// </para>
    ///
    /// <para>
    /// So we end up with a set of changes that are only ever to whitespace, or legitimate Html (though
    /// in reality the formatter doesn't change that anyway).
    /// </para>
    /// </remarks>
    public static ImmutableArray<TextChange> FixHtmlTextChanges(SourceText htmlSourceText, ImmutableArray<TextChange> changes)
    {
        // Avoid computing a minimal diff if we don't need to. Slightly wasteful if we've come from one
        // of the other overloads, but worth it if we haven't (and worth it for them to validate before
        // doing the work to convert edits to changes).
        if (!changes.Any(static e => e.NewText?.Contains('~') ?? false))
            return changes;

        var changedText = htmlSourceText.WithChanges(changes);
        return SourceTextDiffer.GetMinimalTextChanges(htmlSourceText, changedText, DiffKind.Word);
    }
}
