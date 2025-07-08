// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public sealed class NamespaceDeclarationIntermediateNode(bool isPrimaryNamespace = false) : IntermediateNode
{
    private IntermediateNodeCollection? _children;

    public bool IsPrimaryNamespace { get; } = isPrimaryNamespace;

    public string? Content { get; set; }

    public bool IsGenericTyped { get; set; }

    public override IntermediateNodeCollection Children
        => _children ??= [];

    public override void Accept(IntermediateNodeVisitor visitor)
        => visitor.VisitNamespaceDeclaration(this);

    public override void FormatNode(IntermediateNodeFormatter formatter)
    {
        formatter.WriteContent(Content);

        formatter.WriteProperty(nameof(Content), Content);
    }
}
