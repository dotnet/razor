﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.Text;

public class StringTextSnapshot : ITextSnapshot2
{
    private readonly List<ITextSnapshotLine> _lines;
    private ITextBuffer _textBuffer;

    public static readonly StringTextSnapshot Empty = new(string.Empty);

    public StringTextSnapshot(string content) : this(content, versionNumber: 0)
    {
    }

    public StringTextSnapshot(string content, int versionNumber)
    {
        Content = content;
        _lines = new List<ITextSnapshotLine>();

        var start = 0;
        var delimiterIndex = 0;
        while (delimiterIndex != -1)
        {
            var delimiterLength = 2;
            delimiterIndex = Content.IndexOf("\r\n", start, StringComparison.Ordinal);

            if (delimiterIndex == -1)
            {
                delimiterLength = 1;
                for (var i = start; i < Content.Length; i++)
                {
                    if (SyntaxFacts.IsNewLine(content[i]))
                    {
                        delimiterIndex = i;
                        break;
                    }
                }
            }

            var nextLineStartIndex = delimiterIndex != -1 ? delimiterIndex + delimiterLength : Content.Length;

            var lineText = Content[start..nextLineStartIndex];
            _lines.Add(new SnapshotLine(lineText, start, this));

            start = nextLineStartIndex;

            Version = new TextVersion(versionNumber);
        }
    }

    public string Content { get; }

    public char this[int position] => Content[position];

    public ITextVersion Version { get; }

    public int Length => Content.Length;

    public ITextBuffer TextBuffer
    {
        get => _textBuffer;
        set
        {
            _textBuffer = value;
            ContentType = _textBuffer.ContentType;
        }
    }

    public IContentType ContentType { get; private set; }

    public int LineCount => _lines.Count;

    public IEnumerable<ITextSnapshotLine> Lines => _lines;

    public ITextImage TextImage => new StringTextImage(Content);

    public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) => Content.CopyTo(sourceIndex, destination, destinationIndex, count);

    public string GetText(int startIndex, int length) => Content.Substring(startIndex, length);

    public string GetText() => Content;

    public char[] ToCharArray(int startIndex, int length) => Content.ToCharArray();

    public ITextSnapshotLine GetLineFromPosition(int position)
    {
        var matchingLine = _lines.FirstOrDefault(line => line.Start + line.Length >= position);

        if (position < 0 || matchingLine is null)
        {
            throw new ArgumentOutOfRangeException();
        }

        return matchingLine;
    }

    public ITextSnapshotLine GetLineFromLineNumber(int lineNumber)
    {
        if (lineNumber < 0 || lineNumber >= _lines.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber));
        }

        return _lines[lineNumber];
    }

    public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode)
    {
        return new SnapshotTrackingPoint(position);
    }

    public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) => throw new NotImplementedException();

    public ITrackingSpan CreateTrackingSpan(VisualStudio.Text.Span span, SpanTrackingMode trackingMode) => throw new NotImplementedException();

    public ITrackingSpan CreateTrackingSpan(VisualStudio.Text.Span span, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) => throw new NotImplementedException();

    public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode) => throw new NotImplementedException();

    public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) => throw new NotImplementedException();

    public int GetLineNumberFromPosition(int position) => throw new NotImplementedException();

    public string GetText(VisualStudio.Text.Span span) => throw new NotImplementedException();

    public void Write(TextWriter writer, VisualStudio.Text.Span span) => throw new NotImplementedException();

    public void Write(TextWriter writer) => throw new NotImplementedException();

    public void SaveToFile(string filePath, bool replaceFile, Encoding encoding)
    {
        throw new NotImplementedException();
    }

    private class TextVersion : ITextVersion
    {
        public TextVersion(int versionNumber)
        {
            VersionNumber = versionNumber;
        }

        public INormalizedTextChangeCollection Changes { get; } = new TextChangeCollection();

        public int VersionNumber { get; }

        public ITextVersion Next => throw new NotImplementedException();

        public int Length => throw new NotImplementedException();

        public ITextBuffer TextBuffer => throw new NotImplementedException();

        public int ReiteratedVersionNumber => throw new NotImplementedException();

        public ITrackingSpan CreateCustomTrackingSpan(VisualStudio.Text.Span span, TrackingFidelityMode trackingFidelity, object customState, CustomTrackToVersion behavior) => throw new NotImplementedException();

        public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode) => throw new NotImplementedException();

        public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) => throw new NotImplementedException();

        public ITrackingSpan CreateTrackingSpan(VisualStudio.Text.Span span, SpanTrackingMode trackingMode) => throw new NotImplementedException();

        public ITrackingSpan CreateTrackingSpan(VisualStudio.Text.Span span, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) => throw new NotImplementedException();

        public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode) => throw new NotImplementedException();

        public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) => throw new NotImplementedException();

        private class TextChangeCollection : List<ITextChange>, INormalizedTextChangeCollection
        {
            public bool IncludesLineChanges => false;
        }
    }

    private class SnapshotTrackingPoint : ITrackingPoint
    {
        private readonly int _position;

        public SnapshotTrackingPoint(int position)
        {
            _position = position;
        }

        public ITextBuffer TextBuffer => throw new NotImplementedException();

        public PointTrackingMode TrackingMode => throw new NotImplementedException();

        public TrackingFidelityMode TrackingFidelity => throw new NotImplementedException();

        public char GetCharacter(ITextSnapshot snapshot) => throw new NotImplementedException();

        public SnapshotPoint GetPoint(ITextSnapshot snapshot) => throw new NotImplementedException();

        public int GetPosition(ITextSnapshot snapshot) => _position;

        public int GetPosition(ITextVersion version) => throw new NotImplementedException();
    }

    private class SnapshotLine : ITextSnapshotLine
    {
        private readonly string _contentWithLineBreak;
        private readonly string _content;

        public SnapshotLine(string contentWithLineBreak, int start, ITextSnapshot owner)
        {
            _contentWithLineBreak = contentWithLineBreak;
            _content = contentWithLineBreak;

            if (_content.EndsWith("\r\n", StringComparison.Ordinal))
            {
                _content = _content[..^2];
            }
            else if(_content.Length > 0 && SyntaxFacts.IsNewLine(_content[_content.Length - 1]))
            {
                _content = _content[..^1];
            }

            Start = new SnapshotPoint(owner, start);
            End = new SnapshotPoint(owner, start + _content.Length);
            Snapshot = owner;
            LineNumber = (owner as StringTextSnapshot)._lines.Count;
        }

        public ITextSnapshot Snapshot { get; }

        public SnapshotPoint Start { get; }

        public int Length => _content.Length;

        public int LengthIncludingLineBreak => _contentWithLineBreak.Length;

        public int LineBreakLength => _contentWithLineBreak.Length - _content.Length;

        public string GetText() => _content;

        public string GetLineBreakText() => _contentWithLineBreak[_content.Length..];

        public string GetTextIncludingLineBreak() => _contentWithLineBreak;

        public int LineNumber { get; }

        public SnapshotPoint End { get; }

        public SnapshotSpan Extent => throw new NotImplementedException();

        public SnapshotSpan ExtentIncludingLineBreak => throw new NotImplementedException();

        public SnapshotPoint EndIncludingLineBreak => throw new NotImplementedException();
    }
}
