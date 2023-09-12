// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MessagePack;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters.TagHelpers;

internal sealed class TagMatchingRuleFormatter : ValueFormatter<TagMatchingRuleDescriptor>
{
    public static readonly ValueFormatter<TagMatchingRuleDescriptor> Instance = new TagMatchingRuleFormatter();

    private TagMatchingRuleFormatter()
    {
    }

    protected override TagMatchingRuleDescriptor Deserialize(ref MessagePackReader reader, SerializerCachingOptions options)
    {
        reader.ReadArrayHeaderAndVerify(6);

        var tagName = CachedStringFormatter.Instance.Deserialize(ref reader, options).AssumeNotNull();
        var parentTag = CachedStringFormatter.Instance.Deserialize(ref reader, options);
        var tagStructure = (TagStructure)reader.ReadInt32();
        var caseSensitive = reader.ReadBoolean();
        var attributes = reader.Deserialize<RequiredAttributeDescriptor[]>(options);
        var diagnostics = reader.Deserialize<RazorDiagnostic[]>(options);

        return new DefaultTagMatchingRuleDescriptor(
            tagName, parentTag,
            tagStructure, caseSensitive,
            attributes, diagnostics);
    }

    protected override void Serialize(ref MessagePackWriter writer, TagMatchingRuleDescriptor value, SerializerCachingOptions options)
    {
        writer.WriteArrayHeader(6);

        CachedStringFormatter.Instance.Serialize(ref writer, value.TagName, options);
        CachedStringFormatter.Instance.Serialize(ref writer, value.ParentTag, options);
        writer.Write((int)value.TagStructure);
        writer.Write(value.CaseSensitive);
        writer.Serialize((RequiredAttributeDescriptor[])value.Attributes, options);
        writer.Serialize((RazorDiagnostic[])value.Diagnostics, options);
    }
}
