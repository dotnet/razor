// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public sealed class FieldDeclarationIntermediateNode(
    ImmutableArray<string> modifiers,
    string fieldName,
    string fieldType,
    ImmutableArray<string> suppressWarnings = default,
    bool isTagHelperField = false) : MemberDeclarationIntermediateNode
{
    public override IntermediateNodeCollection Children => IntermediateNodeCollection.ReadOnly;

    public ImmutableArray<string> Modifiers { get; } = modifiers.NullToEmpty();

    public ImmutableArray<string> SuppressWarnings { get; } = suppressWarnings.NullToEmpty();

    public string FieldName { get; } = fieldName;

    public string FieldType { get; } = fieldType;

    public bool IsTagHelperField { get; } = isTagHelperField;

    public override void Accept(IntermediateNodeVisitor visitor)
        => visitor.VisitFieldDeclaration(this);

    public override void FormatNode(IntermediateNodeFormatter formatter)
    {
        formatter.WriteContent(FieldName);

        formatter.WriteProperty(nameof(FieldName), FieldName);
        formatter.WriteProperty(nameof(FieldType), FieldType);
        formatter.WriteProperty(nameof(Modifiers), string.Join(" ", Modifiers));
    }
}
