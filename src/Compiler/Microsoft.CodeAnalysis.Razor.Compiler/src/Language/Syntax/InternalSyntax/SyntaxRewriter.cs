﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.AspNetCore.Razor.Language.Syntax.InternalSyntax;

internal abstract partial class SyntaxRewriter : SyntaxVisitor<GreenNode>
{
    public override GreenNode VisitToken(SyntaxToken token)
    {
        var leading = VisitList(token.LeadingTrivia);
        var trailing = VisitList(token.TrailingTrivia);

        if (leading != token.LeadingTrivia || trailing != token.TrailingTrivia)
        {
            if (leading != token.LeadingTrivia)
            {
                token = token.TokenWithLeadingTrivia(leading.Node);
            }

            if (trailing != token.TrailingTrivia)
            {
                token = token.TokenWithTrailingTrivia(trailing.Node);
            }
        }

        return token;
    }

    public SyntaxList<TNode> VisitList<TNode>(SyntaxList<TNode> list) where TNode : GreenNode
    {
        SyntaxListBuilder alternate = null;
        for (int i = 0, n = list.Count; i < n; i++)
        {
            var item = list[i];
            var visited = Visit(item);
            if (item != visited && alternate == null)
            {
                alternate = new SyntaxListBuilder(n);
                alternate.AddRange(list, 0, i);
            }

            if (alternate != null)
            {
                alternate.Add(visited);
            }
        }

        if (alternate != null)
        {
            return alternate.ToList<TNode>();
        }

        return list;
    }
}
