// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.Language.Syntax;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(), nq}}")]
internal readonly struct SyntaxToken : IEquatable<SyntaxToken>
{
    internal static readonly Func<SyntaxToken, bool> NonZeroWidth = t => t.Width > 0;
    internal static readonly Func<SyntaxToken, bool> Any = t => true;

    public SyntaxNode? Parent { get; }
    internal GreenNode? Node { get; }
    internal int Index { get; }
    internal int Position { get; }

    internal SyntaxToken(SyntaxNode? parent, GreenNode? token, int position, int index)
    {
        Debug.Assert(parent == null || !parent.Green.IsList, "list cannot be a parent");
        Debug.Assert(token == null || token.IsToken, "token must be a token");

        Parent = parent;
        Node = token;
        Position = position;
        Index = index;
    }

    internal SyntaxToken(GreenNode? token)
        : this()
    {
        Debug.Assert(token == null || token.IsToken, "token must be a token");

        Node = token;
    }

    private string GetDebuggerDisplay()
        => $"{GetType().Name} {Kind} {ToString()}";

    // For debugging
#pragma warning disable IDE0051 // Remove unused private members
    private string SerializedValue => SyntaxSerializer.Default.Serialize(this);
#pragma warning restore IDE0051 // Remove unused private members
    public SyntaxKind Kind => Node?.Kind ?? 0;

    internal GreenNode RequiredNode
    {
        get
        {
            Debug.Assert(Node != null);
            return Node;
        }
    }

    internal int Width => Node?.Width ?? 0;

    public int SpanStart
        => Node != null ? Position : 0;

    public TextSpan Span
        => Node != null ? new TextSpan(Position, Node.Width) : default;

    internal int EndPosition
        => Node != null ? Position + Node.Width : 0;

    public bool IsMissing
        => Node?.IsMissing ?? false;

    public bool ContainsDiagnostics => Node?.ContainsDiagnostics ?? false;

    public bool ContainsAnnotations => Node?.ContainsAnnotations ?? false;

    public string Content => Node != null ? ((InternalSyntax.SyntaxToken)Node).Content : string.Empty;

    public string Text => ToString();

    public void AppendContent(ref MemoryBuilder<ReadOnlyMemory<char>> builder)
    {
        builder.Append(Content);
    }

    public override string ToString()
        => Node?.ToString() ?? string.Empty;

    /// <summary>
    /// Determines whether two <see cref="SyntaxToken"/>s are equal.
    /// </summary>
    public static bool operator ==(SyntaxToken left, SyntaxToken right)
        => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="SyntaxToken"/>s are unequal.
    /// </summary>
    public static bool operator !=(SyntaxToken left, SyntaxToken right)
        => !left.Equals(right);

    /// <summary>
    /// Determines whether the supplied <see cref="SyntaxToken"/> is equal to this
    /// <see cref="SyntaxToken"/>.
    /// </summary>
    public bool Equals(SyntaxToken other)
        => Parent == other.Parent &&
           Node == other.Node &&
           Position == other.Position &&
           Index == other.Index;

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is SyntaxToken token && Equals(token);

    public override int GetHashCode()
    {
        var hash = HashCodeCombiner.Start();
        hash.Add(Parent);
        hash.Add(Node);
        hash.Add(Position);
        hash.Add(Index);

        return hash.CombinedHash;
    }

    /// <summary>
    /// Gets the token that follows this token in the syntax tree.
    /// </summary>
    /// <returns>The token that follows this token in the syntax tree.</returns>
    public SyntaxToken GetNextToken(bool includeZeroWidth = false)
    {
        if (Node == null)
        {
            return default;
        }

        return SyntaxNavigator.GetNextToken(this, includeZeroWidth);
    }

    /// <summary>
    /// Returns the token after this token in the syntax tree.
    /// </summary>
    /// <param name="predicate">Delegate applied to each token.  The token is returned if the predicate returns
    /// true.</param>
    internal SyntaxToken GetNextToken(Func<SyntaxToken, bool> predicate)
    {
        if (Node == null)
        {
            return default;
        }

        return SyntaxNavigator.GetNextToken(this, predicate);
    }

    /// <summary>
    /// Gets the token that precedes this token in the syntax tree.
    /// </summary>
    /// <returns>The previous token that precedes this token in the syntax tree.</returns>
    public SyntaxToken GetPreviousToken(bool includeZeroWidth = false)
    {
        if (Node == null)
        {
            return default;
        }

        return SyntaxNavigator.GetPreviousToken(this, includeZeroWidth);
    }

    /// <summary>
    /// Returns the token before this token in the syntax tree.
    /// </summary>
    /// <param name="predicate">Delegate applied to each token.  The token is returned if the predicate returns
    /// true.</param>
    internal SyntaxToken GetPreviousToken(Func<SyntaxToken, bool> predicate)
    {
        if (Node == null)
        {
            return default;
        }

        return SyntaxNavigator.GetPreviousToken(this, predicate);
    }

    /// <summary>
    /// True if this token has annotations of the specified annotation kind.
    /// </summary>
    public bool HasAnnotations(string annotationKind)
    {
        return Node?.HasAnnotations(annotationKind) ?? false;
    }

    /// <summary>
    /// True if this token has annotations of the specified annotation kinds.
    /// </summary>
    public bool HasAnnotations(params string[] annotationKinds)
    {
        return Node?.HasAnnotations(annotationKinds) ?? false;
    }

    /// <summary>
    /// True if this token has the specified annotation.
    /// </summary>
    public bool HasAnnotation([NotNullWhen(true)] SyntaxAnnotation? annotation)
    {
        return Node?.HasAnnotation(annotation) ?? false;
    }

    /// <summary>
    /// Gets all the annotations of the specified annotation kind.
    /// </summary>
    public IEnumerable<SyntaxAnnotation> GetAnnotations(string annotationKind)
    {
        return Node?.GetAnnotations(annotationKind) ?? SpecializedCollections.EmptyEnumerable<SyntaxAnnotation>();
    }

    /// <summary>
    /// Gets all the annotations of the specified annotation kind.
    /// </summary>
    public IEnumerable<SyntaxAnnotation> GetAnnotations(params string[] annotationKinds)
    {
        return GetAnnotations((IEnumerable<string>)annotationKinds);
    }

    /// <summary>
    /// Gets all the annotations of the specified annotation kind.
    /// </summary>
    public IEnumerable<SyntaxAnnotation> GetAnnotations(IEnumerable<string> annotationKinds)
    {
        return Node?.GetAnnotations(annotationKinds) ?? SpecializedCollections.EmptyEnumerable<SyntaxAnnotation>();
    }

    /// <summary>
    /// Adds this annotation to a given syntax token, creating a new syntax token of the same type with the
    /// annotation on it.
    /// </summary>
    public SyntaxToken WithAdditionalAnnotations(params SyntaxAnnotation[] annotations)
    {
        return WithAdditionalAnnotations((IEnumerable<SyntaxAnnotation>)annotations);
    }

    /// <summary>
    /// Adds this annotation to a given syntax token, creating a new syntax token of the same type with the
    /// annotation on it.
    /// </summary>
    public SyntaxToken WithAdditionalAnnotations(IEnumerable<SyntaxAnnotation> annotations)
    {
        ArgHelper.ThrowIfNull(annotations);

        if (Node != null)
        {
            return new SyntaxToken(
                parent: null,
                token: Node.WithAdditionalAnnotationsGreen(annotations),
                position: 0,
                index: 0);
        }

        return default;
    }

    /// <summary>
    /// Creates a new syntax token identical to this one without the specified annotations.
    /// </summary>
    public SyntaxToken WithoutAnnotations(params SyntaxAnnotation[] annotations)
    {
        return WithoutAnnotations((IEnumerable<SyntaxAnnotation>)annotations);
    }

    /// <summary>
    /// Creates a new syntax token identical to this one without the specified annotations.
    /// </summary>
    public SyntaxToken WithoutAnnotations(IEnumerable<SyntaxAnnotation> annotations)
    {
        ArgHelper.ThrowIfNull(annotations);

        if (Node != null)
        {
            return new SyntaxToken(
                parent: null,
                token: Node.WithoutAnnotationsGreen(annotations),
                position: 0,
                index: 0);
        }

        return default;
    }

    /// <summary>
    /// Creates a new syntax token identical to this one without annotations of the specified kind.
    /// </summary>
    public SyntaxToken WithoutAnnotations(string annotationKind)
    {
        ArgHelper.ThrowIfNull(annotationKind);

        if (HasAnnotations(annotationKind))
        {
            return WithoutAnnotations(GetAnnotations(annotationKind));
        }

        return this;
    }

    /// <summary>
    /// Copies all SyntaxAnnotations, if any, from this SyntaxToken instance and attaches them to a new instance based on <paramref name="token" />.
    /// </summary>
    /// <remarks>
    /// If no annotations are copied, just returns <paramref name="token" />.
    /// </remarks>
    public SyntaxToken CopyAnnotationsTo(SyntaxToken token)
    {
        if (token.Node == null)
        {
            return default;
        }

        if (Node == null)
        {
            return token;
        }

        var annotations = Node.GetAnnotations();
        if (annotations.Length > 0)
        {
            return new SyntaxToken(
                parent: null,
                token: token.Node.WithAdditionalAnnotationsGreen(annotations),
                position: 0,
                index: 0);
        }

        return token;
    }
    /// <summary>
    /// Gets a list of all the diagnostics associated with this token and any related trivia.
    /// This method does not filter diagnostics based on #pragmas and compiler options
    /// like nowarn, warnaserror etc.
    /// </summary>
    public IEnumerable<RazorDiagnostic> GetDiagnostics()
    {
        if (Node == null)
        {
            return SpecializedCollections.EmptyEnumerable<RazorDiagnostic>();
        }

        var diagnostics = Node.GetDiagnostics();

        return diagnostics.Length == 0
            ? SpecializedCollections.EmptyEnumerable<RazorDiagnostic>()
            : diagnostics;
    }
}
