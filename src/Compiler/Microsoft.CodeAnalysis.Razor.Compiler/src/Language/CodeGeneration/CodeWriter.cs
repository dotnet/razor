﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

public partial class CodeWriter : IDisposable
{
    // This is the size of each "page", which are arrays of ReadOnlyMemory<char>.
    // This number was chosen arbitrarily as a "best guess". If changed, care should be
    // taken to ensure that pages are not allocated on the LOH. ReadOnlyMemory<char>
    // takes up 16 bytes, so a page size of 1000 is 16k.
    private const int MinimumPageSize = 1000;

    // Rather than using a StringBuilder, we maintain a linked list of pages, which are arrays
    // of "chunks of text", represented by ReadOnlyMemory<char>. This avoids copying strings
    // into a StringBuilder's internal char arrays only to copy them out later in
    // StringBuilder.ToString(). This also avoids string duplication by holding onto the strings
    // themselves. So, if the same string instance is added multiple times, we won't duplicate it
    // each time. Instead, we'll hold a ReadOnlyMemory<char> for the string.
    //
    // Note that LinkedList<T> was chosen to avoid copying for especially large generated code files.
    // In addition, because LinkedList<T> provides direct access to the last element, appending
    // is extremely efficient.
    protected readonly LinkedList<ReadOnlyMemory<char>[]> _pages;
    private int _pageOffset;
    private char? _lastChar;

    private string _newLine;
    private int _indentSize;
    private ReadOnlyMemory<char> _indentString;

    private int _absoluteIndex;
    private int _currentLineIndex;
    private int _currentLineCharacterIndex;

    public CodeWriter()
        : this(RazorCodeGenerationOptions.Default)
    {
    }

    public CodeWriter(RazorCodeGenerationOptions options)
    {
        SetNewLine(options.NewLine);
        IndentWithTabs = options.IndentWithTabs;
        TabSize = options.IndentSize;

        _indentSize = 0;
        _indentString = ReadOnlyMemory<char>.Empty;

        _pages = new();
    }

    public void Dispose()
    {
        foreach (var page in _pages)
        {
            ArrayPool<ReadOnlyMemory<char>>.Shared.Return(page, clearArray: true);
        }

        _pages.Clear();
    }

    private void AddTextChunk(ReadOnlyMemory<char> value)
    {
        if (value.Length == 0)
        {
            return;
        }

        // If we're at the start of a page, we need to add the page first.
        ReadOnlyMemory<char>[] lastPage;

        if (_pageOffset == 0)
        {
            lastPage = ArrayPool<ReadOnlyMemory<char>>.Shared.Rent(MinimumPageSize);
            _pages.AddLast(lastPage);
        }
        else
        {
            lastPage = _pages.Last!.Value;
        }

        // Add our chunk of text (the ReadOnlyMemory<char>) and increment the offset.
        lastPage[_pageOffset] = value;
        _pageOffset++;

        // We've reached the end of a page, so we reset the offset to 0.
        // This will cause a new page to be added next time.
        // _pageOffset is checked against the lastPage.Length as the Rent call that
        // return that array may return an array longer that MinimumPageSize.
        if (_pageOffset == lastPage.Length)
        {
            _pageOffset = 0;
        }

        // Remember the last character of the text chunk we just added.
        _lastChar = value.Span[^1];
    }

    public int CurrentIndent
    {
        get => _indentSize;
        set
        {
            ArgHelper.ThrowIfNegative(value);

            if (_indentSize != value)
            {
                _indentSize = value;
                _indentString = ComputeIndent(value, IndentWithTabs, TabSize);
            }
        }
    }

    // Because of how _absoluteIndex is computed, it is effectively the length
    // of what has been written.
    public int Length => _absoluteIndex;

    public string NewLine
    {
        get => _newLine;
        set => SetNewLine(value);
    }

    [MemberNotNull(nameof(_newLine))]
    private void SetNewLine(string value)
    {
        ArgHelper.ThrowIfNull(value);

        if (value != "\r\n" && value != "\n")
        {
            throw new ArgumentException(Resources.FormatCodeWriter_InvalidNewLine(value), nameof(value));
        }

        _newLine = value;
    }

    public bool IndentWithTabs { get; }

    public int TabSize { get; }

    public SourceLocation Location => new(_absoluteIndex, _currentLineIndex, _currentLineCharacterIndex);

    public char this[int index]
    {
        get
        {
            // This Debug.Fail(...) is present because no Razor code currently accesses this
            // indexer and it isn't implemented efficiently. All Razor code that previously
            // used the indexer were really just inspecting the last char, which is now exposed separately.
            Debug.Fail("Do not use this indexer without reimplementing it more efficiently.");

            foreach (var page in _pages)
            {
                foreach (var chars in page)
                {
                    if (index < chars.Length)
                    {
                        return chars.Span[index];
                    }

                    index -= chars.Length;
                }
            }

            throw new IndexOutOfRangeException(nameof(index));
        }
    }

    public char? LastChar => _lastChar;

    public CodeWriter Indent(int size)
    {
        if (size == 0 || LastChar is not '\n')
        {
            return this;
        }

        var indentString = size == _indentSize
            ? _indentString
            : ComputeIndent(size, IndentWithTabs, TabSize);

        AddTextChunk(indentString);

        var indentLength = indentString.Length;
        _currentLineCharacterIndex += indentLength;
        _absoluteIndex += indentLength;

        return this;
    }

    private static ReadOnlyMemory<char> ComputeIndent(int size, bool useTabs, int tabSize)
    {
        if (size == 0)
        {
            return ReadOnlyMemory<char>.Empty;
        }

        if (useTabs)
        {
            var tabCount = size / tabSize;
            var spaceCount = size % tabSize;

            using var _ = StringBuilderPool.GetPooledObject(out var builder);
            builder.SetCapacityIfLarger(tabCount + spaceCount);

            builder.Append('\t', tabCount);
            builder.Append(' ', spaceCount);

            return builder.ToString().AsMemory();
        }

        return new string(' ', size).AsMemory();
    }

    public CodeWriter Write(string value)
    {
        ArgHelper.ThrowIfNull(value);

        return WriteCore(value.AsMemory());
    }

    public CodeWriter Write(ReadOnlyMemory<char> value)
        => WriteCore(value);

    public CodeWriter Write(string value, int startIndex, int count)
    {
        ArgHelper.ThrowIfNull(value);
        ArgHelper.ThrowIfNegative(startIndex);
        ArgHelper.ThrowIfNegative(count);
        ArgHelper.ThrowIfGreaterThan(startIndex, value.Length - count);

        return WriteCore(value.AsMemory(startIndex, count));
    }

    public CodeWriter Write([InterpolatedStringHandlerArgument("")] ref WriteInterpolatedStringHandler handler)
        => this;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private CodeWriter WriteCore(ReadOnlyMemory<char> value, bool allowIndent = true)
    {
        if (value.IsEmpty)
        {
            return this;
        }

        if (allowIndent)
        {
            Indent(_indentSize);
        }

        var lastChar = _lastChar;

        AddTextChunk(value);

        var span = value.Span;

        _absoluteIndex += span.Length;

        // Check the last character *before* the write and the first character of the span that
        // was written to determine whether this is a new-line that is spread across two writes.
        if (lastChar == '\r' && span[0] == '\n')
        {
            // Skip the first character of span to ensure that it isn't considered in the following
            // line break detection loop.
            span = span[1..];
        }

        // Iterate the span, stopping at each occurrence of a new-line character.
        // This lets us count the new-line occurrences and keep the index of the last one.
        int newLineIndex;
        while ((newLineIndex = span.IndexOfAny('\r', '\n')) >= 0)
        {
            _currentLineIndex++;
            _currentLineCharacterIndex = 0;

            newLineIndex++;

            // We might have stopped at a \r, so check if it's followed by \n and then advance the index.
            // Otherwise, we'll count this line break twice.
            if (newLineIndex < span.Length &&
                span[newLineIndex - 1] == '\r' &&
                span[newLineIndex] == '\n')
            {
                newLineIndex++;
            }

            span = span[newLineIndex..];
        }

        _currentLineCharacterIndex += span.Length;

        return this;
    }

    public CodeWriter WriteLine()
        => WriteCore(_newLine.AsMemory(), allowIndent: false);

    public CodeWriter WriteLine(ReadOnlyMemory<char> value)
        => WriteCore(value).WriteLine();

    public CodeWriter WriteLine(string value)
    {
        ArgHelper.ThrowIfNull(value);

        return WriteCore(value.AsMemory()).WriteLine();
    }

    public CodeWriter WriteLine([InterpolatedStringHandlerArgument("")] ref WriteInterpolatedStringHandler handler)
        => WriteLine();

    public virtual SourceText GetText()
    {
        using var reader = new Reader(_pages, Length);
        return SourceText.From(reader, Length, Encoding.UTF8);
    }

    // Internal for testing
    internal static TextReader GetTestTextReader(LinkedList<ReadOnlyMemory<char>[]> pages)
    {
        return new Reader(pages, pages.Count);
    }

    internal sealed class Reader(LinkedList<ReadOnlyMemory<char>[]> pages, int length) : TextReader
    {
        private readonly LinkedList<ReadOnlyMemory<char>[]> _pages = pages;
        private LinkedListNode<ReadOnlyMemory<char>[]>? _page = pages.First;
        private int _chunkIndex;
        private int _charIndex;
        private int _position;

        public int Length => length;

        private void SetPositionToClosestKnownPosition(int position)
        {
            if (position < _position / 2)
            {
                // Closest to the start
                _page = _pages.First;
                _chunkIndex = 0;
                _charIndex = 0;
                _position = 0;
            }
            else if (position > _position + ((Length - _position) / 2))
            {
                // Closest to the end
                _page = null;
                _chunkIndex = -1;
                _charIndex = -1;
                _position = Length;
            }
            else
            {
                // Somewhere in the middle, use the current position.
            }
        }

        private void EnsurePositionAtOrBefore(int position)
        {
            if (_position <= position)
            {
                return;
            }

            var page = _page;
            var chunkIndex = _chunkIndex;
            int newPosition;

            if (page is null)
            {
                page = _pages.Last!;
                chunkIndex = page.Value.Length - 1;
                newPosition = Length - page.Value[chunkIndex].Length;
            }
            else
            {
                newPosition = _position - _charIndex;
            }

            while (newPosition > position)
            {
                if (chunkIndex > 0)
                {
                    chunkIndex--;
                }
                else
                {
                    page = page.Previous!;
                    chunkIndex = page.Value.Length - 1;
                }

                newPosition -= page.Value[chunkIndex].Length;
            }

            _page = page;
            _chunkIndex = chunkIndex;
            _charIndex = 0;
            _position = newPosition;
        }

        private void EnsurePositionAt(int position)
        {
            Debug.Assert(_page != null);

            if (_position >= position)
            {
                return;
            }

            var page = _page;
            var chunkIndex = _chunkIndex;
            var newPosition = _position - _charIndex;
            var chunkLength = page.Value[chunkIndex].Length;
            while (newPosition + chunkLength <= position)
            {
                if (chunkIndex < page.Value.Length - 1)
                {
                    chunkIndex++;
                }
                else
                {
                    page = page.Next!;
                    chunkIndex = 0;
                }

                newPosition += chunkLength;
                chunkLength = page.Value[chunkIndex].Length;
            }

            _page = page;
            _chunkIndex = chunkIndex;
            _charIndex = position - newPosition;
            _position = position;
        }

        public void SetPosition(int position)
        {
            ArgHelper.ThrowIfNegative(position);
            ArgHelper.ThrowIfGreaterThan(position, Length);

            SetPositionToClosestKnownPosition(position);
            if (position == _position)
            {
                // If we are already at the position, no need to change anything.
                return;
            }

            EnsurePositionAtOrBefore(position);
            EnsurePositionAt(position);
        }

        public override int Read()
        {
            if (_page is null)
            {
                return -1;
            }

            var result = _page.Value[_chunkIndex].Span[_charIndex];
            EnsurePositionAt(_position + 1);

            return result;
        }

        public override int Peek()
        {
            var (page, chunkIndex, charIndex, position) = (_page, _chunkIndex, _charIndex, _position);

            var result = Read();

            (_page, _chunkIndex, _charIndex, _position) = (page, chunkIndex, charIndex, position);

            return result;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            ArgHelper.ThrowIfNull(buffer);
            ArgHelper.ThrowIfNegative(index);
            ArgHelper.ThrowIfNegative(count);

            if (buffer.Length - index < count)
            {
                throw new ArgumentException($"{nameof(count)} is greater than the number of elements from {nameof(index)} to the end of {nameof(buffer)}.");
            }

            if (_page is null)
            {
                return 0;
            }

            var destination = buffer.AsSpan(index, count);
            var charsWritten = 0;

            var page = _page;
            var chunkIndex = _chunkIndex;
            var charIndex = _charIndex;

            Debug.Assert(chunkIndex >= 0);
            Debug.Assert(charIndex >= 0);

            do
            {
                var chunks = page.Value.AsSpan(chunkIndex);
                var isFirst = true;

                foreach (var chunk in chunks)
                {
                    if (destination.IsEmpty)
                    {
                        // If we have no more space in the destination, we're done.
                        break;
                    }

                    var source = chunk.Span;

                    // Slice if the first chunk is partial. Note that this only occurs for the first chunk.
                    if (isFirst)
                    {
                        isFirst = false;

                        if (charIndex > 0)
                        {
                            source = source[charIndex..];
                        }
                    }

                    // Are we about to write past the end of the buffer? If so, adjust source.
                    // This will be the last chunk we write, so be sure to update charIndex.
                    if (source.Length > destination.Length)
                    {
                        source = source[..destination.Length];
                        charIndex += source.Length;
                    }
                    else
                    {
                        chunkIndex++;
                        charIndex = 0;
                    }

                    if (source.IsEmpty)
                    {
                        continue;
                    }

                    source.CopyTo(destination);
                    destination = destination[source.Length..];

                    charsWritten += source.Length;
                }

                if (destination.IsEmpty)
                {
                    break;
                }

                page = page.Next;
                chunkIndex = 0;
                charIndex = 0;
            }
            while (page is not null);

            if (page is not null)
            {
                _page = page;
                _chunkIndex = chunkIndex;
                _charIndex = charIndex;
            }
            else
            {
                _page = null;
                _chunkIndex = -1;
                _charIndex = -1;
            }

            _position += charsWritten;

            return charsWritten;
        }

        public override string ReadToEnd()
        {
            if (_page is null)
            {
                return string.Empty;
            }

            var remainingLength = Length - _position;
            var result = string.Create(remainingLength, (_page, _chunkIndex, _charIndex), static (destination, state) =>
            {
                var (page, chunkIndex, charIndex) = state;

                Debug.Assert(page is not null);
                Debug.Assert(chunkIndex >= 0);
                Debug.Assert(charIndex >= 0);

                // Use the current chunk index to slice the first set of chunks.
                var chunks = page.Value.AsSpan(chunkIndex);

                do
                {
                    foreach (var chunk in chunks)
                    {
                        var source = chunk.Span;

                        // Slice the first chunk if it's partial.
                        if (charIndex > 0)
                        {
                            source = source[charIndex..];
                            charIndex = 0;
                        }

                        if (source.IsEmpty)
                        {
                            continue;
                        }

                        source.CopyTo(destination);
                        destination = destination[source.Length..];
                    }

                    page = page.Next;
                    chunks = (page?.Value ?? []).AsSpan();
                }
                while (page is not null);

                Debug.Assert(destination.Length == 0, "We didn't fill the whole span!");
            });

            _page = null;
            _chunkIndex = -1;
            _charIndex = 1;
            _position = Length;

            return result;
        }
    }
}
