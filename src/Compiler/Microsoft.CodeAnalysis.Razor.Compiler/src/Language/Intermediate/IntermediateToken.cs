// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public abstract class IntermediateToken : IntermediateNode
{
    private object _content;

    public TokenKind Kind { get; }

    public bool IsCSharp => Kind == TokenKind.CSharp;
    public bool IsHtml => Kind == TokenKind.Html;

    public bool IsLazy { get; }

    public string Content
        => _content switch
        {
            string s => s,
            LazyContent lazy => lazy.Content,
            _ => Assumed.Unreachable<string>(),
        };

    public override IntermediateNodeCollection Children => IntermediateNodeCollection.ReadOnly;

    protected IntermediateToken(TokenKind kind, string content, SourceSpan? span)
    {
        Debug.Assert(content != null);

        Kind = kind;
        IsLazy = false;
        _content = content;
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

    internal void SetContent(string content)
    {
        Debug.Assert(content != null);

        _content = content;
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
}
