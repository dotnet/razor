// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.IO;

namespace Microsoft.AspNetCore.Razor.Language.Syntax.InternalSyntax;

internal class SyntaxToken : RazorSyntaxNode
{
    private readonly GreenNode _leadingTrivia;
    private readonly GreenNode _trailingTrivia;

    internal SyntaxToken(SyntaxKind kind, string content, RazorDiagnostic[] diagnostics)
        : base(kind, content.Length, diagnostics, annotations: null)
    {
        Content = content;
    }

    internal SyntaxToken(SyntaxKind kind, string content, GreenNode leadingTrivia, GreenNode trailingTrivia, RazorDiagnostic[] diagnostics, SyntaxAnnotation[] annotations)
        : base(kind, content.Length, diagnostics, annotations)
    {
        Content = content;
        _leadingTrivia = leadingTrivia;
        AdjustFlagsAndWidth(leadingTrivia);
        _trailingTrivia = trailingTrivia;
        AdjustFlagsAndWidth(trailingTrivia);
    }

    public string Content { get; }

    internal override bool IsToken => true;

    public override int Width => Content.Length;

    internal override SyntaxNode CreateRed(SyntaxNode parent, int position)
    {
        return new Syntax.SyntaxToken(this, parent, position);
    }

    protected override void WriteTokenTo(TextWriter writer)
    {
        writer.Write(Content);
    }

    public sealed override GreenNode GetLeadingTrivia()
    {
        return _leadingTrivia;
    }

    public sealed override GreenNode GetTrailingTrivia()
    {
        return _trailingTrivia;
    }

    internal override GreenNode SetDiagnostics(RazorDiagnostic[] diagnostics)
    {
        return new SyntaxToken(Kind, Content, _leadingTrivia, _trailingTrivia, diagnostics, GetAnnotations());
    }

    internal override GreenNode SetAnnotations(SyntaxAnnotation[] annotations)
    {
        return new SyntaxToken(Kind, Content, _leadingTrivia, _trailingTrivia, GetDiagnostics(), annotations);
    }

    protected sealed override int GetSlotCount()
    {
        return 0;
    }

    internal sealed override GreenNode GetSlot(int index)
    {
        throw new InvalidOperationException("Tokens don't have slots.");
    }

    public override TResult Accept<TResult>(SyntaxVisitor<TResult> visitor)
    {
        return visitor.VisitToken(this);
    }

    public override void Accept(SyntaxVisitor visitor)
    {
        visitor.VisitToken(this);
    }

    public override bool IsEquivalentTo(GreenNode other)
    {
        if (!base.IsEquivalentTo(other))
        {
            return false;
        }

        var otherToken = (SyntaxToken)other;

        if (Content != otherToken.Content)
        {
            return false;
        }

        var thisLeading = GetLeadingTrivia();
        var otherLeading = otherToken.GetLeadingTrivia();
        if (thisLeading != otherLeading)
        {
            if (thisLeading == null || otherLeading == null)
            {
                return false;
            }

            if (!thisLeading.IsEquivalentTo(otherLeading))
            {
                return false;
            }
        }

        var thisTrailing = GetTrailingTrivia();
        var otherTrailing = otherToken.GetTrailingTrivia();
        if (thisTrailing != otherTrailing)
        {
            if (thisTrailing == null || otherTrailing == null)
            {
                return false;
            }

            if (!thisTrailing.IsEquivalentTo(otherTrailing))
            {
                return false;
            }
        }

        return true;
    }

    public override string ToString()
    {
        return Content;
    }

    internal static SyntaxToken CreateMissing(SyntaxKind kind, params RazorDiagnostic[] diagnostics)
    {
        return new MissingToken(kind, diagnostics);
    }

    private class MissingToken : SyntaxToken
    {
        internal MissingToken(SyntaxKind kind, RazorDiagnostic[] diagnostics)
            : base(kind, string.Empty, diagnostics)
        {
            Flags |= NodeFlags.IsMissing;
        }
    }
}
