// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MessagePack;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters.TagHelpers;

internal sealed class TagMatchingRuleFormatter : MessagePackFormatter<TagMatchingRuleDescriptor>
{
    public static readonly MessagePackFormatter<TagMatchingRuleDescriptor> Instance = new TagMatchingRuleFormatter();

    private TagMatchingRuleFormatter()
    {
    }

    public override TagMatchingRuleDescriptor Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var cache = options as CachingOptions;

        reader.ReadArrayHeaderAndVerify(6);

        var tagName = reader.ReadString(cache).AssumeNotNull();
        var parentTag = reader.ReadString(cache);
        var tagStructure = (TagStructure)reader.ReadInt32();
        var caseSensitive = reader.ReadBoolean();
        var attributes = reader.Deserialize<RequiredAttributeDescriptor[]>(options);
        var diagnostics = reader.Deserialize<RazorDiagnostic[]>(options);

        return new DefaultTagMatchingRuleDescriptor(
            tagName, parentTag,
            tagStructure, caseSensitive,
            attributes, diagnostics);
    }

    public override void Serialize(ref MessagePackWriter writer, TagMatchingRuleDescriptor value, MessagePackSerializerOptions options)
    {
        var cache = options as CachingOptions;

        writer.WriteArrayHeader(6);

        writer.Write(value.TagName, cache);
        writer.Write(value.ParentTag, cache);
        writer.Write((int)value.TagStructure);
        writer.Write(value.CaseSensitive);
        writer.Serialize((RequiredAttributeDescriptor[])value.Attributes, options);
        writer.Serialize((RazorDiagnostic[])value.Diagnostics, options);
    }
}
