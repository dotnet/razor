// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.Language;

public class AllowedChildTagDescriptor : TagHelperObject, IEquatable<AllowedChildTagDescriptor>
{
    public string Name { get; }
    public string DisplayName { get; }

    internal AllowedChildTagDescriptor(string name, string displayName, ImmutableArray<RazorDiagnostic> diagnostics)
    {
        Name = name;
        DisplayName = displayName;

        if (!diagnostics.IsDefaultOrEmpty)
        {
            SetFlag(ContainsDiagnosticsBit);
            TagHelperDiagnostics.AddDiagnostics(this, diagnostics);
        }
    }

    public ImmutableArray<RazorDiagnostic> Diagnostics
        => HasFlag(ContainsDiagnosticsBit)
            ? TagHelperDiagnostics.GetDiagnostics(this)
            : ImmutableArray<RazorDiagnostic>.Empty;

    public bool HasErrors
        => HasFlag(ContainsDiagnosticsBit) &&
           Diagnostics.Any(static d => d.Severity == RazorDiagnosticSeverity.Error);

    public override string ToString()
        => DisplayName ?? base.ToString();

    public bool Equals(AllowedChildTagDescriptor other)
        => AllowedChildTagDescriptorComparer.Default.Equals(this, other);

    public override bool Equals(object? obj)
        => obj is AllowedChildTagDescriptor other &&
           Equals(other);

    public override int GetHashCode()
        => AllowedChildTagDescriptorComparer.Default.GetHashCode(this);
}
