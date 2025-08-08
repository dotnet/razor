// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public sealed class MethodDeclarationIntermediateNode(bool isPrimaryMethod = false) : MemberDeclarationIntermediateNode
{
    private IntermediateNodeCollection? _children;
    private ImmutableArray<string> _modifiers = [];
    private ImmutableArray<MethodParameter> _parameters = [];

    public bool IsPrimaryMethod { get; } = isPrimaryMethod;

    public ImmutableArray<string> Modifiers
    {
        get => _modifiers;
        init => _modifiers = value.NullToEmpty();
    }

    public string? MethodName { get; set; }

    public ImmutableArray<MethodParameter> Parameters
    {
        get => _parameters;
        init => _parameters = value.NullToEmpty();
    }

    public string? ReturnType { get; set; }

    public override IntermediateNodeCollection Children
        => _children ??= [];

    public void UpdateModifiers(params ImmutableArray<string> modifiers)
        => _modifiers = modifiers.NullToEmpty();

    public void UpdateParameters(params ImmutableArray<MethodParameter> parameters)
        => _parameters = parameters.NullToEmpty();

    public override void Accept(IntermediateNodeVisitor visitor)
        => visitor.VisitMethodDeclaration(this);

    public override void FormatNode(IntermediateNodeFormatter formatter)
    {
        formatter.WriteContent(MethodName);

        formatter.WriteProperty(nameof(MethodName), MethodName);
        formatter.WriteProperty(nameof(Modifiers), string.Join(", ", Modifiers));
        formatter.WriteProperty(nameof(Parameters), string.Join(", ", Parameters.Select(FormatMethodParameter)));
        formatter.WriteProperty(nameof(ReturnType), ReturnType);
    }

    private static string FormatMethodParameter(MethodParameter parameter)
    {
        using var _ = StringBuilderPool.GetPooledObject(out var builder);

        foreach (var modifier in parameter.Modifiers)
        {
            builder.Append(modifier);
            builder.Append(' ');
        }

        builder.Append(parameter.TypeName);
        builder.Append(' ');

        builder.Append(parameter.ParameterName);

        return builder.ToString();
    }
}
