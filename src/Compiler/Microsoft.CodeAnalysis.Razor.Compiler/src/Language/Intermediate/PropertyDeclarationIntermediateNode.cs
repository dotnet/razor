// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public sealed class PropertyDeclarationIntermediateNode(
    string propertyName,
    IntermediateToken propertyType,
    string propertyExpression,
    ImmutableArray<string> modifiers) : MemberDeclarationIntermediateNode
{
    public string PropertyName { get; } = propertyName;
    public IntermediateToken PropertyType { get; } = propertyType;
    public string PropertyExpression { get; } = propertyExpression;

    public ImmutableArray<string> Modifiers { get; } = modifiers.NullToEmpty();

    public override IntermediateNodeCollection Children => IntermediateNodeCollection.ReadOnly;

    public override void Accept(IntermediateNodeVisitor visitor)
        => visitor.VisitPropertyDeclaration(this);
}
