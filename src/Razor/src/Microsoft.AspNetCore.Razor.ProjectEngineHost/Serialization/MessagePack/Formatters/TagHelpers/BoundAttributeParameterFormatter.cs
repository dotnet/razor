// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MessagePack;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters.TagHelpers;

internal sealed class BoundAttributeParameterFormatter : MessagePackFormatter<BoundAttributeParameterDescriptor>
{
    public static readonly MessagePackFormatter<BoundAttributeParameterDescriptor> Instance = new BoundAttributeParameterFormatter();

    private BoundAttributeParameterFormatter()
    {
    }

    public override BoundAttributeParameterDescriptor Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var cache = options as CachingOptions;

        reader.ReadArrayHeaderAndVerify(9);

        var kind = reader.ReadString(cache).AssumeNotNull();
        var name = reader.ReadString(cache);
        var typeName = reader.ReadString(cache);
        var isEnum = reader.ReadBoolean();
        var displayName = reader.ReadString(cache);
        var documentationObject = reader.DeserializeObject<DocumentationObject>(options);
        var caseSensitive = reader.ReadBoolean();

        var metadata = reader.DeserializeObject<MetadataCollection>(options);
        var diagnostics = reader.DeserializeObject<RazorDiagnostic[]>(options);

        return new DefaultBoundAttributeParameterDescriptor(
            kind, name, typeName,
            isEnum, documentationObject, displayName, caseSensitive,
            metadata, diagnostics);
    }

    public override void Serialize(ref MessagePackWriter writer, BoundAttributeParameterDescriptor value, MessagePackSerializerOptions options)
    {
        var cache = options as CachingOptions;

        writer.WriteArrayHeader(9);

        writer.Write(value.Kind, cache);
        writer.Write(value.Name, cache);
        writer.Write(value.TypeName, cache);
        writer.Write(value.IsEnum);
        writer.Write(value.DisplayName, cache);
        writer.SerializeObject(value.DocumentationObject, options);
        writer.Write(value.CaseSensitive);

        writer.SerializeObject((MetadataCollection)value.Metadata, options);
        writer.SerializeObject((RazorDiagnostic[])value.Diagnostics, options);
    }
}
