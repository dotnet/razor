// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Moq;
using Xunit;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    public class DefaultLSPEditorServiceTest
    {
        [Fact]
        public void ExtractsCursorPlaceholder_AppliesEditsCorrectly()
        {
            // Arrange
            var expectedCursorPosition = new Position(2, 10);
            var snapshot = new TestTextSnapshot($@"
@{{
    <text>{LanguageServerConstants.CursorPlaceholderString}</text>
}}");
            var edits = new[]
            {
                new TextEdit()
                {
                    NewText = $"{LanguageServerConstants.CursorPlaceholderString}</text>",
                    Range = new Range() { Start = expectedCursorPosition, End = expectedCursorPosition },
                }
            };

            // Act
            var cursorPosition = DefaultLSPEditorService.ExtractCursorPlaceholder(snapshot, edits);

            // Assert
            Assert.Equal(expectedCursorPosition, cursorPosition);
        }

        [Fact]
        public void ExtractsCursorPlaceholder_MultipleEdits_AppliesEditsCorrectly()
        {
            // Arrange
            var expectedCursorPosition = new Position(2, 10);
            var snapshot = new TestTextSnapshot($@"
@{{
    <text>{LanguageServerConstants.CursorPlaceholderString}</text>
}}");
            var edits = new[]
            {
                new TextEdit()
                {
                    NewText = $"unrelated Edit",
                    Range = new Range() { Start = new Position(0, 0), End = new Position(0, 1) },
                },
                new TextEdit()
                {
                    NewText = $"{LanguageServerConstants.CursorPlaceholderString}</text>",
                    Range = new Range() { Start = expectedCursorPosition, End = expectedCursorPosition },
                }
            };

            // Act
            var cursorPosition = DefaultLSPEditorService.ExtractCursorPlaceholder(snapshot, edits);

            // Assert
            Assert.Equal(expectedCursorPosition, cursorPosition);
        }

        private class TestTextSnapshot : ITextSnapshot
        {
            private readonly StringTextSnapshot _inner;

            public TestTextSnapshot(string text)
            {
                _inner = new StringTextSnapshot(text);
                var buffer = new Mock<ITextBuffer>();
                buffer.Setup(b => b.CreateEdit()).Returns(Mock.Of<ITextEdit>(e => e.Snapshot == _inner));
                _inner.TextBuffer = buffer.Object;
            }

            public char this[int position] => _inner[position];

            public ITextBuffer TextBuffer => _inner.TextBuffer;

            public IContentType ContentType => throw new NotImplementedException();

            public ITextVersion Version => _inner.Version;

            public int Length => _inner.Length;

            public int LineCount => _inner.GetLineFromPosition(_inner.Length - 1).LineNumber;

            public IEnumerable<ITextSnapshotLine> Lines => throw new NotImplementedException();

            public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count)
            {
                throw new NotImplementedException();
            }

            public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode)
            {
                throw new NotImplementedException();
            }

            public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode, TrackingFidelityMode trackingFidelity)
            {
                throw new NotImplementedException();
            }

            public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode)
            {
                throw new NotImplementedException();
            }

            public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity)
            {
                throw new NotImplementedException();
            }

            public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode)
            {
                throw new NotImplementedException();
            }

            public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity)
            {
                throw new NotImplementedException();
            }

            public ITextSnapshotLine GetLineFromLineNumber(int lineNumber)
            {
                return _inner.GetLineFromLineNumber(lineNumber);
            }

            public ITextSnapshotLine GetLineFromPosition(int position)
            {
                throw new NotImplementedException();
            }

            public int GetLineNumberFromPosition(int position)
            {
                throw new NotImplementedException();
            }

            public string GetText(Span span)
            {
                throw new NotImplementedException();
            }

            public string GetText(int startIndex, int length)
            {
                throw new NotImplementedException();
            }

            public string GetText()
            {
                throw new NotImplementedException();
            }

            public char[] ToCharArray(int startIndex, int length)
            {
                throw new NotImplementedException();
            }

            public void Write(TextWriter writer, Span span)
            {
                throw new NotImplementedException();
            }

            public void Write(TextWriter writer)
            {
                throw new NotImplementedException();
            }
        }
    }
}
