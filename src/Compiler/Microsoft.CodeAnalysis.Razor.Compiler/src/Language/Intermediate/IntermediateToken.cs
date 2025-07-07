// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public class IntermediateToken : IntermediateNode
{
    public TokenKind Kind { get; }

    public bool IsCSharp => Kind == TokenKind.CSharp;
    public bool IsHtml => Kind == TokenKind.Html;

    public virtual string? Content { get; private set; }

    public override IntermediateNodeCollection Children => IntermediateNodeCollection.ReadOnly;

    public IntermediateToken(TokenKind kind, string? content, SourceSpan? source)
    {
        Kind = kind;
        Content = content;

        if (source != null)
        {
            Source = source;
        }
    }

    public void UpdateContent(string content)
    {
        Content = content;
    }

    public override void Accept(IntermediateNodeVisitor visitor)
        => visitor.VisitToken(this);

    public override void FormatNode(IntermediateNodeFormatter formatter)
    {
        formatter.WriteContent(Content);

        formatter.WriteProperty(nameof(Content), Content);
    }
}
