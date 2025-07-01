// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

internal readonly struct CodeSnippet
{
    private readonly ReadOnlyMemory<char> _value;
    private readonly object? _values;

    public CodeSnippet(string value)
    {
        _value = value.AsMemory();
    }

    public CodeSnippet(ReadOnlyMemory<char> value)
    {
        _value = value;
    }

    public CodeSnippet(ImmutableArray<string> values)
    {
        _values = ImmutableCollectionsMarshal.AsArray(values).AssumeNotNull();
    }

    public CodeSnippet(ImmutableArray<ReadOnlyMemory<char>> text)
    {
        _values = ImmutableCollectionsMarshal.AsArray(text).AssumeNotNull();
    }

    public CodeSnippet(ImmutableArray<CodeSnippet> text)
    {
        _values = ImmutableCollectionsMarshal.AsArray(text).AssumeNotNull();
    }

    public CodeSnippet(ref readonly PooledArrayBuilder<CodeSnippet> builder)
    {
        _values = builder.ToArray();
    }

    public CodeSnippet(ref CodeSnippetInterpolatedStringHandler handler)
    {
        _values = handler.ToArray();
    }

    public void WriteTo(CodeWriter writer)
    {
        if (_values is CodeSnippet[] snippetArray)
        {
            foreach (var snippet in snippetArray)
            {
                snippet.WriteTo(writer);
            }
        }
        else if (_values is string[] stringArray)
        {
            writer.Write(ImmutableCollectionsMarshal.AsImmutableArray(stringArray));
        }
        else if (_values is ReadOnlyMemory<char>[] memoryArray)
        {
            writer.Write(ImmutableCollectionsMarshal.AsImmutableArray(memoryArray));
        }
        else
        {
            writer.Write(_value);
        }
    }

    public static implicit operator CodeSnippet(string value)
        => new(value);

    [InterpolatedStringHandler]
    public ref struct CodeSnippetInterpolatedStringHandler
    {
        private MemoryBuilder<ReadOnlyMemory<char>> _builder;

        public CodeSnippetInterpolatedStringHandler(int literalLength, int formattedCount)
        {
            _builder = new MemoryBuilder<ReadOnlyMemory<char>>(formattedCount);
        }

        public ReadOnlyMemory<char>[] ToArray()
        {
            var result = _builder.AsMemory().ToArray();
            _builder.Dispose();

            return result;
        }

        public void AppendLiteral(string value)
            => _builder.Append(value.AsMemory());

        public void AppendFormatted(string? value)
        {
            if (value is not null)
            {
                _builder.Append(value.AsMemory());
            }
        }

        public void AppendFormatted(ReadOnlyMemory<char> value)
        {
            if (!value.IsEmpty)
            {
                _builder.Append(value);
            }
        }

        public void AppendFormatted(CodeSnippet value)
        {
            if (!value._value.IsEmpty)
            {
                AppendFormatted(value._value);
            }
            else if (value._values is ReadOnlyMemory<char>[] memoryArray)
            {
                foreach (var item in memoryArray)
                {
                    AppendFormatted(item);
                }
            }
            else if (value._values is string[] stringArray)
            {
                foreach (var item in stringArray)
                {
                    AppendFormatted(item);
                }
            }
            else if (value._values is CodeSnippet[] snippetArray)
            {
                foreach (var item in snippetArray)
                {
                    AppendFormatted(item);
                }
            }
        }

        public void AppendFormatted(ImmutableArray<string> values)
        {
            foreach (var item in values)
            {
                AppendFormatted(item);
            }
        }

        public void AppendFormatted(ImmutableArray<ReadOnlyMemory<char>> values)
        {
            foreach (var item in values)
            {
                AppendFormatted(item);
            }
        }

        public void AppendFormatted(ImmutableArray<CodeSnippet> values)
        {
            foreach (var item in values)
            {
                AppendFormatted(item);
            }
        }

        public void AppendFormatted<T>(T value)
        {
            if (value is null)
            {
                return;
            }

            switch (value)
            {
                case string s:
                    AppendFormatted(s);
                    break;

                case ReadOnlyMemory<char> memory:
                    AppendFormatted(memory);
                    break;

                case CodeSnippet snippet:
                    AppendFormatted(snippet);
                    break;

                case ImmutableArray<string> stringArray:
                    AppendFormatted(stringArray);
                    break;

                case ImmutableArray<ReadOnlyMemory<char>> memoryArray:
                    AppendFormatted(memoryArray);
                    break;

                case ImmutableArray<CodeSnippet> snippetArray:
                    AppendFormatted(snippetArray);
                    break;

                default:
                    AppendFormatted(value.ToString() ?? string.Empty);
                    break;
            }
        }
    }
}
