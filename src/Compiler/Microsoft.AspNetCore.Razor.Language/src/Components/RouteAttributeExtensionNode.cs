﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Components;

internal sealed class RouteAttributeExtensionNode : ExtensionIntermediateNode
{
    public RouteAttributeExtensionNode(ReadOnlyMemory<char> template)
    {
        Template = template;
    }

    public ReadOnlyMemory<char> Template { get; }

    public override IntermediateNodeCollection Children => IntermediateNodeCollection.ReadOnly;

    public override void Accept(IntermediateNodeVisitor visitor) => AcceptExtensionNode(this, visitor);

    public override void WriteNode(CodeTarget target, CodeRenderingContext context)
    {
        context.CodeWriter.Write("[");
        context.CodeWriter.Write("global::");
        context.CodeWriter.Write(ComponentsApi.RouteAttribute.FullTypeName);
        context.CodeWriter.Write("(\"");
        context.CodeWriter.Write(Template);
        context.CodeWriter.Write("\")");
        context.CodeWriter.Write("]");
        context.CodeWriter.WriteLine();
    }
}
