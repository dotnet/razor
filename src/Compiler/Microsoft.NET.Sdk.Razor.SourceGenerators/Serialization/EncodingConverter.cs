using Newtonsoft.Json;
using System;
using System.Text;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

internal sealed class EncodingConverter : JsonConverter<Encoding>
{
    public override Encoding? ReadJson(JsonReader reader, Type objectType, Encoding? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var name = reader.ReadAsString();
        return name is null ? null : Encoding.GetEncoding(name);
    }

    public override void WriteJson(JsonWriter writer, Encoding? value, JsonSerializer serializer)
    {
        writer.WriteValue(value?.WebName);
    }
}
