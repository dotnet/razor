// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.Language.Syntax;

/// <summary>
/// Represents a <see cref="SyntaxVisitor"/> that descends an entire <see cref="SyntaxNode"/> graph
/// visiting each SyntaxNode and its child SyntaxNodes and <see cref="SyntaxToken"/>s in depth-first order.
/// An optional range can be passed in which reduces the <see cref="SyntaxNode"/>s and <see cref="SyntaxToken"/>s
/// visited to those overlapping with the given range.
/// </summary>
internal abstract class SyntaxWalker : SyntaxVisitor
{
    private int _recursionDepth;
    private readonly TextSpan? _range;

    protected SyntaxWalker(TextSpan? range = null)
    {
        _range = range;
    }

    private bool ShouldVisit(TextSpan span)
    {
        return _range is TextSpan range && range.OverlapsWith(span);
    }

    public override void Visit(SyntaxNode? node)
    {
        if (node != null && ShouldVisit(node.Span))
        {
            _recursionDepth++;
            StackGuard.EnsureSufficientExecutionStack(_recursionDepth);

            ((RazorSyntaxNode)node).Accept(this);

            _recursionDepth--;
        }
    }

    public override void DefaultVisit(SyntaxNode node)
    {
        foreach (var child in node.ChildNodes())
        {
            if (ShouldVisit(child.Span))
            {
                if (child is SyntaxToken token)
                {
                    VisitToken(token);
                }
                else
                {
                    Visit(child);
                }
            }
        }
    }

    public virtual void VisitToken(SyntaxToken token)
    {
    }
}
