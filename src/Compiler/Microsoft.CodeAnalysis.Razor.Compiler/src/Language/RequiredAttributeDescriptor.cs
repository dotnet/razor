// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Utilities;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class RequiredAttributeDescriptor : TagHelperObject<RequiredAttributeDescriptor>
{
    private readonly RequiredAttributeDescriptorFlags _flags;
    private TagMatchingRuleDescriptor? _parent;

    internal RequiredAttributeDescriptorFlags Flags => _flags;

    public string Name { get; }
    public RequiredAttributeNameComparison NameComparison { get; }
    public string? Value { get; }
    public RequiredAttributeValueComparison ValueComparison { get; }
    public string DisplayName { get; }

    public bool CaseSensitive => _flags.IsFlagSet(RequiredAttributeDescriptorFlags.CaseSensitive);
    public bool IsDirectiveAttribute => _flags.IsFlagSet(RequiredAttributeDescriptorFlags.IsDirectiveAttribute);

    public MetadataCollection Metadata { get; }

    internal RequiredAttributeDescriptor(
        RequiredAttributeDescriptorFlags flags,
        string name,
        RequiredAttributeNameComparison nameComparison,
        string? value,
        RequiredAttributeValueComparison valueComparison,
        string displayName,
        ImmutableArray<RazorDiagnostic> diagnostics,
        MetadataCollection metadata)
        : base(diagnostics)
    {
        _flags = flags;
        Name = name;
        NameComparison = nameComparison;
        Value = value;
        ValueComparison = valueComparison;
        DisplayName = displayName;
        Metadata = metadata ?? MetadataCollection.Empty;
    }

    private protected override void BuildChecksum(in Checksum.Builder builder)
    {
        builder.AppendData((int)Flags);
        builder.AppendData(Name);
        builder.AppendData((int)NameComparison);
        builder.AppendData(Value);
        builder.AppendData((int)ValueComparison);
        builder.AppendData(DisplayName);
        builder.AppendData(Metadata.Checksum);
    }

    public TagMatchingRuleDescriptor Parent
        => _parent ?? ThrowHelper.ThrowInvalidOperationException<TagMatchingRuleDescriptor>(Resources.Parent_has_not_been_set);

    internal void SetParent(TagMatchingRuleDescriptor parent)
    {
        Debug.Assert(parent != null);
        Debug.Assert(_parent == null);

        _parent = parent;
    }

    public override string ToString()
    {
        return DisplayName ?? base.ToString()!;
    }
}
