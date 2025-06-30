// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public abstract class IntermediateToken : IntermediateNode
{
    private object? _content;

    private ImmutableArray<ReadOnlyMemory<char>>? _contentParts;

    public TokenKind Kind { get; }

    public bool IsCSharp => Kind == TokenKind.CSharp;
    public bool IsHtml => Kind == TokenKind.Html;

    public bool IsLazy { get; }

    public string Content
    {
        get
        {
            if (_content is null && _contentParts is { Length: > 0 } contentParts)
            {
                // If we have content parts, we need to concatenate them.
                // This is a common case for large tokens that are split into parts.

                var size = 0;
                foreach (var part in contentParts)
                {
                    size += part.Length;
                }

                _content = string.Create(size, contentParts, (span, parts) =>
                {
                    foreach (var part in parts)
                    {
                        part.Span.CopyTo(span);
                        span = span[part.Length..];
                    }

                    Debug.Assert(span.Length == 0, "All parts should have been copied to the span.");
                });
            }

            return _content switch
            {
                string s => s,
                LazyContent lazy => lazy.Content,
                _ => Assumed.Unreachable<string>(),
            };
        }
    }

    internal ImmutableArray<ReadOnlyMemory<char>> ContentParts
    {
        get
        {
            if (_contentParts is { } contentParts)
            {
                // If we have content parts, we return them as is.
                return contentParts;
            }

            // If we have a single string content, we wrap it in an array.
            contentParts = [Content.AsMemory()];
            _contentParts = contentParts;

            return contentParts;
        }
    }

    public override IntermediateNodeCollection Children => IntermediateNodeCollection.ReadOnly;

    protected IntermediateToken(TokenKind kind, string content, SourceSpan? span)
    {
        Debug.Assert(content != null);

        Kind = kind;
        IsLazy = false;
        _content = content;
        Source = span;
    }

    private protected IntermediateToken(TokenKind kind, ImmutableArray<ReadOnlyMemory<char>> contentParts, SourceSpan? span)
    {
        Kind = kind;
        IsLazy = false;
        _contentParts = contentParts;
        Source = span;
    }

    private protected IntermediateToken(TokenKind kind, LazyContent lazyContent, SourceSpan? span)
    {
        Debug.Assert(lazyContent != null);

        Kind = kind;
        IsLazy = true;
        _content = lazyContent;
        Source = span;
    }

    private protected IntermediateToken(TokenKind kind, ref ContentInterpolatedStringHandler handler, SourceSpan? span)
    {
        Kind = kind;
        IsLazy = false;
        _contentParts = handler.ToContentParts();
        Source = span;
    }

    internal void SetContent(string content)
    {
        Debug.Assert(content != null);

        _content = content;
        _contentParts = null;
    }

    internal void SetContent(ImmutableArray<ReadOnlyMemory<char>> contentParts)
    {
        Debug.Assert(contentParts.Length > 0);

        _contentParts = contentParts;
        _content = null;
    }

    internal void SetContent([InterpolatedStringHandlerArgument("")] ref ContentInterpolatedStringHandler contentParts)
    {
        _contentParts = contentParts.ToContentParts();
        _content = null;
    }

    public override void Accept(IntermediateNodeVisitor visitor)
    {
        visitor.VisitToken(this);
    }

    public override void FormatNode(IntermediateNodeFormatter formatter)
    {
        formatter.WriteContent(Content);

        formatter.WriteProperty(nameof(Content), Content);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [InterpolatedStringHandler]
    internal ref struct ContentInterpolatedStringHandler
    {
        private MemoryBuilder<ReadOnlyMemory<char>> _builder;

        public ContentInterpolatedStringHandler(int literalLength, int formattedCount)
        {
            _builder = new MemoryBuilder<ReadOnlyMemory<char>>();
        }

        public ImmutableArray<ReadOnlyMemory<char>> ToContentParts()
        {
            if (_builder.AsMemory().Length == 0)
            {
                return [];
            }

            var contentParts = _builder.AsMemory().ToArray();
            _builder.Dispose();

            return ImmutableCollectionsMarshal.AsImmutableArray(contentParts);
        }

        public void AppendLiteral(string value)
        {
            _builder.Append(value.AsMemory());
        }

        public void AppendFormatted(ReadOnlyMemory<char> value)
        {
            _builder.Append(value);
        }

        public void AppendFormatted(string? value)
        {
            if (value is not null)
            {
                _builder.Append(value.AsMemory());
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
                case ReadOnlyMemory<char> memory:
                    AppendFormatted(memory);
                    break;

                case string s:
                    AppendFormatted(s.AsMemory());
                    break;

                default:
                    AppendLiteral(value.ToString() ?? string.Empty);
                    break;
            }
        }
    }
}
