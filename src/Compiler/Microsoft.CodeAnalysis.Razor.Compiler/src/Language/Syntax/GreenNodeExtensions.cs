// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language.Syntax;

internal static class GreenNodeExtensions
{
    internal static InternalSyntax.SyntaxList<T> ToGreenList<T>(this SyntaxNode node) where T : GreenNode
    {
        return node != null ?
            ToGreenList<T>(node.Green) :
            default(InternalSyntax.SyntaxList<T>);
    }

    internal static InternalSyntax.SyntaxList<T> ToGreenList<T>(this GreenNode node) where T : GreenNode
    {
        return new InternalSyntax.SyntaxList<T>(node);
    }

#nullable enable

    public static TNode WithAnnotationsGreen<TNode>(this TNode node, IEnumerable<SyntaxAnnotation> annotations)
        where TNode : GreenNode
    {
        using var newAnnotations = new PooledArrayBuilder<SyntaxAnnotation>();

        foreach (var candidate in annotations)
        {
            if (!newAnnotations.Contains(candidate))
            {
                newAnnotations.Add(candidate);
            }
        }

        if (newAnnotations.Count == 0)
        {
            var existingAnnotations = node.GetAnnotations();
            return existingAnnotations.Length > 0
                ? (TNode)node.SetAnnotations(null)
                : node;
        }
        else
        {
            return (TNode)node.SetAnnotations(newAnnotations.ToArrayAndClear());
        }
    }

    public static TNode WithAdditionalAnnotationsGreen<TNode>(this TNode node, SyntaxAnnotation annotation)
        where TNode : GreenNode
    {
        var existingAnnotations = node.GetAnnotations();

        using var newAnnotations = new PooledArrayBuilder<SyntaxAnnotation>(capacity: existingAnnotations.Length);
        newAnnotations.AddRange(existingAnnotations);

        if (!newAnnotations.Contains(annotation))
        {
            newAnnotations.Add(annotation);
        }

        return newAnnotations.Count != existingAnnotations.Length
            ? (TNode)node.SetAnnotations(newAnnotations.ToArrayAndClear())
            : node;
    }

    public static TNode WithAdditionalAnnotationsGreen<TNode>(this TNode node, IEnumerable<SyntaxAnnotation> annotations)
        where TNode : GreenNode
    {
        var existingAnnotations = node.GetAnnotations();

        using var newAnnotations = new PooledArrayBuilder<SyntaxAnnotation>(capacity: existingAnnotations.Length);
        newAnnotations.AddRange(existingAnnotations);

        foreach (var candidate in annotations)
        {
            if (!newAnnotations.Contains(candidate))
            {
                newAnnotations.Add(candidate);
            }
        }

        return newAnnotations.Count != existingAnnotations.Length
            ? (TNode)node.SetAnnotations(newAnnotations.ToArrayAndClear())
            : node;
    }

    public static TNode WithoutAnnotationsGreen<TNode>(this TNode node, string annotationKind)
        where TNode : GreenNode
    {
        return node.HasAnnotations(annotationKind)
            ? node.WithoutAnnotationsGreen(node.GetAnnotations(annotationKind))
            : node;
    }

    public static TNode WithoutAnnotationsGreen<TNode>(this TNode node, IEnumerable<SyntaxAnnotation> annotations)
        where TNode : GreenNode
    {
        var existingAnnotations = node.GetAnnotations();

        if (existingAnnotations.Length == 0)
        {
            return node;
        }

        using var removalAnnotations = new PooledArrayBuilder<SyntaxAnnotation>();
        removalAnnotations.AddRange(annotations);

        if (removalAnnotations.Count == 0)
        {
            return node;
        }

        var newAnnotations = new PooledArrayBuilder<SyntaxAnnotation>();
        foreach (var candidate in existingAnnotations)
        {
            if (!removalAnnotations.Contains(candidate))
            {
                newAnnotations.Add(candidate);
            }
        }

        return (TNode)node.SetAnnotations(newAnnotations.ToArrayAndClear());
    }

#nullable disable

    public static TNode WithDiagnosticsGreen<TNode>(this TNode node, RazorDiagnostic[] diagnostics)
        where TNode : GreenNode
    {
        return (TNode)node.SetDiagnostics(diagnostics);
    }

    public static TNode WithDiagnosticsGreen<TNode>(this TNode node, params ImmutableArray<RazorDiagnostic> diagnostics)
        where TNode : GreenNode
    {
        var array = ImmutableCollectionsMarshal.AsArray(diagnostics);
        return node.WithDiagnosticsGreen(array);
    }
}
