// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public sealed class PropertyDeclarationIntermediateNode(
    ImmutableArray<string> modifiers,
    CSharpIntermediateToken propertyType,
    string propertyName,
    string propertyExpression) : MemberDeclarationIntermediateNode
{
    public override IntermediateNodeCollection Children => IntermediateNodeCollection.ReadOnly;

    public ImmutableArray<string> Modifiers { get; } = modifiers.NullToEmpty();
    public CSharpIntermediateToken PropertyType { get; } = propertyType;
    public string PropertyName { get; } = propertyName;
    public string PropertyExpression { get; } = propertyExpression;

    public override void Accept(IntermediateNodeVisitor visitor)
        => visitor.VisitPropertyDeclaration(this);
}
