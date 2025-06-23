// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using MessagePack;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.Serialization.MessagePack.Formatters.TagHelpers;

internal sealed class RequiredAttributeFormatter : ValueFormatter<RequiredAttributeDescriptor>
{
    private const int PropertyCount = 6;

    public static readonly ValueFormatter<RequiredAttributeDescriptor> Instance = new RequiredAttributeFormatter();

    private RequiredAttributeFormatter()
    {
    }

    public override RequiredAttributeDescriptor Deserialize(ref MessagePackReader reader, SerializerCachingOptions options)
    {
        reader.ReadArrayHeaderAndVerify(PropertyCount);

        var flags = (RequiredAttributeDescriptorFlags)reader.ReadByte();
        var name = CachedStringFormatter.Instance.Deserialize(ref reader, options);
        var nameComparison = (RequiredAttributeNameComparison)reader.ReadByte();
        var value = CachedStringFormatter.Instance.Deserialize(ref reader, options);
        var valueComparison = (RequiredAttributeValueComparison)reader.ReadByte();

        var diagnostics = reader.Deserialize<ImmutableArray<RazorDiagnostic>>(options);

        return new RequiredAttributeDescriptor(
            flags, name!, nameComparison, value, valueComparison, diagnostics);
    }

    public override void Serialize(ref MessagePackWriter writer, RequiredAttributeDescriptor value, SerializerCachingOptions options)
    {
        writer.WriteArrayHeader(PropertyCount);

        writer.Write((byte)value.Flags);
        CachedStringFormatter.Instance.Serialize(ref writer, value.Name, options);
        writer.Write((byte)value.NameComparison);
        CachedStringFormatter.Instance.Serialize(ref writer, value.Value, options);
        writer.Write((byte)value.ValueComparison);

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

        RazorDiagnosticFormatter.Instance.SkimArray(ref reader, options); // Diagnostics
    }
}
