// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public abstract class IntermediateToken : IntermediateNode
{
    public TokenKind Kind { get; }
    public virtual string? Content { get; set; }

    public bool HasContent => Content is not null;

    public bool IsCSharp => Kind == TokenKind.CSharp;
    public bool IsHtml => Kind == TokenKind.Html;

    public override IntermediateNodeCollection Children => IntermediateNodeCollection.ReadOnly;

    protected IntermediateToken(TokenKind kind, string? content, SourceSpan? span)
    {
        Kind = kind;
        Content = content;
        Source = span;
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
