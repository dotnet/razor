// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language.Syntax;

internal static class SyntaxUtilities
{
    internal static SyntaxList<RazorSyntaxNode> GetRewrittenMarkupStartTagChildren(
        MarkupStartTagSyntax node, bool includeEditHandler = false)
    {
        // Rewrites the children of the start tag to look like the legacy syntax tree.
        if (node.IsMarkupTransition)
        {
            return GetRewrittenMarkupNodeChildren(node, node.ChunkGenerator, includeEditHandler);
        }

        using PooledArrayBuilder<GreenNode> newChildren = [];
        using PooledArrayBuilder<MarkupTextLiteralSyntax> literals = [];

        SpanEditHandler? latestEditHandler = null;

        foreach (var child in node.LegacyChildren)
        {
            switch (child)
            {
                case MarkupTextLiteralSyntax literal:
                    literals.Add(literal);

                    if (includeEditHandler)
                    {
                        latestEditHandler = literal.GetEditHandler() ?? latestEditHandler;
                    }

                    break;

                case MarkupMiscAttributeContentSyntax miscContent:
                    foreach (var contentChild in miscContent.Children)
                    {
                        if (contentChild is MarkupTextLiteralSyntax contentLiteral)
                        {
                            literals.Add(contentLiteral);

                            if (includeEditHandler)
                            {
                                latestEditHandler = contentLiteral.GetEditHandler() ?? latestEditHandler;
                            }
                        }
                        else
                        {
                            AddLiteralIsIfNeeded();
                            newChildren.Add(contentChild.Green);
                        }
                    }

                    break;

                default:
                    AddLiteralIsIfNeeded();
                    newChildren.Add(child.Green);
                    break;
            }
        }

        AddLiteralIsIfNeeded();

        return newChildren.ToList<RazorSyntaxNode>(node);

        void AddLiteralIsIfNeeded()
        {
            if (literals.Count > 0)
            {
                var mergedLiteral = MergeTextLiterals(literals.ToArrayAndClear());

                if (includeEditHandler)
                {
                    mergedLiteral = mergedLiteral.WithEditHandlerGreen(latestEditHandler);
                }

                latestEditHandler = null;
                newChildren.Add(mergedLiteral);
            }
        }
    }

    private static InternalSyntax.MarkupTextLiteralSyntax MergeTextLiterals(params ReadOnlySpan<MarkupTextLiteralSyntax> literals)
    {
        using PooledArrayBuilder<SyntaxToken> builder = [];

        foreach (var literal in literals)
        {
            builder.AddRange(literal.LiteralTokens);
        }

        return InternalSyntax.SyntaxFactory.MarkupTextLiteral(
            literalTokens: builder.ToGreenListNode().ToGreenList<InternalSyntax.SyntaxToken>(),
            chunkGenerator: null);
    }

    internal static SyntaxList<RazorSyntaxNode> GetRewrittenMarkupEndTagChildren(
        MarkupEndTagSyntax node, bool includeEditHandler = false)
    {
        // Rewrites the children of the end tag to look like the legacy syntax tree.
        return node.IsMarkupTransition
            ? GetRewrittenMarkupNodeChildren(node, node.ChunkGenerator, includeEditHandler)
            : node.LegacyChildren;
    }

    internal static SyntaxList<SyntaxNode> GetRewrittenMarkupNodeChildren(
        MarkupSyntaxNode node, ISpanChunkGenerator chunkGenerator, bool includeEditHandler = false)
    {
        using PooledArrayBuilder<SyntaxToken> builder = [];

        foreach (var token in node.DescendantTokens())
        {
            if (!token.IsMissing)
            {
                builder.Add(token);
            }
        }

        var markupTransition = InternalSyntax.SyntaxFactory.MarkupTransition(
            builder.ToGreenListNode().ToGreenList<InternalSyntax.SyntaxToken>(),
            chunkGenerator);

        if (includeEditHandler && node.GetEditHandler() is { } editHandler)
        {
            markupTransition = markupTransition.WithEditHandlerGreen(editHandler);
        }

        return new(markupTransition.CreateRed(node, node.Position));
    }
}
