// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

public sealed class CodeWriter
{
    private const int PageSize = 1000;

    // Rather than using a StringBuilder, we maintain a list of pages of ReadOnlyMemory<char> arrays.
    // This avoids copying strings into a StringBuilder's internal char arrays only to copy them
    // out later in StringBuilder.ToString(). This also avoids string duplication by holding onto
    // the strings themselves. So, if the same string instance is added multiple times, we won't
    // duplicate it each time. Instead, we'll hold a ReadOnlyMemory<char> for the string.
    private readonly List<ReadOnlyMemory<char>[]> _pages;
    private int _pageIndex;
    private int _pageOffset;
    private char? _lastChar;

    private ReadOnlyMemory<char> _newLine;
    private int _indentSize;
    private ReadOnlyMemory<char> _indentString;

    private int _absoluteIndex;
    private int _currentLineIndex;
    private int _currentLineCharacterIndex;

    public CodeWriter()
        : this(Environment.NewLine, RazorCodeGenerationOptions.CreateDefault())
    {
    }

    public CodeWriter(string newLine, RazorCodeGenerationOptions options)
    {
        SetNewLine(newLine);
        IndentWithTabs = options.IndentWithTabs;
        TabSize = options.IndentSize;

        _indentSize = 0;
        _indentString = ReadOnlyMemory<char>.Empty;

        _pages = new();
    }

    private void AddChars(ReadOnlyMemory<char> value)
    {
        if (value.Length == 0)
        {
            return;
        }

        if (_pageIndex == _pages.Count)
        {
            _pages.Add(new ReadOnlyMemory<char>[PageSize]);
        }

        _pages[_pageIndex][_pageOffset] = value;
        _pageOffset++;

        if (_pageOffset == PageSize)
        {
            _pageIndex++;
            _pageOffset = 0;
        }

        _lastChar = value.Span[^1];
    }

    public int CurrentIndent
    {
        get => _indentSize;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (_indentSize != value)
            {
                _indentSize = value;
                _indentString = ComputeIndent(value, IndentWithTabs, TabSize);
            }
        }
    }

    // Because of the how _absoluteIndex is computed, it is effectively the length
    // of what has been written.
    public int Length => _absoluteIndex;

    public string NewLine
    {
        get => _newLine.ToString();
        set => SetNewLine(value);
    }

    [MemberNotNull(nameof(_newLine))]
    private void SetNewLine(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (value != "\r\n" && value != "\n")
        {
            throw new ArgumentException(Resources.FormatCodeWriter_InvalidNewLine(value), nameof(value));
        }

        _newLine = value.AsMemory();
    }

    public bool IndentWithTabs { get; }

    public int TabSize { get; }

    public SourceLocation Location => new(_absoluteIndex, _currentLineIndex, _currentLineCharacterIndex);

    public char this[int index]
    {
        get
        {
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

        AddChars(indentString);

        var indentLength = indentString.Length;
        _currentLineCharacterIndex += indentLength;
        _absoluteIndex += indentLength;

        return this;
    }

    public CodeWriter Indent()
    {
        if (_indentSize == 0 || LastChar is not '\n')
        {
            return this;
        }

        var indentString = _indentString;
        AddChars(indentString);

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
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return WriteCore(value.AsMemory());
    }

    public CodeWriter Write(ReadOnlyMemory<char> memory)
    {
        return WriteCore(memory);
    }

    public CodeWriter Write(string value, int startIndex, int count)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (startIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (startIndex > value.Length - count)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        }

        return WriteCore(value.AsMemory(startIndex, count));
    }

    [InterpolatedStringHandler]
    internal readonly ref struct CodeWriterInterpolatedStringHandler
    {
        private readonly CodeWriter _writer;

        public CodeWriterInterpolatedStringHandler(int literalLength, int formattedCount, CodeWriter writer)
        {
            _writer = writer;
        }

        public void AppendLiteral(string value)
            => _writer.Write(value);

        public void AppendFormatted<T>(T value)
        {
            if (value is null)
            {
                return;
            }

            _writer.Write(value.ToString());
        }
    }

    internal CodeWriter Write([InterpolatedStringHandlerArgument("")] ref CodeWriterInterpolatedStringHandler handler)
    {
        return this;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe CodeWriter WriteCore(ReadOnlyMemory<char> chars, bool allowIndent = true)
    {
        if (chars.IsEmpty)
        {
            return this;
        }

        if (allowIndent)
        {
            Indent();
        }

        var lastChar = _lastChar;

        AddChars(chars);

        var span = chars.Span;

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
        // This let's us count the new-line occurrences and keep the index of the last one.
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
    {
        WriteCore(_newLine, allowIndent: false);

        return this;
    }

    public CodeWriter WriteLine(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return Write(value).WriteLine();
    }

    public string GenerateCode()
    {
        unsafe
        {
            // This might look a bit scary, but it's pretty simple. We allocate our string
            // with the correct length up front and then use simple pointer math to copy
            // the pages of ReadOnlyMemory<char> directly into it.

            // Eventually, we need to remove this and not return a giant string, which can
            // easily be allocated on the LOH. The work to remove this is tracked by
            // https://github.com/dotnet/razor/issues/8076.

            var length = Length;
            var result = new string('\0', length);

            fixed (char* stringPtr = result)
            {
                var destination = stringPtr;

                // destinationSize and sourceSize track the number of bytes (not chars).
                var destinationSize = length * sizeof(char);

                foreach (var page in _pages)
                {
                    foreach (var chars in page)
                    {
                        var source = chars.Span;
                        var sourceSize = source.Length * sizeof(char);

                        fixed (char* srcPtr = source)
                        {
                            Buffer.MemoryCopy(srcPtr, destination, destinationSize, sourceSize);
                        }

                        destination += source.Length;
                        destinationSize -= sourceSize;

                        Debug.Assert(destinationSize >= 0);
                    }
                }

                Debug.Assert(destinationSize == 0, "We didn't exhaust our destination pointer!");
            }

            return result;
        }
    }
}
