// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public static class DocumentIntermediateNodeExtensions
{
    public static ClassDeclarationIntermediateNode? FindPrimaryClass(this DocumentIntermediateNode document)
        => FindNode<ClassDeclarationIntermediateNode>(document, static n => n.IsPrimaryClass);

    public static MethodDeclarationIntermediateNode? FindPrimaryMethod(this DocumentIntermediateNode document)
        => FindNode<MethodDeclarationIntermediateNode>(document, static n => n.IsPrimaryMethod);

    public static NamespaceDeclarationIntermediateNode? FindPrimaryNamespace(this DocumentIntermediateNode document)
        => FindNode<NamespaceDeclarationIntermediateNode>(document, static n => n.IsPrimaryNamespace);

    public static ImmutableArray<IntermediateNodeReference<DirectiveIntermediateNode>> FindDirectiveReferences(
        this DocumentIntermediateNode document, DirectiveDescriptor directive)
    {
        using var _ = ArrayBuilderPool<IntermediateNodeReference<DirectiveIntermediateNode>>.GetPooledObject(out var results);
        DirectiveVisitor.Visit(document, directive, results);

        return results.ToImmutableAndClear();
    }

    public static ImmutableArray<IntermediateNodeReference<TNode>> FindDescendantReferences<TNode>(this DocumentIntermediateNode document)
        where TNode : IntermediateNode
    {
        using var _ = ArrayBuilderPool<IntermediateNodeReference<TNode>>.GetPooledObject(out var results);
        ReferenceVisitor<TNode>.Visit(document, results);

        return results.ToImmutableAndClear();
    }

    private static T? FindNode<T>(IntermediateNode node, Func<T, bool> predicate)
        where T : IntermediateNode
    {
        if (node is T target && predicate(target))
        {
            return target;
        }

        foreach (var child in node.Children)
        {
            var result = FindNode(child, predicate);

            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private sealed class DirectiveVisitor : IntermediateNodeWalker
    {
        private readonly DirectiveDescriptor _directive;
        private readonly ImmutableArray<IntermediateNodeReference<DirectiveIntermediateNode>>.Builder _results;

        private DirectiveVisitor(
            DirectiveDescriptor directive,
            ImmutableArray<IntermediateNodeReference<DirectiveIntermediateNode>>.Builder results)
        {
            _directive = directive;
            _results = results;
        }

        public static void Visit(
            DocumentIntermediateNode document,
            DirectiveDescriptor directive,
            ImmutableArray<IntermediateNodeReference<DirectiveIntermediateNode>>.Builder results)
        {
            var visitor = new DirectiveVisitor(directive, results);
            visitor.Visit(document);
        }

        public override void VisitDirective(DirectiveIntermediateNode node)
        {
            if (_directive == node.Directive)
            {
                // Because we start visiting from a DocumentIntermediateNode, we know Parent isn't null here.
                _results.Add(new(Parent!, node));
            }

            base.VisitDirective(node);
        }
    }

    private sealed class ReferenceVisitor<TNode> : IntermediateNodeWalker
        where TNode : IntermediateNode
    {
        private readonly ImmutableArray<IntermediateNodeReference<TNode>>.Builder _results;

        private ReferenceVisitor(ImmutableArray<IntermediateNodeReference<TNode>>.Builder results)
        {
            _results = results;
        }

        public static void Visit(
            DocumentIntermediateNode document,
            ImmutableArray<IntermediateNodeReference<TNode>>.Builder results)
        {
            var visitor = new ReferenceVisitor<TNode>(results);
            visitor.Visit(document);
        }

        public override void VisitDefault(IntermediateNode node)
        {
            base.VisitDefault(node);

            // Use a post-order traversal because references are used to replace nodes, and thus
            // change the parent nodes.
            //
            // This ensures that we always operate on the leaf nodes first.
            if (node is TNode resultNode)
            {
                // Because we start visiting from a DocumentIntermediateNode, we know Parent isn't null here.
                _results.Add(new(Parent!, resultNode));
            }
        }
    }
}
