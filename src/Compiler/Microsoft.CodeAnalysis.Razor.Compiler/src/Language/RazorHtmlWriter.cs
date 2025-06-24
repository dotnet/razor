// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language;

// We want to generate a HTML document that contains only pure HTML.
// So we want replace all non-HTML content with whitespace.
// Ideally we should just use ClassifiedSpans to generate this document but
// not all characters in the document are included in the ClassifiedSpans.
internal sealed class RazorHtmlWriter : SyntaxWalker
{
    // 32 '~' characters followed by comment start and end text.
    private const string KnownText = "~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~/**/";

    private static ReadOnlyMemory<char> Tildes => KnownText.AsMemory(0, 32);
    private static ReadOnlyMemory<char> CommentStart => KnownText.AsMemory(32, 2);
    private static ReadOnlyMemory<char> CommentEnd => KnownText.AsMemory(34, 2);

    private readonly RazorSourceDocument _source;
    private readonly CodeWriter _codeWriter;
    private readonly ImmutableArray<SourceMapping>.Builder _sourceMappings;

    private bool _isWritingHtml;
    private (SourceSpan Original, SourceSpan Generated)? _lastSpans;

    // Rather than writing out C# characters as we find them (as '~') we keep a count so that consecutive characters
    // can be written as a block, allowing any block of 4 characters or more to be written as a comment (ie '/**/`)
    // which takes pressure off the TypeScript/JavaScript compiler. Doing this per token means we can end up with
    // "@className" being written as '~/*~~~~~*/', which means Html formatting will insert a space which breaks things.
    private int _tildesToWrite;

    private RazorHtmlWriter(RazorSourceDocument source, CodeWriter codeWriter, ImmutableArray<SourceMapping>.Builder sourceMappings)
    {
        _source = source;
        _codeWriter = codeWriter;
        _sourceMappings = sourceMappings;
        _isWritingHtml = true;
    }

    public static RazorHtmlDocument GetHtmlDocument(RazorCodeDocument codeDocument)
    {
        var source = codeDocument.Source;
        var options = codeDocument.CodeGenerationOptions;

        using var _ = ArrayBuilderPool<SourceMapping>.GetPooledObject(out var sourceMappings);
        using var codeWriter = new CodeWriter(options);

        var htmlWriter = new RazorHtmlWriter(source, codeWriter, sourceMappings);
        var syntaxTree = codeDocument.GetRequiredSyntaxTree();

        htmlWriter.Visit(syntaxTree);

        var text = codeWriter.GetText();

        Debug.Assert(
            source.Text.Length == text.Length,
            $"The backing HTML document should be the same length as the original document. Expected: {source.Text.Length} Actual: {text.Length}");

        return new RazorHtmlDocument(codeDocument, text, sourceMappings.ToImmutableAndClear());
    }

    private void Visit(RazorSyntaxTree syntaxTree)
    {
        Visit(syntaxTree.Root);

        WriteCSharpContentPlaceholder();

        // If we finished up with a source mapping being tracked, then add it to the list now
        AddLastSourceMappingAndClear();
    }

    private void AddLastSourceMappingAndClear()
    {
        if (_lastSpans is var (original, generated))
        {
            AddSourceMapping(original, generated);
            _lastSpans = null;
        }
    }

    private void AddSourceMapping(SourceSpan original, SourceSpan generated)
    {
        var sourceMapping = new SourceMapping(original, generated);
        _sourceMappings.Add(sourceMapping);
    }

    public override void VisitRazorCommentBlock(RazorCommentBlockSyntax node)
    {
        using (IsNotHtml())
        {
            base.VisitRazorCommentBlock(node);
        }
    }

    public override void VisitRazorMetaCode(RazorMetaCodeSyntax node)
    {
        using (IsNotHtml())
        {
            base.VisitRazorMetaCode(node);
        }
    }

    public override void VisitMarkupTransition(MarkupTransitionSyntax node)
    {
        using (IsNotHtml())
        {
            base.VisitMarkupTransition(node);
        }
    }

    public override void VisitCSharpTransition(CSharpTransitionSyntax node)
    {
        using (IsNotHtml())
        {
            base.VisitCSharpTransition(node);
        }
    }

    public override void VisitCSharpEphemeralTextLiteral(CSharpEphemeralTextLiteralSyntax node)
    {
        using (IsNotHtml())
        {
            base.VisitCSharpEphemeralTextLiteral(node);
        }
    }

    public override void VisitCSharpExpressionLiteral(CSharpExpressionLiteralSyntax node)
    {
        using (IsNotHtml())
        {
            base.VisitCSharpExpressionLiteral(node);
        }
    }

    public override void VisitCSharpStatementLiteral(CSharpStatementLiteralSyntax node)
    {
        using (IsNotHtml())
        {
            base.VisitCSharpStatementLiteral(node);
        }
    }

    public override void VisitMarkupStartTag(MarkupStartTagSyntax node)
    {
        using (IsHtml())
        {
            base.VisitMarkupStartTag(node);
        }
    }

    public override void VisitMarkupEndTag(MarkupEndTagSyntax node)
    {
        using (IsHtml())
        {
            base.VisitMarkupEndTag(node);
        }
    }

    public override void VisitMarkupTagHelperStartTag(MarkupTagHelperStartTagSyntax node)
    {
        using (IsHtml())
        {
            base.VisitMarkupTagHelperStartTag(node);
        }
    }

    public override void VisitMarkupTagHelperEndTag(MarkupTagHelperEndTagSyntax node)
    {
        using (IsHtml())
        {
            base.VisitMarkupTagHelperEndTag(node);
        }
    }

    public override void VisitMarkupEphemeralTextLiteral(MarkupEphemeralTextLiteralSyntax node)
    {
        using (IsHtml())
        {
            base.VisitMarkupEphemeralTextLiteral(node);
        }
    }

    public override void VisitMarkupTextLiteral(MarkupTextLiteralSyntax node)
    {
        using (IsHtml())
        {
            base.VisitMarkupTextLiteral(node);
        }
    }

    public override void VisitUnclassifiedTextLiteral(UnclassifiedTextLiteralSyntax node)
    {
        using (IsHtml())
        {
            base.VisitUnclassifiedTextLiteral(node);
        }
    }

    public override void VisitToken(SyntaxToken token)
    {
        base.VisitToken(token);
        WriteToken(token);
    }

    private readonly ref struct WriterStateSaver
    {
        private readonly RazorHtmlWriter _writer;
        private readonly bool _oldIsWritingHtml;

        public WriterStateSaver(RazorHtmlWriter writer, bool isWritingHtml)
        {
            _writer = writer;
            _oldIsWritingHtml = writer._isWritingHtml;
            writer._isWritingHtml = isWritingHtml;
        }

        public void Dispose()
        {
            _writer._isWritingHtml = _oldIsWritingHtml;
        }
    }

    private WriterStateSaver IsHtml()
        => new(this, isWritingHtml: true);

    private WriterStateSaver IsNotHtml()
        => new(this, isWritingHtml: false);

    private void WriteToken(SyntaxToken token)
    {
        if (_isWritingHtml)
        {
            WriteHtmlToken(token);
            return;
        }

        // If we were tracking a source mapping span before now, add it to the list. Importantly there are cases
        // where there are 0-length C# nodes, so this step is very important if the source mappings are to match
        // the syntax tree.
        AddLastSourceMappingAndClear();

        // We're in non-HTML context. Let's replace all non-whitespace chars with a tilde(~).
        foreach (var c in token.Content)
        {
            if (char.IsWhiteSpace(c))
            {
                WriteCSharpContentPlaceholder();
                _codeWriter.Write(c.ToString());
            }
            else
            {
                _tildesToWrite++;
            }
        }
    }

    private void WriteHtmlToken(SyntaxToken token)
    {
        var content = token.Content;
        if (content.Length == 0)
        {
            // If the token is empty, we don't need to do anything further.
            return;
        }

        WriteCSharpContentPlaceholder();

        var newOriginal = token.GetSourceSpan(_source);
        var newGenerated = new SourceSpan(_codeWriter.Location, newOriginal.Length);

        _codeWriter.Write(content);

        // If we're currently tracking a source mapping, we need to check if the new token is adjacent to the last one.
        // If so, we can extend the existing source mapping to include the new token.
        // If not, we need to add the last source mapping to the list and start a new one for the current token.
        if (_lastSpans is var (lastOriginal, lastGenerated))
        {
            if (newGenerated.LineCount <= 1 && TouchesLastSpan(newGenerated, lastGenerated) &&
                newOriginal.LineCount <= 1 && TouchesLastSpan(newOriginal, lastOriginal))
            {
                _lastSpans = (
                    ExtendSpan(lastOriginal, newOriginal.Length, newOriginal.EndCharacterIndex),
                    ExtendSpan(lastGenerated, newOriginal.Length, newOriginal.EndCharacterIndex)
                );

                return;
            }

            // The new span is not directly next to the previous one, so add the previous to the list.
            AddSourceMapping(lastOriginal, lastGenerated);
        }

        // Start tracking the new span.
        _lastSpans = (newOriginal, newGenerated);
    }

    /// <summary>
    ///  Returns <see langword="true"/> if the new span starts after the last span.
    /// </summary>
    private static bool TouchesLastSpan(SourceSpan newSpan, SourceSpan lastSpan)
        => newSpan.AbsoluteIndex == lastSpan.AbsoluteIndex + lastSpan.Length;

    private static SourceSpan ExtendSpan(SourceSpan span, int length, int endCharacterIndex)
        => new(span.FilePath, span.AbsoluteIndex, span.LineIndex, span.CharacterIndex, length: span.Length + length, span.LineCount, endCharacterIndex);

    private void WriteCSharpContentPlaceholder()
    {
        var tildesToWrite = _tildesToWrite;

        if (tildesToWrite == 0)
        {
            // Nothing to write, so just return
            return;
        }

        _tildesToWrite = 0;

        var writeComment = false;

        // When writing 4 or more tildes, we write them as a comment to relieve pressure on the JS/TS compiler.

        if (tildesToWrite >= 4)
        {
            // SPECIAL CASE: If the next character is a greater than symbol ('>'), we don't write a comment because
            // the forward slash in the comment would be interpreted as the end of a tag, resulting in incorrect HTML.
            // For example, `<div class=@className>` should not be written as `<div class=/*~~~~~*/>`.

            var nextIndex = _codeWriter.Location.AbsoluteIndex + tildesToWrite;
            var text = _source.Text;

            Debug.Assert(nextIndex <= text.Length, "The next index should not exceed the length of the source text.");

            if (nextIndex >= text.Length || text[nextIndex] != '>')
            {
                // We can write a comment
                writeComment = true;
                tildesToWrite -= 4; // We need to reserve 4 characters for the comment start and end
            }
        }

        if (writeComment)
        {
            _codeWriter.Write(CommentStart);
        }

        while (tildesToWrite > 0)
        {
            var tildes = tildesToWrite < Tildes.Length
                ? Tildes[..tildesToWrite]
                : Tildes;

            _codeWriter.Write(tildes);
            tildesToWrite -= tildes.Length;
        }

        if (writeComment)
        {
            _codeWriter.Write(CommentEnd);
        }
    }
}
