// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO;
using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

internal sealed class SeekableTextReader : TextReader, ITextDocument
{
    private readonly RazorSourceDocument _sourceDocument;
    private readonly string _filePath;
    private int _position;
    private int _current;
    private SourceLocation _location;
    private (TextSpan Span, int LineIndex) _cachedLineInfo;

    public SeekableTextReader(string source, string filePath) : this(RazorSourceDocument.Create(source, Encoding.UTF8, RazorSourceDocumentProperties.Create(filePath, relativePath: null))) { }

    public SeekableTextReader(RazorSourceDocument source)
    {
        _sourceDocument = source;
        _filePath = source.FilePath;
        _cachedLineInfo = (new TextSpan(0, _sourceDocument.SourceText.Lines[0].Span.Length), 0);
        UpdateState();
    }

    public SourceLocation Location => _location;

    public int Length => _sourceDocument.SourceText.Length;

    public int Position
    {
        get { return _position; }
        set
        {
            if (_position != value)
            {
                _position = value;
                UpdateState();
            }
        }
    }

    public override int Read()
    {
        var c = _current;
        _position++;
        UpdateState();
        return c;
    }

    public override int Peek() => _current;

    private void UpdateState()
    {
        if (_cachedLineInfo.Span.Contains(_position))
        {
            _location = new SourceLocation(_filePath, _position, _cachedLineInfo.LineIndex, _position - _cachedLineInfo.Span.Start);
            _current = _sourceDocument.SourceText[_location.AbsoluteIndex];

            return;
        }

        if (_position < _sourceDocument.SourceText.Length)
        {
            if (_position >= _cachedLineInfo.Span.End)
            {
                // Try to avoid the GetLocation call by checking if the next line contains the position
                var nextLineIndex = _cachedLineInfo.LineIndex + 1;
                var nextLineLength = _sourceDocument.SourceText.Lines[nextLineIndex].Span.Length;
                var nextLineSpan = new TextSpan(_cachedLineInfo.Span.End, nextLineLength);

                if (nextLineSpan.Contains(_position))
                {
                    _cachedLineInfo = (nextLineSpan, nextLineIndex);
                    _location = new SourceLocation(_filePath, _position, nextLineIndex, _position - nextLineSpan.Start);
                    _current = _sourceDocument.SourceText[_location.AbsoluteIndex];

                    return;
                }
            }
            else
            {
                // Try to avoid the GetLocation call by checking if the previous line contains the position
                var prevLineIndex = _cachedLineInfo.LineIndex - 1;
                var prevLineLength = _sourceDocument.SourceText.Lines[prevLineIndex].Text.Length;
                var prevLineSpan = new TextSpan(_cachedLineInfo.Span.Start - prevLineLength, prevLineLength);

                if (prevLineSpan.Contains(_position))
                {
                    _cachedLineInfo = (prevLineSpan, prevLineIndex);
                    _location = new SourceLocation(_filePath, _position, prevLineIndex, _position - prevLineSpan.Start);
                    _current = _sourceDocument.SourceText[_location.AbsoluteIndex];

                    return;
                }
            }

            // The call to GetLocation is expensive
            _location = new SourceLocation(_sourceDocument.FilePath, _position, _sourceDocument.SourceText.Lines.GetLinePosition(_position));

            var lineLength = _sourceDocument.SourceText.Lines[_location.LineIndex].Span.Length;
            var lineSpan = new TextSpan(_position - _location.CharacterIndex, lineLength);
            _cachedLineInfo = (lineSpan, _location.LineIndex);

            _current = _sourceDocument.SourceText[_location.AbsoluteIndex];

            return;
        }

        if (_sourceDocument.SourceText.Length == 0)
        {
            _location = SourceLocation.Zero;
            _current = -1;

            return;
        }

        var lineNumber = _sourceDocument.SourceText.Lines.Count - 1;
        _location = new SourceLocation(_filePath, Length, lineNumber, _sourceDocument.SourceText.Lines[lineNumber].Span.Length);

        _current = -1;
    }
}
