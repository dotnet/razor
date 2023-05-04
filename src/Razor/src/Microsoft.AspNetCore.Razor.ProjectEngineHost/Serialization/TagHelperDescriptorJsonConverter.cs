// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal class TagHelperDescriptorJsonConverter : ObjectJsonConverter<TagHelperDescriptor>
{
    public static readonly TagHelperDescriptorJsonConverter Instance = new();

    public static bool DisableCachingForTesting { private get; set; } = false;

    private TagHelperDescriptorJsonConverter()
    {
    }

    protected override TagHelperDescriptor ReadFromProperties(JsonReader reader)
        => ObjectReaders.ReadTagHelperFromProperties(reader, !DisableCachingForTesting);

    protected override void WriteProperties(JsonWriter writer, TagHelperDescriptor value)
        => ObjectWriters.WriteProperties(writer, value);
}
