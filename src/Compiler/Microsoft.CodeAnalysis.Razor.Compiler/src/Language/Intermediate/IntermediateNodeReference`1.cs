// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct IntermediateNodeReference<TNode>
    where TNode : IntermediateNode
{
    public IntermediateNode Parent { get; }
    public TNode Node { get; }

    private IntermediateNodeReference UntypedReference => new(Parent, Node);

    public IntermediateNodeReference(IntermediateNode parent, TNode node)
    {
        ArgHelper.ThrowIfNull(parent);
        ArgHelper.ThrowIfNull(node);

        Parent = parent;
        Node = node;
    }

    public void Deconstruct(out IntermediateNode parent, out TNode node)
    {
        parent = Parent;
        node = Node;
    }

    // Node: Just delegate the mutation operations to the non-generic IntermediateNodeReference.
    public void InsertAfter(IntermediateNode node)
        => UntypedReference.InsertAfter(node);

    public void InsertAfter(IEnumerable<IntermediateNode> nodes)
        => UntypedReference.InsertAfter(nodes);

    public void InsertBefore(IntermediateNode node)
        => UntypedReference.InsertBefore(node);

    public void InsertBefore(IEnumerable<IntermediateNode> nodes)
        => UntypedReference.InsertBefore(nodes);

    public void Remove()
        => UntypedReference.Remove();

    public void Replace(IntermediateNode node)
        => UntypedReference.Replace(node);

    private string GetDebuggerDisplay()
        => $"ref: {Parent.GetDebuggerDisplay()} - {Node.GetDebuggerDisplay()}";

    public static implicit operator IntermediateNodeReference(IntermediateNodeReference<TNode> reference)
        => new(reference.Parent, reference.Node);
}
