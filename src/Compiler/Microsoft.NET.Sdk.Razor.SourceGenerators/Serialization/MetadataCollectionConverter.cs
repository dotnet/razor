using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

internal sealed class MetadataCollectionConverter : JsonConverter<MetadataCollection>
{
    public override MetadataCollection? ReadJson(JsonReader reader, Type objectType, MetadataCollection? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.StartObject)
        {
            return null;
        }
        
        //PROTOTYPE: we should special case 0,1,2,3
        //// read the length
        //reader.Read();
        //Debug.Assert(reader.TokenType == JsonToken.PropertyName && reader.Value is string s && s == "Count");

        //reader.Read();
        //Debug.Assert(reader.TokenType == JsonToken.Integer);
        //var count = (int)reader.Value!;

        //var result = existingValue ?? count switch
        //{
        //    0 => MetadataCollection.Empty,
        //    1 or 2 or 3 => MetadataCollection.
        //}

        var result = new Dictionary<string, string>();
        while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
        {
            var propertyName = (string)reader.Value!;
            var value = reader.ReadAsString();

            result.Add(propertyName, value ?? string.Empty);
        }

        Debug.Assert(reader.TokenType == JsonToken.EndObject);
        return MetadataCollection.Create(result);
    }

    public override void WriteJson(JsonWriter writer, MetadataCollection? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        //writer.WritePropertyName("count");
        //writer.WriteValue(value.Count);

        foreach (var pair in value)
        {
            writer.WritePropertyName(pair.Key);
            writer.WriteValue(pair.Value);

        }
        writer.WriteEndObject();
    }
}
