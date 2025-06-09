// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

[DebuggerDisplay("{DebuggerToString(),nq}")]
public abstract class IntermediateNode
{
    private ItemCollection _annotations;
    private RazorDiagnosticCollection _diagnostics;

    public ItemCollection Annotations
    {
        get
        {
            if (_annotations == null)
            {
                _annotations = new ItemCollection();
            }

            return _annotations;
        }
    }

    public abstract IntermediateNodeCollection Children { get; }

    public RazorDiagnosticCollection Diagnostics
    {
        get
        {
            if (_diagnostics == null)
            {
                _diagnostics = new RazorDiagnosticCollection();
            }

            return _diagnostics;
        }
    }

    public bool HasDiagnostics => _diagnostics != null && _diagnostics.Count > 0;

    public SourceSpan? Source { get; set; }

    public abstract void Accept(IntermediateNodeVisitor visitor);

    [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members")]
    private string Tree
    {
        get
        {
            using var _ = StringBuilderPool.GetPooledObject(out var builder);

            var formatter = new IntermediateNodeFormatter(builder);
            formatter.FormatTree(this);

            return builder.ToString();
        }
    }

    internal string DebuggerToString()
    {
        using var _ = StringBuilderPool.GetPooledObject(out var builder);

        var formatter = new IntermediateNodeFormatter(builder);
        formatter.FormatNode(this);

        return builder.ToString();
    }

    public virtual void FormatNode(IntermediateNodeFormatter formatter)
    {
    }
}
