// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public abstract class IntermediateToken : IntermediateNode
{
    private string _content;

    public string Content
        => _content;

    public override IntermediateNodeCollection Children => IntermediateNodeCollection.ReadOnly;

    protected IntermediateToken(string content, SourceSpan? source)
    {
        _content = content;

        if (source != null)
        {
            Source = source;
        }
    }

    public void UpdateContent(string content)
    {
        _content = content;
    }

    public override void Accept(IntermediateNodeVisitor visitor)
        => visitor.VisitToken(this);

    public override void FormatNode(IntermediateNodeFormatter formatter)
    {
        formatter.WriteContent(Content);

        formatter.WriteProperty(nameof(Content), Content);
    }
}
