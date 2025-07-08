// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public sealed class ClassDeclarationIntermediateNode(bool isPrimaryClass = false) : MemberDeclarationIntermediateNode
{
    private IntermediateNodeCollection? _children;
    private ImmutableArray<string> _modifiers = [];
    private ImmutableArray<IntermediateToken> _interfaces = [];
    private ImmutableArray<TypeParameter> _typeParameters = [];

    public bool IsPrimaryClass { get; } = isPrimaryClass;

    public ImmutableArray<string> Modifiers
    {
        get => _modifiers;
        init => _modifiers = value.NullToEmpty();
    }

    public string? ClassName { get; set; }

    public BaseTypeWithModel? BaseType { get; set; }

    public ImmutableArray<IntermediateToken> Interfaces
    {
        get => _interfaces;
        init => _interfaces = value.NullToEmpty();
    }

    public ImmutableArray<TypeParameter> TypeParameters
    {
        get => _typeParameters;
        init => _typeParameters = value.NullToEmpty();
    }

    public bool NullableContext { get; set; }

    public override IntermediateNodeCollection Children
        => _children ??= [];

    public void UpdateModifiers(params ImmutableArray<string> modifiers)
        => _modifiers = modifiers.NullToEmpty();

    public void UpdateInterfaces(params ImmutableArray<IntermediateToken> interfaces)
        => _interfaces = interfaces.NullToEmpty();

    public void UpdateTypeParameters(params ImmutableArray<TypeParameter> typeParameters)
        => _typeParameters = typeParameters.NullToEmpty();

    public override void Accept(IntermediateNodeVisitor visitor)
        => visitor.VisitClassDeclaration(this);

    public override void FormatNode(IntermediateNodeFormatter formatter)
    {
        formatter.WriteContent(ClassName);

        formatter.WriteProperty(nameof(ClassName), ClassName);
        formatter.WriteProperty(nameof(Interfaces), string.Join(", ", Interfaces.Select(i => i.Content)));
        formatter.WriteProperty(nameof(Modifiers), string.Join(", ", Modifiers));
        formatter.WriteProperty(nameof(TypeParameters), string.Join(", ", TypeParameters.Select(t => t.ParameterName)));
    }
}
