// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable enable

using System;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public sealed class RenderModeIntermediateNode : IntermediateNode
{
    public RenderModeIntermediateNode(IntermediateNode expressionNode)
    {
        ExpressionNode = expressionNode ?? throw new ArgumentNullException(nameof(expressionNode));
        Source = ExpressionNode.Source;
    }

    public override IntermediateNodeCollection Children => IntermediateNodeCollection.ReadOnly;

    public IntermediateNode ExpressionNode { get; }

    public override void Accept(IntermediateNodeVisitor visitor)
    {
        if (visitor == null)
        {
            throw new ArgumentNullException(nameof(visitor));
        }

        visitor.VisitRenderMode(this);
    }

    public override void FormatNode(IntermediateNodeFormatter formatter)
    {
        if (formatter == null)
        {
            throw new ArgumentNullException(nameof(formatter));
        }

        ExpressionNode.FormatNode(formatter);
    }
}
