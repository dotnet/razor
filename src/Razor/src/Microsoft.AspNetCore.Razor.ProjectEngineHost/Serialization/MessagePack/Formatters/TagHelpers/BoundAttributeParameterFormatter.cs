// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MessagePack;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters.TagHelpers;

internal sealed class BoundAttributeParameterFormatter : ValueFormatter<BoundAttributeParameterDescriptor>
{
    public static readonly ValueFormatter<BoundAttributeParameterDescriptor> Instance = new BoundAttributeParameterFormatter();

    private BoundAttributeParameterFormatter()
    {
    }

    protected override BoundAttributeParameterDescriptor Deserialize(ref MessagePackReader reader, SerializerCachingOptions options)
    {
        reader.ReadArrayHeaderAndVerify(9);

        var kind = CachedStringFormatter.Instance.Deserialize(ref reader, options).AssumeNotNull();
        var name = CachedStringFormatter.Instance.Deserialize(ref reader, options);
        var typeName = CachedStringFormatter.Instance.Deserialize(ref reader, options);
        var isEnum = reader.ReadBoolean();
        var displayName = CachedStringFormatter.Instance.Deserialize(ref reader, options);
        var documentationObject = reader.Deserialize<DocumentationObject>(options);
        var caseSensitive = reader.ReadBoolean();

        var metadata = reader.Deserialize<MetadataCollection>(options);
        var diagnostics = reader.Deserialize<RazorDiagnostic[]>(options);

        return new DefaultBoundAttributeParameterDescriptor(
            kind, name, typeName,
            isEnum, documentationObject, displayName, caseSensitive,
            metadata, diagnostics);
    }

    protected override void Serialize(ref MessagePackWriter writer, BoundAttributeParameterDescriptor value, SerializerCachingOptions options)
    {
        writer.WriteArrayHeader(9);

        CachedStringFormatter.Instance.Serialize(ref writer, value.Kind, options);
        CachedStringFormatter.Instance.Serialize(ref writer, value.Name, options);
        CachedStringFormatter.Instance.Serialize(ref writer, value.TypeName, options);
        writer.Write(value.IsEnum);
        CachedStringFormatter.Instance.Serialize(ref writer, value.DisplayName, options);
        writer.SerializeObject(value.DocumentationObject, options);
        writer.Write(value.CaseSensitive);

        writer.Serialize((MetadataCollection)value.Metadata, options);
        writer.Serialize((RazorDiagnostic[])value.Diagnostics, options);
    }
}
