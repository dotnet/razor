// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MessagePack;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters.TagHelpers;

internal sealed class BoundAttributeFormatter : MessagePackFormatter<BoundAttributeDescriptor>
{
    public static readonly MessagePackFormatter<BoundAttributeDescriptor> Instance = new BoundAttributeFormatter();

    private BoundAttributeFormatter()
    {
    }

    public override BoundAttributeDescriptor Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var cache = options as CachingOptions;

        reader.ReadArrayHeaderAndVerify(14);

        var kind = reader.ReadString(cache).AssumeNotNull();
        var name = reader.ReadString(cache);
        var typeName = reader.ReadString(cache);
        var isEnum = reader.ReadBoolean();
        var hasIndexer = reader.ReadBoolean();
        var indexerNamePrefix = reader.ReadString(cache);
        var indexerTypeName = reader.ReadString(cache);
        var displayName = reader.ReadString(cache);
        var documentationObject = reader.DeserializeObject<DocumentationObject>(options);
        var caseSensitive = reader.ReadBoolean();
        var isEditorRequired = reader.ReadBoolean();
        var parameters = reader.DeserializeObject<BoundAttributeParameterDescriptor[]>(options);

        var metadata = reader.DeserializeObject<MetadataCollection>(options);
        var diagnostics = reader.DeserializeObject<RazorDiagnostic[]>(options);

        return new DefaultBoundAttributeDescriptor(
            kind, name, typeName, isEnum,
            hasIndexer, indexerNamePrefix, indexerTypeName,
            documentationObject, displayName, caseSensitive, isEditorRequired,
            parameters, metadata, diagnostics);
    }

    public override void Serialize(ref MessagePackWriter writer, BoundAttributeDescriptor value, MessagePackSerializerOptions options)
    {
        var cache = options as CachingOptions;

        writer.WriteArrayHeader(14);

        writer.Write(value.Kind, cache);
        writer.Write(value.Name, cache);
        writer.Write(value.TypeName, cache);
        writer.Write(value.IsEnum);
        writer.Write(value.HasIndexer);
        writer.Write(value.IndexerNamePrefix, cache);
        writer.Write(value.IndexerTypeName, cache);
        writer.Write(value.DisplayName, cache);
        writer.SerializeObject(value.DocumentationObject, options);
        writer.Write(value.CaseSensitive);
        writer.Write(value.IsEditorRequired);
        writer.SerializeObject(value.BoundAttributeParameters, options);

        writer.SerializeObject((MetadataCollection)value.Metadata, options);
        writer.SerializeObject((RazorDiagnostic[])value.Diagnostics, options);
    }
}
