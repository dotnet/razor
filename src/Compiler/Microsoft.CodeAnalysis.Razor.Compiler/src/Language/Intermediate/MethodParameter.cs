// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public sealed class MethodParameter(ImmutableArray<string> modifiers, string typeName, string parameterName)
{
    public ImmutableArray<string> Modifiers { get; } = modifiers.NullToEmpty();
    public string TypeName { get; } = typeName;
    public string ParameterName { get; } = parameterName;

    public MethodParameter(string typeName, string parameterName)
        : this(modifiers: [], typeName, parameterName)
    {
    }
}
