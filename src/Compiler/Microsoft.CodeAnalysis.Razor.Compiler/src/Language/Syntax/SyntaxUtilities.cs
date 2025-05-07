// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language.Syntax;

internal static class SyntaxUtilities
{
    public static MarkupTextLiteralSyntax MergeTextLiterals(params ReadOnlySpan<MarkupTextLiteralSyntax?> literals)
    {
        SyntaxNode? parent = null;
        var position = 0;
        var seenFirstLiteral = false;

        using PooledArrayBuilder<SyntaxToken> builder = [];

        foreach (var literal in literals)
        {
            if (literal == null)
            {
                continue;
            }

            if (!seenFirstLiteral)
            {
                // Set the parent and position of the merged literal to the value of the first non-null literal.
                parent = literal.Parent;
                position = literal.Position;
                seenFirstLiteral = true;
            }

            builder.AddRange(literal.LiteralTokens);
        }

        return (MarkupTextLiteralSyntax)InternalSyntax.SyntaxFactory
            .MarkupTextLiteral(
                literalTokens: builder.ToGreenListNode().ToGreenList<InternalSyntax.SyntaxToken>(),
                chunkGenerator: null)
            .CreateRed(parent, position);
    }

    /// <summary>
    ///  This method returns the children of this start tag in legacy format.
    ///  This is needed to generate the same classified spans as the legacy syntax tree.
    /// </summary>
    internal static SyntaxNode ComputeStartTagLegacyChildren(this BaseMarkupStartTagSyntax startTag)
    {
        using PooledArrayBuilder<SyntaxNode> builder = [];
        using PooledArrayBuilder<SyntaxToken> tokensBuilder = [];

        // Take a ref to tokensBuilder here to avoid calling AsRef() multiple times below
        // for each call to ToListAndClear().
        ref var tokens = ref tokensBuilder.AsRef();

        SpanEditHandler? acceptsAnyHandler = null;
        SpanEditHandler? acceptsNoneHandler = null;

        var containsAttributesContent = false;

        var editHandler = startTag.GetEditHandler();
        if (editHandler != null)
        {
            acceptsAnyHandler = SpanEditHandler.CreateDefault(AcceptedCharactersInternal.Any);
            acceptsNoneHandler = SpanEditHandler.CreateDefault(AcceptedCharactersInternal.None);

            // We want to know if this tag contains non-whitespace attribute content to set
            // the appropriate AcceptedCharacters. The prefix of a start tag(E.g '|<foo| attr>')
            // will have 'Any' accepted characters if non-whitespace attribute content exists.

            foreach (var attribute in startTag.Attributes)
            {
                if (!string.IsNullOrWhiteSpace(attribute.GetContent()))
                {
                    containsAttributesContent = true;
                    break;
                }
            }
        }

        var chunkGenerator = startTag.ChunkGenerator;

        if (startTag.OpenAngle is { IsMissing: false } openAngle)
        {
            tokens.Add(openAngle);
        }

        if (startTag.Bang is { IsMissing: false } bang)
        {
            builder.Add(NewMarkupTextLiteral(tokens.ToListAndClear(), chunkGenerator, acceptsAnyHandler));

            // We can skip adding bang to the tokens builder, since we just cleared it.
            builder.Add(NewRazorMetaCode(bang, chunkGenerator, acceptsNoneHandler));
        }

        if (startTag.Name is { IsMissing: false } name)
        {
            tokens.Add(name);
        }

        builder.Add(NewMarkupTextLiteral(
            tokens.ToListAndClear(), chunkGenerator, containsAttributesContent ? acceptsAnyHandler : editHandler));

        builder.AddRange(startTag.Attributes);

        if (startTag.ForwardSlash is { IsMissing: false } forwardSlash)
        {
            tokens.Add(forwardSlash);
        }

        if (startTag.CloseAngle is { IsMissing: false } closeAngle)
        {
            tokens.Add(closeAngle);
        }

        if (tokens.Count > 0)
        {
            builder.Add(NewMarkupTextLiteral(tokens.ToListAndClear(), chunkGenerator, editHandler));
        }

        return builder.ToListNode(startTag, startTag.Position)
            .AssumeNotNull($"ToListNode should not return null since builder was not empty.");
    }

    internal static SyntaxNode ComputeEndTagLegacyChildren(this BaseMarkupEndTagSyntax endTag)
    {
        // This method returns the children of this end tag in legacy format.
        // This is needed to generate the same classified spans as the legacy syntax tree.

        using PooledArrayBuilder<SyntaxNode> builder = [];
        using PooledArrayBuilder<SyntaxToken> tokensBuilder = [];

        // Take a ref to tokensBuilder here to avoid calling AsRef() multiple times below
        // for each call to ToListAndClear().
        ref var tokens = ref tokensBuilder.AsRef();

        var editHandler = endTag.GetEditHandler();
        var chunkGenerator = endTag.GetChunkGenerator();

        if (endTag.OpenAngle is { IsMissing: false} openAngle)
        {
            tokens.Add(openAngle);
        }

        if (endTag.ForwardSlash is { IsMissing: false } forwardSlash)
        {
            tokens.Add(forwardSlash);
        }

        if (endTag.Bang is { IsMissing: false } bang)
        {
            SpanEditHandler? acceptsAnyHandler = null;
            SpanEditHandler? acceptsNoneHandler = null;

            if (editHandler != null)
            {
                acceptsAnyHandler = SpanEditHandler.CreateDefault(AcceptedCharactersInternal.Any);
                acceptsNoneHandler = SpanEditHandler.CreateDefault(AcceptedCharactersInternal.None);
            }

            // The prefix of an end tag(E.g '|</|!foo>') will have 'Any' accepted characters if a bang exists.
            builder.Add(NewMarkupTextLiteral(tokens.ToListAndClear(), chunkGenerator, acceptsAnyHandler));

            // We can skip adding bang to the tokens builder, since we just cleared it.
            builder.Add(NewRazorMetaCode(bang, chunkGenerator, acceptsNoneHandler));
        }

        if (endTag.Name is { IsMissing: false } name)
        {
            tokens.Add(name);
        }

        if (endTag.MiscAttributeContent?.Children is { Count: > 0 } children)
        {
            foreach (var content in children)
            {
                var literal = (MarkupTextLiteralSyntax)content;
                tokens.AddRange(literal.LiteralTokens);
            }
        }

        if (endTag.CloseAngle is { IsMissing: false } closeAngle)
        {
            tokens.Add(closeAngle);
        }

        builder.Add(NewMarkupTextLiteral(tokens.ToListAndClear(), chunkGenerator, editHandler));

        return builder.ToListNode(endTag, endTag.Position)
            .AssumeNotNull($"ToListNode should not return null since builder was not empty.");
    }

    private static MarkupTextLiteralSyntax NewMarkupTextLiteral(
        SyntaxList<SyntaxToken> tokens, ISpanChunkGenerator? chunkGenerator, SpanEditHandler? editHandler)
    {
        return SyntaxFactory.MarkupTextLiteral(tokens, chunkGenerator).WithEditHandler(editHandler);
    }

    private static RazorMetaCodeSyntax NewRazorMetaCode(
        SyntaxToken tokens, ISpanChunkGenerator? chunkGenerator, SpanEditHandler? editHandler)
    {
        return SyntaxFactory.RazorMetaCode(new(tokens), chunkGenerator).WithEditHandler(editHandler);
    }

    internal static SyntaxList<RazorSyntaxNode> GetRewrittenMarkupStartTagChildren(
        MarkupStartTagSyntax node, bool includeEditHandler = false)
    {
        // Rewrites the children of the start tag to look like the legacy syntax tree.
        if (node.IsMarkupTransition)
        {
            return GetRewrittenMarkupNodeChildren(node, node.ChunkGenerator, includeEditHandler);
        }

        using PooledArrayBuilder<RazorSyntaxNode> newChildren = [];
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
                            // Pop stack
                            AddLiteralIsIfNeeded();
                            newChildren.Add(contentChild);
                        }
                    }

                    break;

                default:
                    AddLiteralIsIfNeeded();
                    newChildren.Add(child);
                    break;
            }
        }

        AddLiteralIsIfNeeded();

        return newChildren.ToList(node);

        void AddLiteralIsIfNeeded()
        {
            if (literals.Count > 0)
            {
                var mergedLiteral = MergeTextLiterals(literals.ToArrayAndClear());

                if (includeEditHandler)
                {
                    mergedLiteral = mergedLiteral.WithEditHandler(latestEditHandler);
                }

                latestEditHandler = null;
                newChildren.Add(mergedLiteral);
            }
        }
    }

    internal static SyntaxList<RazorSyntaxNode> GetRewrittenMarkupEndTagChildren(
        MarkupEndTagSyntax node, bool includeEditHandler = false)
    {
        // Rewrites the children of the end tag to look like the legacy syntax tree.
        return node.IsMarkupTransition
            ? GetRewrittenMarkupNodeChildren(node, node.ChunkGenerator, includeEditHandler)
            : node.LegacyChildren;
    }

    internal static SyntaxList<RazorSyntaxNode> GetRewrittenMarkupNodeChildren(
        MarkupSyntaxNode node, ISpanChunkGenerator chunkGenerator, bool includeEditHandler = false)
    {
        using PooledArrayBuilder<SyntaxToken> builder = [];

        foreach (var descendantNode in node.DescendantNodes())
        {
            if (descendantNode is SyntaxToken { IsMissing: false } token)
            {
                builder.Add(token);
            }
        }

        var markupTransition = InternalSyntax.SyntaxFactory
            .MarkupTransition(
                builder.ToGreenListNode().ToGreenList<InternalSyntax.SyntaxToken>(),
                chunkGenerator)
            .CreateRed(node, node.Position);

        if (includeEditHandler && node.GetEditHandler() is { } editHandler)
        {
            markupTransition = markupTransition.WithEditHandler(editHandler);
        }

        return SyntaxList<RazorSyntaxNode>.Create(markupTransition, parent: node);
    }
}
