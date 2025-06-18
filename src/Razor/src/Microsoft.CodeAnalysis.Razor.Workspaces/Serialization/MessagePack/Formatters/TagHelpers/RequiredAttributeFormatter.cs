// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using MessagePack;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.Serialization.MessagePack.Formatters.TagHelpers;

internal sealed class RequiredAttributeFormatter : ValueFormatter<RequiredAttributeDescriptor>
{
    private const int PropertyCount = 8;

    public static readonly ValueFormatter<RequiredAttributeDescriptor> Instance = new RequiredAttributeFormatter();

    private RequiredAttributeFormatter()
    {
    }

    public override RequiredAttributeDescriptor Deserialize(ref MessagePackReader reader, SerializerCachingOptions options)
    {
        reader.ReadArrayHeaderAndVerify(PropertyCount);

        var flags = (RequiredAttributeDescriptorFlags)reader.ReadInt32();
        var name = CachedStringFormatter.Instance.Deserialize(ref reader, options);
        var nameComparison = (RequiredAttributeNameComparison)reader.ReadInt32();
        var value = CachedStringFormatter.Instance.Deserialize(ref reader, options);
        var valueComparison = (RequiredAttributeValueComparison)reader.ReadInt32();
        var displayName = CachedStringFormatter.Instance.Deserialize(ref reader, options).AssumeNotNull();

        var metadata = reader.Deserialize<MetadataCollection>(options);
        var diagnostics = reader.Deserialize<ImmutableArray<RazorDiagnostic>>(options);

        return new RequiredAttributeDescriptor(
            flags, name!, nameComparison,
            value, valueComparison,
            displayName, diagnostics, metadata);
    }

    public override void Serialize(ref MessagePackWriter writer, RequiredAttributeDescriptor value, SerializerCachingOptions options)
    {
        writer.WriteArrayHeader(PropertyCount);

        writer.Write((int)value.Flags);
        CachedStringFormatter.Instance.Serialize(ref writer, value.Name, options);
        writer.Write((int)value.NameComparison);
        CachedStringFormatter.Instance.Serialize(ref writer, value.Value, options);
        writer.Write((int)value.ValueComparison);
        CachedStringFormatter.Instance.Serialize(ref writer, value.DisplayName, options);

        writer.Serialize(value.Metadata, options);
        writer.Serialize(value.Diagnostics, options);
    }

    public override void Skim(ref MessagePackReader reader, SerializerCachingOptions options)
    {
        reader.ReadArrayHeaderAndVerify(PropertyCount);

        reader.Skip(); // Flags
        CachedStringFormatter.Instance.Skim(ref reader, options); // Name
        reader.Skip(); // NameComparison
        CachedStringFormatter.Instance.Skim(ref reader, options); // Value
        reader.Skip(); // ValueComparison
        CachedStringFormatter.Instance.Skim(ref reader, options); // DisplayName

        MetadataCollectionFormatter.Instance.Skim(ref reader, options); // Metadata
        RazorDiagnosticFormatter.Instance.SkimArray(ref reader, options); // Diagnostics
    }
}
