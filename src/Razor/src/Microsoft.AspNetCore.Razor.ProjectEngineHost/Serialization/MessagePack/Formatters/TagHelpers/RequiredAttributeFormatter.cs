// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MessagePack;
using Microsoft.AspNetCore.Razor.Language;
using static Microsoft.AspNetCore.Razor.Language.RequiredAttributeDescriptor;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters.TagHelpers;

internal sealed class RequiredAttributeFormatter : MessagePackFormatter<RequiredAttributeDescriptor>
{
    public static readonly MessagePackFormatter<RequiredAttributeDescriptor> Instance = new RequiredAttributeFormatter();

    private RequiredAttributeFormatter()
    {
    }

    public override RequiredAttributeDescriptor Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var cache = options as CachingOptions;

        reader.ReadArrayHeaderAndVerify(8);

        var name = reader.ReadString(cache);
        var nameComparison = (NameComparisonMode)reader.ReadInt32();
        var caseSensitive = reader.ReadBoolean();
        var value = reader.ReadString(cache);
        var valueComparison = (ValueComparisonMode)reader.ReadInt32();
        var displayName = reader.ReadString(cache);

        var metadata = reader.Deserialize<MetadataCollection>(options);
        var diagnostics = reader.Deserialize<RazorDiagnostic[]>(options);

        return new DefaultRequiredAttributeDescriptor(
            name, nameComparison,
            caseSensitive,
            value, valueComparison,
            displayName!, diagnostics, metadata);
    }

    public override void Serialize(ref MessagePackWriter writer, RequiredAttributeDescriptor value, MessagePackSerializerOptions options)
    {
        var cache = options as CachingOptions;

        writer.WriteArrayHeader(8);

        writer.Write(value.Name, cache);
        writer.Write((int)value.NameComparison);
        writer.Write(value.CaseSensitive);
        writer.Write(value.Value, cache);
        writer.Write((int)value.ValueComparison);
        writer.Write(value.DisplayName, cache);

        writer.Serialize((MetadataCollection)value.Metadata, options);
        writer.Serialize((RazorDiagnostic[])value.Diagnostics, options);
    }
}
