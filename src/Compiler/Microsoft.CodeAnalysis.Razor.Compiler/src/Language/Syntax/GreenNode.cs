// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language.Syntax;

[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
internal abstract class GreenNode
{
    private static readonly RazorDiagnostic[] s_noDiagnostics = [];
    private static readonly SyntaxAnnotation[] s_noAnnotations = [];
    private static readonly IEnumerable<SyntaxAnnotation> s_noAnnotationsEnumerable = SpecializedCollections.EmptyEnumerable<SyntaxAnnotation>();

    private static readonly ConditionalWeakTable<GreenNode, RazorDiagnostic[]> DiagnosticsTable = new();
    private static readonly ConditionalWeakTable<GreenNode, SyntaxAnnotation[]> s_annotationsTable = new();

    /// <summary>
    /// Pool of StringWriters for use in <see cref="ToString()"/>. Users should not dispose the StringWriter directly
    /// (but should dispose of the PooledObject returned from Pool.GetPooledObject).
    /// </summary>
    private static readonly ObjectPool<StringWriter> StringWriterPool = DefaultPool.Create(Policy.Instance);

    private int _width;
    private byte _slotCount;

    protected GreenNode(SyntaxKind kind)
    {
        Kind = kind;
    }

    protected GreenNode(SyntaxKind kind, int width)
        : this(kind)
    {
        _width = width;
    }

    protected GreenNode(SyntaxKind kind, RazorDiagnostic[]? diagnostics, SyntaxAnnotation[]? annotations)
        : this(kind, 0, diagnostics, annotations)
    {
    }

    protected GreenNode(SyntaxKind kind, int width, RazorDiagnostic[]? diagnostics, SyntaxAnnotation[]? annotations)
        : this(kind, width)
    {
        if (diagnostics?.Length > 0)
        {
            Flags |= NodeFlags.ContainsDiagnostics;
            DiagnosticsTable.Add(this, diagnostics);
        }

        if (annotations?.Length > 0)
        {
            foreach (var annotation in annotations)
            {
                if (annotation == null)
                {
                    throw new ArgumentException("Annotation cannot be null", nameof(annotations));
                }
            }

            SetFlags(NodeFlags.HasAnnotationsDirectly | NodeFlags.ContainsAnnotations);
            s_annotationsTable.Add(this, annotations);
        }
    }

    protected void AdjustFlagsAndWidth(GreenNode node)
    {
        if (node == null)
        {
            return;
        }

        SetFlags(node.Flags & NodeFlags.InheritMask);
        _width += node.Width;
    }

    #region Kind
    internal SyntaxKind Kind { get; }

    internal virtual bool IsList => false;

    internal virtual bool IsToken => false;
    #endregion

    public int Width => _width;

    #region Slots
    public int SlotCount
    {
        get
        {
            int count = _slotCount;
            if (count == byte.MaxValue)
            {
                count = GetSlotCount();
            }

            return count;
        }

        protected set
        {
            _slotCount = (byte)value;
        }
    }

    internal abstract GreenNode? GetSlot(int index);

    internal GreenNode GetRequiredSlot(int index)
    {
        var node = GetSlot(index);
        Debug.Assert(node is not null);

        return node;
    }

    // for slot counts >= byte.MaxValue
    protected virtual int GetSlotCount()
    {
        return _slotCount;
    }

    public virtual int GetSlotOffset(int index)
    {
        var offset = 0;
        for (var i = 0; i < index; i++)
        {
            var child = GetSlot(i);
            if (child != null)
            {
                offset += child.Width;
            }
        }

        return offset;
    }

    public virtual int FindSlotIndexContainingOffset(int offset)
    {
        Debug.Assert(0 <= offset && offset < Width);

        int i;
        var accumulatedWidth = 0;
        for (i = 0; ; i++)
        {
            Debug.Assert(i < SlotCount);
            var child = GetSlot(i);
            if (child != null)
            {
                accumulatedWidth += child.Width;
                if (offset < accumulatedWidth)
                {
                    break;
                }
            }
        }

        return i;
    }
    #endregion

    #region Flags
    public NodeFlags Flags { get; protected set; }

    internal void SetFlags(NodeFlags flags)
    {
        Flags |= flags;
    }

    internal void ClearFlags(NodeFlags flags)
    {
        Flags &= ~flags;
    }

    internal virtual bool IsMissing => (Flags & NodeFlags.IsMissing) != 0;

    public bool ContainsDiagnostics => (Flags & NodeFlags.ContainsDiagnostics) != 0;

    public bool ContainsAnnotations => (Flags & NodeFlags.ContainsAnnotations) != 0;

    public bool HasAnnotationsDirectly => (Flags & NodeFlags.HasAnnotationsDirectly) != 0;
    #endregion

    #region Diagnostics
    internal abstract GreenNode SetDiagnostics(RazorDiagnostic[]? diagnostics);

    internal RazorDiagnostic[] GetDiagnostics()
    {
        if (ContainsDiagnostics)
        {
            if (DiagnosticsTable.TryGetValue(this, out var diagnostics))
            {
                return diagnostics;
            }
        }

        return s_noDiagnostics;
    }
    #endregion

    #region Annotations

    public bool HasAnnotations(string annotationKind)
    {
        var annotations = GetAnnotations();
        if (annotations == s_noAnnotations)
        {
            return false;
        }

        foreach (var a in annotations)
        {
            if (a.Kind == annotationKind)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasAnnotations(IEnumerable<string> annotationKinds)
    {
        var annotations = GetAnnotations();
        if (annotations == s_noAnnotations)
        {
            return false;
        }

        foreach (var a in annotations)
        {
            if (annotationKinds.Contains(a.Kind))
            {
                return true;
            }
        }

        return false;
    }

    public bool HasAnnotation([NotNullWhen(true)] SyntaxAnnotation? annotation)
    {
        var annotations = GetAnnotations();
        if (annotations == s_noAnnotations)
        {
            return false;
        }

        foreach (var a in annotations)
        {
            if (a == annotation)
            {
                return true;
            }
        }

        return false;
    }

    public IEnumerable<SyntaxAnnotation> GetAnnotations(string annotationKind)
    {
        ArgHelper.ThrowIfNullOrWhiteSpace(annotationKind);

        var annotations = GetAnnotations();

        if (annotations == s_noAnnotations)
        {
            return s_noAnnotationsEnumerable;
        }

        return GetAnnotationsSlow(annotations, annotationKind);

        static IEnumerable<SyntaxAnnotation> GetAnnotationsSlow(SyntaxAnnotation[] annotations, string annotationKind)
        {
            foreach (var a in annotations)
            {
                if (a.Kind == annotationKind)
                {
                    yield return a;
                }
            }
        }
    }

    public IEnumerable<SyntaxAnnotation> GetAnnotations(IEnumerable<string> annotationKinds)
    {
        ArgHelper.ThrowIfNull(annotationKinds);

        var annotations = GetAnnotations();

        if (annotations == s_noAnnotations)
        {
            return s_noAnnotationsEnumerable;
        }

        return GetAnnotationsSlow(annotations, annotationKinds);

        static IEnumerable<SyntaxAnnotation> GetAnnotationsSlow(SyntaxAnnotation[] annotations, IEnumerable<string> annotationKinds)
        {
            foreach (var a in annotations)
            {
                if (annotationKinds.Contains(a.Kind))
                {
                    yield return a;
                }
            }
        }
    }

    internal SyntaxAnnotation[] GetAnnotations()
    {
        if (!HasAnnotationsDirectly)
        {
            return s_noAnnotations;
        }

        var found = s_annotationsTable.TryGetValue(this, out var annotations);

        Debug.Assert(found, "We must be able to find annotations since we had the bit set on ourselves");
        Debug.Assert(annotations != null, "annotations should not be null");
        Debug.Assert(annotations != s_noAnnotations, "annotations should not be s_noAnnotations");
        Debug.Assert(annotations.Length != 0, "annotations should be non-empty");

        return annotations;
    }

    internal abstract GreenNode SetAnnotations(SyntaxAnnotation[]? annotations);

    #endregion

    #region Text
    private string GetDebuggerDisplay()
    {
        using var _ = StringBuilderPool.GetPooledObject(out var builder);
        builder.Append(GetType().Name);
        builder.Append('<');
        builder.Append(Kind.ToString());
        builder.Append('>');

        return builder.ToString();
    }

    public override string ToString()
    {
        // If there is only a single value in our slots, then we can just defer to the ToString
        // implementation on that item, as it may avoid the need to allocate a string
        if (TryGetSingleSlotValue(out var loneSlotValue))
        {
            return loneSlotValue.ToString();
        }

        using var _ = StringWriterPool.GetPooledObject(out var writer);

        WriteTo(writer);

        return writer.ToString();

        bool TryGetSingleSlotValue([NotNullWhen(true)] out GreenNode? result)
        {
            result = null;

            var slotCount = SlotCount;
            for (var i = 0; i < slotCount; i++)
            {
                var slotValue = GetSlot(i);
                if (slotValue is not null)
                {
                    if (result is not null)
                    {
                        result = null;
                        return false;
                    }

                    result = slotValue;
                }
            }

            return result is not null;
        }
    }

    public void WriteTo(TextWriter writer)
    {
        // Use an actual Stack so we can write out deeply recursive structures without overflowing.
        using var stack = new PooledArrayBuilder<GreenNode>();

        stack.Push(this);

        while (stack.Count > 0)
        {
            var node = stack.Pop();

            if (node.IsToken)
            {
                node.WriteTokenTo(writer);
                continue;
            }

            var slotCount = node.SlotCount;

            for (var i = slotCount - 1; i >= 0; i--)
            {
                if (node.GetSlot(i) is GreenNode child)
                {
                    stack.Push(child);
                }
            }
        }
    }

    protected virtual void WriteTokenTo(TextWriter writer)
    {
        throw new NotImplementedException();
    }
    #endregion

    #region Equivalence
    public virtual bool IsEquivalentTo([NotNullWhen(true)] GreenNode? other)
    {
        if (this == other)
        {
            return true;
        }

        if (other == null)
        {
            return false;
        }

        return EquivalentToInternal(this, other);
    }

    private static bool EquivalentToInternal(GreenNode node1, GreenNode node2)
    {
        if (node1.Kind != node2.Kind)
        {
            // A single-element list is usually represented as just a single node,
            // but can be represented as a List node with one child. Move to that
            // child if necessary.
            if (node1.IsList && node1.SlotCount == 1)
            {
                node1 = node1.GetRequiredSlot(0);
            }

            if (node2.IsList && node2.SlotCount == 1)
            {
                node2 = node2.GetRequiredSlot(0);
            }

            if (node1.Kind != node2.Kind)
            {
                return false;
            }
        }

        if (node1.Width != node2.Width)
        {
            return false;
        }

        var n = node1.SlotCount;
        if (n != node2.SlotCount)
        {
            return false;
        }

        for (var i = 0; i < n; i++)
        {
            var node1Child = node1.GetSlot(i);
            var node2Child = node2.GetSlot(i);
            if (node1Child != null && node2Child != null && !node1Child.IsEquivalentTo(node2Child))
            {
                return false;
            }
        }

        return true;
    }
    #endregion

    #region Factories
    public SyntaxNode CreateRed()
    {
        return CreateRed(null, 0);
    }

    internal abstract SyntaxNode CreateRed(SyntaxNode? parent, int position);
    #endregion

    public abstract TResult Accept<TResult>(InternalSyntax.SyntaxVisitor<TResult> visitor);

    public abstract void Accept(InternalSyntax.SyntaxVisitor visitor);

    private sealed class Policy : IPooledObjectPolicy<StringWriter>
    {
        public static readonly Policy Instance = new();

        private Policy()
        {
        }

        public StringWriter Create()
            => new StringWriter(new StringBuilder(), CultureInfo.InvariantCulture);

        public bool Return(StringWriter writer)
        {
            var builder = writer.GetStringBuilder();

            // Very similar to StringBuilderPool.Policy implementation.
            builder.Clear();
            if (builder.Capacity > DefaultPool.MaximumObjectSize)
            {
                builder.Capacity = DefaultPool.MaximumObjectSize;
            }

            return true;
        }
    }
}
