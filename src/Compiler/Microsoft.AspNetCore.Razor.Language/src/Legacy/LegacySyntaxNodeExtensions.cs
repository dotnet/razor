// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

internal static partial class LegacySyntaxNodeExtensions
{
    private class SpanData
    {
        public SyntaxNode Previous;
        public SyntaxNode Next;
    }

    /// <summary>
    ///  Caches previous/next span result for a particular node. A conditional weak table
    ///  is used to avoid adding fields to all syntax nodes.
    /// </summary>
    private static readonly ConditionalWeakTable<SyntaxNode, SpanData> s_spanDataTable = new();

    private static readonly ISet<SyntaxKind> s_transitionSpanKinds = new HashSet<SyntaxKind>
    {
        SyntaxKind.CSharpTransition,
        SyntaxKind.MarkupTransition,
    };

    private static readonly ISet<SyntaxKind> s_metaCodeSpanKinds = new HashSet<SyntaxKind>
    {
        SyntaxKind.RazorMetaCode,
    };

    private static readonly ISet<SyntaxKind> s_commentSpanKinds = new HashSet<SyntaxKind>
    {
        SyntaxKind.RazorCommentTransition,
        SyntaxKind.RazorCommentStar,
        SyntaxKind.RazorCommentLiteral,
    };

    private static readonly ISet<SyntaxKind> s_codeSpanKinds = new HashSet<SyntaxKind>
    {
        SyntaxKind.CSharpStatementLiteral,
        SyntaxKind.CSharpExpressionLiteral,
        SyntaxKind.CSharpEphemeralTextLiteral,
    };

    private static readonly ISet<SyntaxKind> s_markupSpanKinds = new HashSet<SyntaxKind>
    {
        SyntaxKind.MarkupTextLiteral,
        SyntaxKind.MarkupEphemeralTextLiteral,
    };

    private static readonly ISet<SyntaxKind> s_noneSpanKinds = new HashSet<SyntaxKind>
    {
        SyntaxKind.UnclassifiedTextLiteral,
    };

    private static readonly ISet<SyntaxKind> s_allSpanKinds = CreateAllSpanKindsSet();

    private static ISet<SyntaxKind> CreateAllSpanKindsSet()
    {
        var set = new HashSet<SyntaxKind>();

        set.UnionWith(s_transitionSpanKinds);
        set.UnionWith(s_metaCodeSpanKinds);
        set.UnionWith(s_commentSpanKinds);
        set.UnionWith(s_codeSpanKinds);
        set.UnionWith(s_markupSpanKinds);
        set.UnionWith(s_noneSpanKinds);

        return set;
    }

    public static SpanContext GetSpanContext(this SyntaxNode node)
    {
        var context = node.GetAnnotationValue(SyntaxConstants.SpanContextKind);

        return context is SpanContext ? (SpanContext)context : null;
    }

    public static TNode WithSpanContext<TNode>(this TNode node, SpanContext spanContext) where TNode : SyntaxNode
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var newAnnotation = new SyntaxAnnotation(SyntaxConstants.SpanContextKind, spanContext);

        List<SyntaxAnnotation> newAnnotations = null;
        if (node.ContainsAnnotations)
        {
            var existingNodeAnnotations = node.GetAnnotations();
            for (int i = 0; i < existingNodeAnnotations.Length; i++)
            {
                var annotation = existingNodeAnnotations[i];
                if (annotation.Kind != newAnnotation.Kind)
                {
                    if (newAnnotations == null)
                    {
                        newAnnotations = new List<SyntaxAnnotation>();
                        newAnnotations.Add(newAnnotation);
                    }

                    newAnnotations.Add(annotation);
                }
            }
        }

        var newAnnotationsArray = newAnnotations == null ? new[] { newAnnotation } : newAnnotations.ToArray();

        return node.WithAnnotations(newAnnotationsArray);
    }

    public static SyntaxNode LocateOwner(this SyntaxNode node, SourceChange change)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (change.Span.AbsoluteIndex < node.Position)
        {
            // Early escape for cases where changes overlap multiple spans
            // In those cases, the span will return false, and we don't want to search the whole tree
            // So if the current span starts after the change, we know we've searched as far as we need to
            return null;
        }

        if (node.EndPosition < change.Span.AbsoluteIndex)
        {
            // no need to look into this node as it completely precedes the change
            return null;
        }

        if (IsSpanKind(node))
        {
            var editHandler = node.GetSpanContext()?.EditHandler ?? SpanEditHandler.CreateDefault();
            return editHandler.OwnsChange(node, change) ? node : null;
        }

        IReadOnlyList<SyntaxNode> children;
        if (node is MarkupStartTagSyntax startTag)
        {
            children = startTag.Children;
        }
        else if (node is MarkupEndTagSyntax endTag)
        {
            children = endTag.Children;
        }
        else if (node is MarkupTagHelperStartTagSyntax startTagHelper)
        {
            children = startTagHelper.Children;
        }
        else if (node is MarkupTagHelperEndTagSyntax endTagHelper)
        {
            children = endTagHelper.Children;
        }
        else
        {
            children = node.ChildNodes();
        }

        SyntaxNode owner = null;

        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            owner = LocateOwner(child, change);
            if (owner != null)
            {
                break;
            }
        }

        return owner;
    }

    public static bool IsTransitionSpanKind(this SyntaxNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        return s_transitionSpanKinds.Contains(node.Kind);
    }

    public static bool IsMetaCodeSpanKind(this SyntaxNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        return s_metaCodeSpanKinds.Contains(node.Kind);
    }

    public static bool IsCommentSpanKind(this SyntaxNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        return s_commentSpanKinds.Contains(node.Kind);
    }

    public static bool IsCodeSpanKind(this SyntaxNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        return s_codeSpanKinds.Contains(node.Kind);
    }

    public static bool IsMarkupSpanKind(this SyntaxNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        return s_markupSpanKinds.Contains(node.Kind);
    }

    public static bool IsNoneSpanKind(this SyntaxNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        return s_noneSpanKinds.Contains(node.Kind);
    }

    public static bool IsSpanKind(this SyntaxNode node)
        => s_allSpanKinds.Contains(node.Kind);

    private static IEnumerable<SyntaxNode> FlattenSpansInReverse(this SyntaxNode node)
    {
        using var stack = new NodeStack(node.DescendantNodes());

        // Iterate through stack.
        while (!stack.IsEmpty)
        {
            var child = stack.Pop();

            if (child is MarkupStartTagSyntax startTag)
            {
                var children = startTag.Children;

                for (var i = children.Count - 1; i >= 0; i--)
                {
                    var tagChild = children[i];
                    if (tagChild.IsSpanKind())
                    {
                        yield return tagChild;
                    }
                }
            }
            else if (child is MarkupEndTagSyntax endTag)
            {
                var children = endTag.Children;

                for (var i = children.Count - 1; i >= 0; i--)
                {
                    var tagChild = children[i];
                    if (tagChild.IsSpanKind())
                    {
                        yield return tagChild;
                    }
                }
            }
            else if (child.IsSpanKind())
            {
                yield return child;
            }
        }
    }

    public static IEnumerable<SyntaxNode> FlattenSpans(this SyntaxNode node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        foreach (var child in node.DescendantNodes())
        {
            if (child is MarkupStartTagSyntax startTag)
            {
                foreach (var tagChild in startTag.Children)
                {
                    if (tagChild.IsSpanKind())
                    {
                        yield return tagChild;
                    }
                }
            }
            else if (child is MarkupEndTagSyntax endTag)
            {
                foreach (var tagChild in endTag.Children)
                {
                    if (tagChild.IsSpanKind())
                    {
                        yield return tagChild;
                    }
                }
            }
            else if (child.IsSpanKind())
            {
                yield return child;
            }
        }
    }

    public static SyntaxNode PreviousSpan(this SyntaxNode node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var spanData = s_spanDataTable.GetOrCreateValue(node);

        if (spanData.Previous is { } previousSpan)
        {
            return previousSpan;
        }

        var parent = node.Parent;
        while (parent is not null)
        {
            foreach (var span in parent.FlattenSpansInReverse())
            {
                if (span.EndPosition <= node.Position && span != node)
                {
                    spanData.Previous = span;
                    return span;
                }
            }

            parent = parent.Parent;
        }

        return null;
    }

    public static SyntaxNode NextSpan(this SyntaxNode node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var spanData = s_spanDataTable.GetOrCreateValue(node);

        if (spanData.Next is { } nextSpan)
        {
            return nextSpan;
        }

        var parent = node.Parent;
        while (parent is not null)
        {
            foreach (var span in parent.FlattenSpans())
            {
                if (span.Position >= node.Position && span != node)
                {
                    spanData.Next = span;
                    return span;
                }
            }

            parent = parent.Parent;
        }

        return null;
    }
}
