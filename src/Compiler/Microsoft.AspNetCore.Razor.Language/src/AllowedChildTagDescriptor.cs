// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class AllowedChildTagDescriptor : TagHelperObject, IEquatable<AllowedChildTagDescriptor>
{
    public string Name { get; }
    public string DisplayName { get; }

    internal AllowedChildTagDescriptor(string name, string displayName, ImmutableArray<RazorDiagnostic> diagnostics)
        : base(diagnostics)
    {
        Name = name;
        DisplayName = displayName;
    }

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
