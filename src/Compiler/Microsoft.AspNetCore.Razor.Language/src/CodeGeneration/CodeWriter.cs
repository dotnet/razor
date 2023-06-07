// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

public sealed class CodeWriter
{
    private readonly StringBuilder _builder;

    private string _newLine;

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
        _builder = new StringBuilder();
    }

    public int CurrentIndent { get; set; }

    public int Length => _builder.Length;

    public string NewLine
    {
        get => _newLine;
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

        _newLine = value;
    }

    public bool IndentWithTabs { get; }

    public int TabSize { get; }

    public SourceLocation Location => new(_absoluteIndex, _currentLineIndex, _currentLineCharacterIndex);

    public char this[int index]
    {
        get
        {
            if (index < 0 || index >= _builder.Length)
            {
                throw new IndexOutOfRangeException(nameof(index));
            }

            return _builder[index];
        }
    }

    public CodeWriter Indent(int size)
    {
        if (size == 0 || (Length != 0 && this[Length - 1] != '\n'))
        {
            return this;
        }

        var actualSize = 0;
        if (IndentWithTabs)
        {
            // Avoid writing directly to the StringBuilder here, that will throw off the manual indexing
            // done by the base class.
            var tabs = size / TabSize;
            actualSize += tabs;
            _builder.Append('\t', tabs);

            var spaces = size % TabSize;
            actualSize += spaces;
            _builder.Append(' ', spaces);
        }
        else
        {
            actualSize = size;
            _builder.Append(' ', size);
        }

        _currentLineCharacterIndex += actualSize;
        _absoluteIndex += actualSize;

        return this;
    }

    public CodeWriter Write(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return WriteCore(value.AsSpan());
    }

    public CodeWriter Write(ReadOnlySpan<char> span)
    {
        return WriteCore(span);
    }

    public CodeWriter Write(ReadOnlyMemory<char> memory)
    {
        return WriteCore(memory.Span);
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

        return WriteCore(value.AsSpan(startIndex, count));
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
    private unsafe CodeWriter WriteCore(ReadOnlySpan<char> span)
    {
        if (span.Length == 0)
        {
            return this;
        }

        Indent(CurrentIndent);

        char? lastChar = _builder.Length > 0
            ? _builder[^1]
            : null;

        // Internally, StringBuilder can create char arrays that are large
        // enough to end up on the LOH. This can happen when appending chars at the
        // end of the StringBuilder's internal array block and there are a lot of
        // remaining chars to append onto the next block. In this case, the StringBuilder
        // will allocate the next block to be large enough to contain all of the remaining
        // chars, which could end up on the LOH. To avoid this problem, we write in a loop
        // and break large writes into multiple chunks.

        var toWrite = span;
        while (!toWrite.IsEmpty)
        {
            const int MaxChunkSize = 32768;

            var chunk = toWrite.Length >= MaxChunkSize
                ? toWrite[..MaxChunkSize]
                : toWrite;

            fixed (char* ptr = chunk)
            {
                _builder.Append(ptr, chunk.Length);
            }

            toWrite = toWrite[chunk.Length..];
        }

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
        int charIndex;
        while ((charIndex = span.IndexOfAny('\r', '\n')) >= 0)
        {
            _currentLineIndex++;
            _currentLineCharacterIndex = 0;

            charIndex++;

            // We might have stopped at a \r, so check if it's followed by \n and then advance the index.
            // Otherwise, we'll count this line break twice.
            if (charIndex < span.Length &&
                span[charIndex - 1] == '\r' &&
                span[charIndex] == '\n')
            {
                charIndex++;
            }

            span = span[charIndex..];
        }

        _currentLineCharacterIndex += span.Length;

        return this;
    }

    public CodeWriter WriteLine()
    {
        _builder.Append(NewLine);

        _currentLineIndex++;
        _currentLineCharacterIndex = 0;
        _absoluteIndex += NewLine.Length;

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
        return _builder.ToString();
    }
}
