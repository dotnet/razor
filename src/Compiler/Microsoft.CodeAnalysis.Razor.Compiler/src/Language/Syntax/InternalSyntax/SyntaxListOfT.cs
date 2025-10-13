// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace Microsoft.AspNetCore.Razor.Language.Syntax.InternalSyntax;

internal readonly struct SyntaxList<TNode> : IEquatable<SyntaxList<TNode>>
    where TNode : GreenNode
{
    private readonly GreenNode? _node;

    public SyntaxList(GreenNode? node)
    {
        _node = node;
    }

    internal GreenNode? Node => _node;

    public int Count => _node == null ? 0 : (_node.IsList ? _node.SlotCount : 1);

    public TNode? this[int index]
    {
        get
        {
            if (_node == null)
            {
                return null;
            }
            else if (_node.IsList)
            {
                Debug.Assert(index >= 0);
                Debug.Assert(index <= _node.SlotCount);

                return (TNode?)_node.GetSlot(index);
            }
            else if (index == 0)
            {
                return (TNode?)_node;
            }
            else
            {
                return Assumed.Unreachable<TNode?>();
            }
        }
    }

    internal TNode GetRequiredItem(int index)
    {
        var node = this[index];
        Debug.Assert(node is object);

        return node;
    }

    public GreenNode? ItemUntyped(int index)
    {
        Debug.Assert(_node is not null);

        var node = _node;
        if (node.IsList)
        {
            return node.GetSlot(index);
        }

        Debug.Assert(index == 0);
        return node;
    }

    public bool Any()
    {
        return _node != null;
    }

    public bool Any(SyntaxKind kind)
    {
        foreach (var element in this)
        {
            if (element.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    public TNode[] Nodes
    {
        get
        {
            var count = Count;
            var result = new TNode[count];

            for (var i = 0; i < count; i++)
            {
                result[i] = GetRequiredItem(i);
            }

            return result;
        }
    }

    public TNode? Last
    {
        get
        {
            Debug.Assert(_node is not null);

            var node = _node;
            if (node.IsList)
            {
                return (TNode?)node.GetSlot(node.SlotCount - 1);
            }

            return (TNode?)node;
        }
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    public static bool operator ==(SyntaxList<TNode> left, SyntaxList<TNode> right)
    {
        return left._node == right._node;
    }

    public static bool operator !=(SyntaxList<TNode> left, SyntaxList<TNode> right)
    {
        return !(left._node == right._node);
    }

    public bool Equals(SyntaxList<TNode> other)
    {
        return _node == other._node;
    }

    public override bool Equals(object? obj)
    {
        return obj is SyntaxList<TNode> other && _node == other._node;
    }

    public override int GetHashCode()
    {
        return _node != null ? _node.GetHashCode() : 0;
    }

    public static implicit operator SyntaxList<TNode>(TNode? node)
    {
        return new SyntaxList<TNode>(node);
    }

    public static implicit operator SyntaxList<GreenNode>(SyntaxList<TNode> nodes)
    {
        return new SyntaxList<GreenNode>(nodes._node);
    }

    internal struct Enumerator
    {
        private readonly SyntaxList<TNode> _list;
        private int _index;

        internal Enumerator(SyntaxList<TNode> list)
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

        public readonly TNode Current
            => _list[_index]!;
    }
}
