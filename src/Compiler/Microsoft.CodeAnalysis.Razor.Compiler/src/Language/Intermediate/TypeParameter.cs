// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language.Intermediate;

public sealed class TypeParameter(
    string parameterName,
    SourceSpan? parameterNameSource = null,
    string? constraints = null,
    SourceSpan? constraintsSource = null)
{
    public string ParameterName { get; } = parameterName;
    public SourceSpan? ParameterNameSource { get; } = parameterNameSource;
    public string? Constraints { get; } = constraints;
    public SourceSpan? ConstraintsSource { get; } = constraintsSource;
}
