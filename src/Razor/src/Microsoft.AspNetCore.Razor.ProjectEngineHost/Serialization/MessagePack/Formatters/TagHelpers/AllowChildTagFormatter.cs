// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MessagePack;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.Serialization.MessagePack.Formatters.TagHelpers;

internal sealed class AllowedChildTagFormatter : MessagePackFormatter<AllowedChildTagDescriptor>
{
    public static readonly MessagePackFormatter<AllowedChildTagDescriptor> Instance = new AllowedChildTagFormatter();

    private AllowedChildTagFormatter()
    {
    }

    public override AllowedChildTagDescriptor Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var cache = options as CachingOptions;

        reader.ReadArrayHeaderAndVerify(3);

        var name = reader.ReadString(cache);
        var displayName = reader.ReadString(cache);
        var diagnostics = reader.DeserializeObject<RazorDiagnostic[]>(options);

        return new DefaultAllowedChildTagDescriptor(name, displayName, diagnostics);
    }

    public override void Serialize(ref MessagePackWriter writer, AllowedChildTagDescriptor value, MessagePackSerializerOptions options)
    {
        var cache = options as CachingOptions;

        writer.WriteArrayHeader(3);

        writer.Write(value.Name, cache);
        writer.Write(value.DisplayName, cache);
        writer.SerializeObject(value.DisplayName, options);
    }
}
