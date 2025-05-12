// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.Language.Syntax;

/// <summary>
/// Represents a read-only list of <see cref="SyntaxToken"/>.
/// </summary>
internal readonly partial struct SyntaxTokenList(SyntaxNode? node) : IEquatable<SyntaxTokenList>, IReadOnlyList<SyntaxToken>
{
    internal SyntaxNode? Node { get; } = node;

    /// <summary>
    /// Creates a singleton list of syntax tokens.
    /// </summary>
    /// <param name="node">The single element node.</param>
    public SyntaxTokenList(SyntaxToken? node)
        : this((SyntaxNode?)node)
    {
    }

    /// <summary>
    /// Creates a list of syntax nodes.
    /// </summary>
    /// <param name="nodes">A sequence of element nodes.</param>
    public SyntaxTokenList(SyntaxList<SyntaxToken> nodes)
        : this(CreateNode(nodes))
    {
    }

    private static SyntaxNode? CreateNode(SyntaxList<SyntaxToken> nodes)
    {
        using var builder = new PooledArrayBuilder<SyntaxToken>(nodes.Count);
        builder.AddRange(nodes);

        return builder.ToList().Node;
    }

    /// <summary>
    /// The number of nodes in the list.
    /// </summary>
    public int Count
    {
        get
        {
            return Node == null ? 0 : (Node.IsList ? Node.SlotCount : 1);
        }
    }

    /// <summary>
    /// Gets the node at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the node to get or set.</param>
    /// <returns>The node at the specified index.</returns>
    public SyntaxToken this[int index]
    {
        get
        {
            if (Node != null)
            {
                if (Node.IsList)
                {
                    if (unchecked((uint)index < (uint)Node.SlotCount))
                    {
                        return (SyntaxToken)Node.GetNodeSlot(index).AssumeNotNull();
                    }
                }
                else if (index == 0)
                {
                    return (SyntaxToken)Node;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    internal SyntaxNode? ItemInternal(int index)
    {
        if (Node?.IsList is true)
        {
            return Node.GetNodeSlot(index);
        }

        Debug.Assert(index == 0);
        return Node;
    }

    /// <summary>
    /// The absolute span of the list elements in characters.
    /// </summary>
    public TextSpan Span
        => Count > 0
            ? TextSpan.FromBounds(this[0].Span.Start, this[Count - 1].Span.End)
            : default;

    /// <summary>
    /// Returns the string representation of the nodes in this list.
    /// </summary>
    /// <returns>
    /// The string representation of the nodes in this list.
    /// </returns>
    public override string ToString()
        => Node?.ToString() ?? string.Empty;

    /// <summary>
    /// Creates a new list with the specified node added at the end.
    /// </summary>
    /// <param name="node">The node to add.</param>
    public SyntaxTokenList Add(SyntaxToken node)
    {
        return Insert(Count, node);
    }

    /// <summary>
    /// Creates a new list with the specified nodes added at the end.
    /// </summary>
    /// <param name="nodes">The nodes to add.</param>
    public SyntaxTokenList AddRange(IEnumerable<SyntaxToken> nodes)
    {
        return InsertRange(Count, nodes);
    }

    /// <summary>
    /// Creates a new list with the specified node inserted at the index.
    /// </summary>
    /// <param name="index">The index to insert at.</param>
    /// <param name="node">The node to insert.</param>
    public SyntaxTokenList Insert(int index, SyntaxToken node)
    {
        ArgHelper.ThrowIfNull(node);

        return InsertRange(index, new[] { node });
    }

    /// <summary>
    /// Creates a new list with the specified nodes inserted at the index.
    /// </summary>
    /// <param name="index">The index to insert at.</param>
    /// <param name="nodes">The nodes to insert.</param>
    public SyntaxTokenList InsertRange(int index, IEnumerable<SyntaxToken> nodes)
    {
        ArgHelper.ThrowIfNegative(index);
        ArgHelper.ThrowIfGreaterThan(index, Count);
        ArgHelper.ThrowIfNull(nodes);

        var list = this.ToList();
        list.InsertRange(index, nodes);

        if (list.Count == 0)
        {
            return this;
        }
        else
        {
            return CreateList(list[0].Green, list);
        }
    }

    /// <summary>
    /// Creates a new list with the element at specified index removed.
    /// </summary>
    /// <param name="index">The index of the element to remove.</param>
    public SyntaxTokenList RemoveAt(int index)
    {
        ArgHelper.ThrowIfNegative(index);
        ArgHelper.ThrowIfGreaterThan(index, Count);

        return Remove(this[index]);
    }

    /// <summary>
    /// Creates a new list with the element removed.
    /// </summary>
    /// <param name="node">The element to remove.</param>
    public SyntaxTokenList Remove(SyntaxToken node)
    {
        return CreateList(this.Where(x => x != node).ToList());
    }

    /// <summary>
    /// Creates a new list with the specified element replaced with the new node.
    /// </summary>
    /// <param name="nodeInList">The element to replace.</param>
    /// <param name="newNode">The new node.</param>
    public SyntaxTokenList Replace(SyntaxToken nodeInList, SyntaxToken newNode)
    {
        return ReplaceRange(nodeInList, new[] { newNode });
    }

    /// <summary>
    /// Creates a new list with the specified element replaced with new nodes.
    /// </summary>
    /// <param name="nodeInList">The element to replace.</param>
    /// <param name="newNodes">The new nodes.</param>
    public SyntaxTokenList ReplaceRange(SyntaxToken nodeInList, IEnumerable<SyntaxToken> newNodes)
    {
        ArgHelper.ThrowIfNull(nodeInList);
        ArgHelper.ThrowIfNull(newNodes);

        var index = IndexOf(nodeInList);
        if (index >= 0 && index < Count)
        {
            var list = this.ToList();
            list.RemoveAt(index);
            list.InsertRange(index, newNodes);
            return CreateList(list);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(nodeInList));
        }
    }

    private static SyntaxTokenList CreateList(List<SyntaxToken> items)
    {
        return items.Count != 0
            ? CreateList(items[0].Green, items)
            : default;
    }

    private static SyntaxTokenList CreateList(GreenNode creator, List<SyntaxToken> items)
    {
        if (items.Count == 0)
        {
            return default;
        }

        var newGreen = creator.CreateList(items.Select(n => n.Green));
        return new SyntaxTokenList(newGreen.CreateRed());
    }

    /// <summary>
    /// The first node in the list.
    /// </summary>
    public SyntaxToken First() => this[0];

    /// <summary>
    /// The first node in the list or default if the list is empty.
    /// </summary>
    public SyntaxToken? FirstOrDefault() => Any() ? this[0] : null;

    /// <summary>
    /// The last node in the list.
    /// </summary>
    public SyntaxToken Last() => this[^1];

    /// <summary>
    /// The last node in the list or default if the list is empty.
    /// </summary>
    public SyntaxToken? LastOrDefault() => Any() ? this[^1] : null;

    /// <summary>
    /// True if the list has at least one node.
    /// </summary>
    public bool Any()
    {
        Debug.Assert(Node == null || Count != 0);
        return Node != null;
    }

    public SyntaxTokenList Where(Func<SyntaxToken, bool> predicate)
    {
        using var builder = new PooledArrayBuilder<SyntaxToken>(Count);

        foreach (var node in this)
        {
            if (predicate(node))
            {
                builder.Add(node);
            }
        }

        return builder.ToList();
    }

    // for debugging
#pragma warning disable IDE0051 // Remove unused private members
    private SyntaxToken[] Tokens => [.. this];
#pragma warning restore IDE0051 // Remove unused private members

    /// <summary>
    /// Get's the enumerator for this list.
    /// </summary>
    public Enumerator GetEnumerator()
    {
        return new Enumerator(in this);
    }

    IEnumerator<SyntaxToken> IEnumerable<SyntaxToken>.GetEnumerator()
    {
        if (Any())
        {
            return new EnumeratorImpl(this);
        }

        return SpecializedCollections.EmptyEnumerator<SyntaxToken>();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        if (Any())
        {
            return new EnumeratorImpl(this);
        }

        return SpecializedCollections.EmptyEnumerator<SyntaxToken>();
    }

    public static bool operator ==(SyntaxTokenList left, SyntaxTokenList right)
        => left.Node == right.Node;

    public static bool operator !=(SyntaxTokenList left, SyntaxTokenList right)
        => left.Node != right.Node;

    public bool Equals(SyntaxTokenList other)
        => Node == other.Node;

    public override bool Equals(object? obj)
        => obj is SyntaxTokenList list &&
           Equals(list);

    public override int GetHashCode()
    {
        return Node?.GetHashCode() ?? 0;
    }

    public static implicit operator SyntaxTokenList(SyntaxList<SyntaxToken> list)
    {
        return new SyntaxTokenList(list.Node);
    }

    public static implicit operator SyntaxList<SyntaxToken>(SyntaxTokenList list)
    {
        return new SyntaxList<SyntaxToken>(list.Node);
    }

    /// <summary>
    /// The index of the node in this list, or -1 if the node is not in the list.
    /// </summary>
    public int IndexOf(SyntaxToken node)
    {
        var index = 0;
        foreach (var child in this)
        {
            if (object.Equals(child, node))
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    public int IndexOf(Func<SyntaxToken, bool> predicate)
    {
        var index = 0;
        foreach (var child in this)
        {
            if (predicate(child))
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    internal int IndexOf(SyntaxKind kind)
    {
        var index = 0;
        foreach (var child in this)
        {
            if (child.Kind == kind)
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    public int LastIndexOf(SyntaxToken node)
    {
        for (var i = Count - 1; i >= 0; i--)
        {
            if (object.Equals(this[i], node))
            {
                return i;
            }
        }

        return -1;
    }

    public int LastIndexOf(Func<SyntaxToken, bool> predicate)
    {
        for (var i = Count - 1; i >= 0; i--)
        {
            if (predicate(this[i]))
            {
                return i;
            }
        }

        return -1;
    }

    public struct Enumerator
    {
        private readonly SyntaxTokenList _list;
        private int _index;

        internal Enumerator(in SyntaxTokenList list)
        {
            _list = list;
            _index = -1;
        }

        public bool MoveNext()
        {
            var newIndex = _index + 1;
            if (newIndex < _list.Count)
            {
                _index = newIndex;
                return true;
            }

            return false;
        }

        public readonly SyntaxToken Current
            => (SyntaxToken)_list.ItemInternal(_index)!;

        public void Reset()
        {
            _index = -1;
        }

        public override bool Equals(object? obj)
        {
            throw new NotSupportedException();
        }

        public override int GetHashCode()
        {
            throw new NotSupportedException();
        }
    }

    private class EnumeratorImpl : IEnumerator<SyntaxToken>
    {
        private Enumerator _e;

        internal EnumeratorImpl(in SyntaxTokenList list)
        {
            _e = new Enumerator(in list);
        }

        public bool MoveNext()
        {
            return _e.MoveNext();
        }

        public SyntaxToken Current
        {
            get
            {
                return _e.Current;
            }
        }

        void IDisposable.Dispose()
        {
        }

        object IEnumerator.Current
        {
            get
            {
                return _e.Current;
            }
        }

        void IEnumerator.Reset()
        {
            _e.Reset();
        }
    }
}
