// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class RequiredAttributeDescriptor : TagHelperObject, IEquatable<RequiredAttributeDescriptor>
{
    public string Name { get; }
    public NameComparisonMode NameComparison { get; }
    public string? Value { get; }
    public ValueComparisonMode ValueComparison { get; }
    public string DisplayName { get; }
    public bool CaseSensitive { get; }

    public MetadataCollection Metadata { get; }

    internal RequiredAttributeDescriptor(
        string name,
        NameComparisonMode nameComparison,
        bool caseSensitive,
        string? value,
        ValueComparisonMode valueComparison,
        string displayName,
        ImmutableArray<RazorDiagnostic> diagnostics,
        MetadataCollection metadata)
        : base(diagnostics)
    {
        Name = name;
        NameComparison = nameComparison;
        CaseSensitive = caseSensitive;
        Value = value;
        ValueComparison = valueComparison;
        DisplayName = displayName;
        Metadata = metadata ?? MetadataCollection.Empty;
    }

    public override string ToString()
    {
        return DisplayName ?? base.ToString();
    }

    public bool Equals(RequiredAttributeDescriptor other)
    {
        return RequiredAttributeDescriptorComparer.Default.Equals(this, other);
    }

    public override bool Equals(object? obj)
    {
        return obj is RequiredAttributeDescriptor other &&
               Equals(other);
    }

    public override int GetHashCode()
    {
        return RequiredAttributeDescriptorComparer.Default.GetHashCode(this);
    }

    /// <summary>
    /// Acceptable <see cref="Name"/> comparison modes.
    /// </summary>
    public enum NameComparisonMode
    {
        /// <summary>
        /// HTML attribute name case insensitively matches <see cref="Name"/>.
        /// </summary>
        FullMatch,

        /// <summary>
        /// HTML attribute name case insensitively starts with <see cref="Name"/>.
        /// </summary>
        PrefixMatch,
    }

    /// <summary>
    /// Acceptable <see cref="Value"/> comparison modes.
    /// </summary>
    public enum ValueComparisonMode
    {
        /// <summary>
        /// HTML attribute value always matches <see cref="Value"/>.
        /// </summary>
        None,

        /// <summary>
        /// HTML attribute value case sensitively matches <see cref="Value"/>.
        /// </summary>
        FullMatch,

        /// <summary>
        /// HTML attribute value case sensitively starts with <see cref="Value"/>.
        /// </summary>
        PrefixMatch,

        /// <summary>
        /// HTML attribute value case sensitively ends with <see cref="Value"/>.
        /// </summary>
        SuffixMatch,
    }
}
