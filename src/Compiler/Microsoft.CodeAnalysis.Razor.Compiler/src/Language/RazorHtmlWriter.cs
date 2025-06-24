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
    private SourceSpan _lastOriginalSourceSpan = SourceSpan.Undefined;
    private SourceSpan _lastGeneratedSourceSpan = SourceSpan.Undefined;

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

        if (_lastGeneratedSourceSpan != SourceSpan.Undefined)
        {
            // If we finished up with a source mapping being tracked, then add it to the list now

            Debug.Assert(_lastOriginalSourceSpan != SourceSpan.Undefined);

            var sourceMapping = new SourceMapping(_lastOriginalSourceSpan, _lastGeneratedSourceSpan);
            _sourceMappings.Add(sourceMapping);

            _lastOriginalSourceSpan = SourceSpan.Undefined;
            _lastGeneratedSourceSpan = SourceSpan.Undefined;
        }
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
        var content = token.Content;
        if (_isWritingHtml)
        {
            WriteCSharpContentPlaceholder();

            var source = token.GetSourceSpan(_source);

            // No point source mapping an empty token
            if (source.Length > 0)
            {
                var generatedLocation = new SourceSpan(_codeWriter.Location, source.Length);

                if (_lastGeneratedSourceSpan == SourceSpan.Undefined)
                {
                    // Not tracking any current source mapping, so start tracking one

                    Debug.Assert(_lastOriginalSourceSpan == SourceSpan.Undefined);

                    _lastGeneratedSourceSpan = generatedLocation;
                    _lastOriginalSourceSpan = source;
                }
                else if (generatedLocation.AbsoluteIndex == _lastGeneratedSourceSpan.AbsoluteIndex + _lastGeneratedSourceSpan.Length &&
                    source.AbsoluteIndex == _lastOriginalSourceSpan.AbsoluteIndex + _lastOriginalSourceSpan.Length &&
                    generatedLocation.LineCount <= 1 &&
                    source.LineCount <= 1)
                {
                    // We're tracking a span, and it ends at the same spot the current token starts, so lets just extend the existing
                    // source mapping we're tracking, so we produce a minimal set
                    // eg, in "<div>" there are three tokens that are written (open angle bracket, tag name, close angle bracket)
                    //     but having three source mappings in unnecessarily complex
                    _lastGeneratedSourceSpan = _lastGeneratedSourceSpan.With(length: _lastGeneratedSourceSpan.Length + source.Length, endCharacterIndex: source.EndCharacterIndex);
                    _lastOriginalSourceSpan = _lastOriginalSourceSpan.With(length: _lastOriginalSourceSpan.Length + source.Length, endCharacterIndex: source.EndCharacterIndex);
                }
                else
                {
                    // New span is not directly next to the previous one, so add the previous to the list, and start tracking the new one
                    var sourceMapping = new SourceMapping(_lastOriginalSourceSpan, _lastGeneratedSourceSpan);
                    _sourceMappings.Add(sourceMapping);

                    _lastOriginalSourceSpan = source;
                    _lastGeneratedSourceSpan = generatedLocation;
                }
            }

            // If we're in HTML context, append the content directly.
            _codeWriter.Write(content);
            return;
        }

        if (_lastGeneratedSourceSpan != SourceSpan.Undefined)
        {
            // If we were tracking a source mapping span before now, add it to the list. Importantly there are cases
            // where there are 0-length C# nodes, so this step is very important if the source mappings are to match
            // the syntax tree.

            Debug.Assert(_lastOriginalSourceSpan != SourceSpan.Undefined);

            var sourceMapping = new SourceMapping(_lastOriginalSourceSpan, _lastGeneratedSourceSpan);
            _sourceMappings.Add(sourceMapping);

            _lastGeneratedSourceSpan = SourceSpan.Undefined;
            _lastOriginalSourceSpan = SourceSpan.Undefined;
        }

        // We're in non-HTML context. Let's replace all non-whitespace chars with a tilde(~).
        foreach (var c in content)
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
