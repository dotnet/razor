// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MessagePack;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters.TagHelpers;

internal sealed class TagHelperFormatter : MessagePackFormatter<TagHelperDescriptor>
{
    public static readonly MessagePackFormatter<TagHelperDescriptor> Instance = new TagHelperFormatter();

    private TagHelperFormatter()
    {
    }

    public override TagHelperDescriptor Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var cache = options as CachingOptions;

        reader.ReadArrayHeaderAndVerify(12);

        var kind = reader.ReadString(cache).AssumeNotNull();
        var name = reader.ReadString(cache).AssumeNotNull();
        var assemblyName = reader.ReadString(cache).AssumeNotNull();

        var displayName = reader.ReadString(cache);
        var documentationObject = reader.DeserializeObject<DocumentationObject>(options);
        var tagOutputHint = reader.ReadString(cache);
        var caseSensitive = reader.ReadBoolean();

        var tagMatchingRules = reader.DeserializeObject<TagMatchingRuleDescriptor[]>(options);
        var boundAttributes = reader.DeserializeObject<BoundAttributeDescriptor[]>(options);
        var allowedChildTags = reader.DeserializeObject<AllowedChildTagDescriptor[]>(options);

        var metadata = reader.DeserializeObject<MetadataCollection>(options);
        var diagnostics = reader.DeserializeObject<RazorDiagnostic[]>(options);

        return new DefaultTagHelperDescriptor(
            kind, name, assemblyName,
            displayName!, documentationObject,
            tagOutputHint, caseSensitive,
            tagMatchingRules, boundAttributes, allowedChildTags,
            metadata, diagnostics);
    }

    public override void Serialize(ref MessagePackWriter writer, TagHelperDescriptor value, MessagePackSerializerOptions options)
    {
        var cache = options as CachingOptions;

        writer.WriteArrayHeader(12);

        writer.Write(value.Kind, cache);
        writer.Write(value.Name, cache);
        writer.Write(value.AssemblyName, cache);

        writer.Write(value.DisplayName, cache);
        writer.SerializeObject(value.DocumentationObject, options);
        writer.Write(value.TagOutputHint, cache);
        writer.Write(value.CaseSensitive);

        writer.SerializeObject((TagMatchingRuleDescriptor[])value.TagMatchingRules, options);
        writer.SerializeObject((BoundAttributeDescriptor[])value.BoundAttributes, options);
        writer.SerializeObject((AllowedChildTagDescriptor[])value.AllowedChildTags, options);

        writer.SerializeObject((MetadataCollection)value.Metadata, options);
        writer.SerializeObject((RazorDiagnostic[])value.Diagnostics, options);
    }
}
