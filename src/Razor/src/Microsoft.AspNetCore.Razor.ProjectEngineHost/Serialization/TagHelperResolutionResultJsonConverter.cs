// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal class TagHelperResolutionResultJsonConverter : ObjectJsonConverter<TagHelperResolutionResult>
{
    public static readonly TagHelperResolutionResultJsonConverter Instance = new();

    public TagHelperResolutionResultJsonConverter()
    {
    }

    protected override TagHelperResolutionResult ReadFromProperties(JsonReader reader)
    {
        Data data = default;
        reader.ReadProperties(ref data, Data.PropertyMap);

        return new(data.Descriptors, data.Diagnostics);
    }

    private record struct Data(TagHelperDescriptor[] Descriptors, RazorDiagnostic[] Diagnostics)
    {
        public static readonly PropertyMap<Data> PropertyMap = new(
            (nameof(Data.Descriptors), ReadDescriptors),
            (nameof(Data.Diagnostics), ReadDiagnostics));

        public static void ReadDescriptors(JsonReader reader, ref Data data)
            => data.Descriptors = reader.ReadArrayOrEmpty(static reader => ObjectReaders.ReadTagHelper(reader, useCache: true));

        public static void ReadDiagnostics(JsonReader reader, ref Data data)
            => data.Diagnostics = reader.ReadArrayOrEmpty(ObjectReaders.ReadDiagnostic);
    }

    protected override void WriteProperties(JsonWriter writer, TagHelperResolutionResult value)
    {
        writer.WriteArray(nameof(value.Descriptors), value.Descriptors, ObjectWriters.Write);
        writer.WriteArray(nameof(value.Diagnostics), value.Diagnostics, ObjectWriters.Write);
    }
}
