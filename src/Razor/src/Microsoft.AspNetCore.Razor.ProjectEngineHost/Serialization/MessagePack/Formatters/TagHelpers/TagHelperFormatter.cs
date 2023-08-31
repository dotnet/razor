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
        var documentationObject = reader.Deserialize<DocumentationObject>(options);
        var tagOutputHint = reader.ReadString(cache);
        var caseSensitive = reader.ReadBoolean();

        var tagMatchingRules = reader.Deserialize<TagMatchingRuleDescriptor[]>(options);
        var boundAttributes = reader.Deserialize<BoundAttributeDescriptor[]>(options);
        var allowedChildTags = reader.Deserialize<AllowedChildTagDescriptor[]>(options);

        var metadata = reader.Deserialize<MetadataCollection>(options);
        var diagnostics = reader.Deserialize<RazorDiagnostic[]>(options);

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

        writer.Serialize((TagMatchingRuleDescriptor[])value.TagMatchingRules, options);
        writer.Serialize((BoundAttributeDescriptor[])value.BoundAttributes, options);
        writer.Serialize((AllowedChildTagDescriptor[])value.AllowedChildTags, options);

        writer.Serialize((MetadataCollection)value.Metadata, options);
        writer.Serialize((RazorDiagnostic[])value.Diagnostics, options);
    }
}
