// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public sealed class FieldDeclarationIntermediateNode(
    string fieldName,
    string fieldType,
    ImmutableArray<string> modifiers,
    ImmutableArray<string> suppressWarnings,
    bool isTagHelperField = false) : MemberDeclarationIntermediateNode
{
    public string FieldName { get; } = fieldName;
    public string FieldType { get; } = fieldType;

    public bool IsTagHelperField { get; } = isTagHelperField;

    public ImmutableArray<string> Modifiers { get; } = modifiers.NullToEmpty();
    public ImmutableArray<string> SuppressWarnings { get; } = suppressWarnings.NullToEmpty();

    public override IntermediateNodeCollection Children => IntermediateNodeCollection.ReadOnly;

    public FieldDeclarationIntermediateNode(
        string fieldName,
        string fieldType,
        ImmutableArray<string> modifiers,
        bool isTagHelperField = false)
        : this(fieldName, fieldType, modifiers, suppressWarnings: [], isTagHelperField)
    {
    }

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
