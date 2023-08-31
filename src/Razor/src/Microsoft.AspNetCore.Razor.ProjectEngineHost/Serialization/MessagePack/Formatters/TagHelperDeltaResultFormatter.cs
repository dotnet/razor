// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using MessagePack;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters.TagHelpers;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters;

internal sealed class TagHelperDeltaResultFormatter : MessagePackFormatter<TagHelperDeltaResult>
{
    public static readonly MessagePackFormatter<TagHelperDeltaResult> Instance = new TagHelperDeltaResultFormatter();

    private TagHelperDeltaResultFormatter()
    {
    }

    public override TagHelperDeltaResult Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        reader.ReadArrayHeaderAndVerify(4);

        using var cachingOptions = new CachingOptions(options);

        var delta = reader.ReadBoolean();
        var resultId = reader.ReadInt32();
        var added = reader.Deserialize<ImmutableArray<TagHelperDescriptor>>(cachingOptions);
        var removed = reader.Deserialize<ImmutableArray<TagHelperDescriptor>>(cachingOptions);

        return new(delta, resultId, added, removed);
    }

    public override void Serialize(ref MessagePackWriter writer, TagHelperDeltaResult value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(4);

        using var cachingOptions = new CachingOptions(options);

        writer.Write(value.Delta);
        writer.Write(value.ResultId);
        writer.SerializeObject(value.Added, cachingOptions);
        writer.SerializeObject(value.Removed, cachingOptions);
    }
}
