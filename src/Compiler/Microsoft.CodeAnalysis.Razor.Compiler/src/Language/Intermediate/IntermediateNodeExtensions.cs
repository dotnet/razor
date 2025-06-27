// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public static class IntermediateNodeExtensions
{
    public static string GetContent(this HtmlContentIntermediateNode node)
    {
        using var _ = StringBuilderPool.GetPooledObject(out var builder);

        foreach (var child in node.Children)
        {
            if (child is HtmlIntermediateToken token)
            {
                builder.Append(token.Content);
            }
        }

        return builder.ToString();
    }

    public static ImmutableArray<RazorDiagnostic> GetAllDiagnostics(this IntermediateNode node)
    {
        ArgHelper.ThrowIfNull(node);

        var diagnostics = new PooledHashSet<RazorDiagnostic>();
        try
        {
            CollectDiagnostics(node, ref diagnostics);

            return diagnostics.OrderByAsArray(static d => d.Span.AbsoluteIndex);
        }
        finally
        {
            diagnostics.ClearAndFree();
        }

        static void CollectDiagnostics(IntermediateNode node, ref PooledHashSet<RazorDiagnostic> diagnostics)
        {
            if (node.HasDiagnostics)
            {
                diagnostics.UnionWith(node.Diagnostics);
            }

            foreach (var childNode in node.Children)
            {
                CollectDiagnostics(childNode, ref diagnostics);
            }
        }
    }

    public static ImmutableArray<TNode> FindDescendantNodes<TNode>(this IntermediateNode node)
        where TNode : IntermediateNode
    {
        using var _ = ArrayBuilderPool<TNode>.GetPooledObject(out var results);
        Visitor<TNode>.Visit(node, results);

        return results.ToImmutableAndClear();
    }

    private sealed class Visitor<TNode> : IntermediateNodeWalker
        where TNode : IntermediateNode
    {
        private readonly IntermediateNode _root;
        private readonly ImmutableArray<TNode>.Builder _results;

        private Visitor(IntermediateNode root, ImmutableArray<TNode>.Builder results)
        {
            _root = root;
            _results = results;
        }

        public static void Visit(IntermediateNode root, ImmutableArray<TNode>.Builder results)
        {
            var visitor = new Visitor<TNode>(root, results);
            visitor.Visit(root);
        }

        public override void VisitDefault(IntermediateNode node)
        {
            if (node is TNode match && !ReferenceEquals(_root, node))
            {
                _results.Add(match);
            }

            base.VisitDefault(node);
        }
    }
}
