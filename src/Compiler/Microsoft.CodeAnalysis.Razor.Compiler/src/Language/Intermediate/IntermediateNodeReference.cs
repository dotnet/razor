// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct IntermediateNodeReference
{
    public IntermediateNode Parent { get; }
    public IntermediateNode Node { get; }

    public IntermediateNodeReference(IntermediateNode parent, IntermediateNode node)
    {
        ArgHelper.ThrowIfNull(parent);
        ArgHelper.ThrowIfNull(node);

        Parent = parent;
        Node = node;
    }

    public void Deconstruct(out IntermediateNode parent, out IntermediateNode node)
    {
        parent = Parent;
        node = Node;
    }

    private void ThrowIfParentIsNull()
    {
        if (Parent is null)
        {
            ThrowHelper.ThrowInvalidOperationException(Resources.IntermediateNodeReference_NotInitialized);
        }
    }

    private void ThrowIfParentIsReadOnly()
    {
        if (Parent.Children.IsReadOnly)
        {
            ThrowHelper.ThrowInvalidOperationException(Resources.FormatIntermediateNodeReference_CollectionIsReadOnly(Parent));
        }
    }

    private int GetNodeIndex()
    {
        var index = Parent.Children.IndexOf(Node);

        if (index == -1)
        {
            ThrowHelper.ThrowInvalidOperationException(Resources.FormatIntermediateNodeReference_NodeNotFound(Node, Parent));
        }

        return index;
    }

    public void InsertAfter(IntermediateNode node)
    {
        ArgHelper.ThrowIfNull(node);

        ThrowIfParentIsNull();
        ThrowIfParentIsReadOnly();

        var index = GetNodeIndex();

        Parent.Children.Insert(index + 1, node);
    }

    public void InsertAfter(IEnumerable<IntermediateNode> nodes)
    {
        ArgHelper.ThrowIfNull(nodes);

        ThrowIfParentIsNull();
        ThrowIfParentIsReadOnly();

        var index = GetNodeIndex();

        foreach (var node in nodes)
        {
            Parent.Children.Insert(++index, node);
        }
    }

    public void InsertBefore(IntermediateNode node)
    {
        ArgHelper.ThrowIfNull(node);

        ThrowIfParentIsNull();
        ThrowIfParentIsReadOnly();

        var index = GetNodeIndex();

        Parent.Children.Insert(index, node);
    }

    public void InsertBefore(IEnumerable<IntermediateNode> nodes)
    {
        ArgHelper.ThrowIfNull(nodes);

        ThrowIfParentIsNull();
        ThrowIfParentIsReadOnly();

        var index = GetNodeIndex();

        foreach (var node in nodes)
        {
            Parent.Children.Insert(index++, node);
        }
    }

    public void Remove()
    {
        ThrowIfParentIsNull();
        ThrowIfParentIsReadOnly();

        var index = GetNodeIndex();

        Parent.Children.RemoveAt(index);
    }

    public void Replace(IntermediateNode node)
    {
        ArgHelper.ThrowIfNull(node);

        ThrowIfParentIsNull();
        ThrowIfParentIsReadOnly();

        var index = GetNodeIndex();

        Parent.Children[index] = node;
    }

    private string GetDebuggerDisplay()
        => $"ref: {Parent.GetDebuggerDisplay()} - {Node.GetDebuggerDisplay()}";
}
