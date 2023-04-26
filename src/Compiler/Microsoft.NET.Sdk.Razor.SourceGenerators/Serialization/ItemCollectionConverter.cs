using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;
using System;
using System.Diagnostics;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

internal sealed class ItemCollectionConverter : JsonConverter<ItemCollection>
{
    public override ItemCollection? ReadJson(JsonReader reader, Type objectType, ItemCollection? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.StartObject)
        {
            return null;
        }

        var result = existingValue ?? new ItemCollection();
        while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
        {
            var propertyName = (string)reader.Value!;
            var value = reader.ReadAsString();
            result.Add(propertyName, value);
        }

        Debug.Assert(reader.TokenType == JsonToken.EndObject);
        return result;
    }

    public override void WriteJson(JsonWriter writer, ItemCollection? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();

        foreach (var pair in value)
        {
            if (pair.Key is string k)
            {
                writer.WritePropertyName(k);
            }
            else
            {
                Debug.Assert(false, $"Cannot serialize non-string annotation {pair}");
            }

            switch (pair.Value)
            {
                case string s:
                    writer.WriteValue(s);
                    break;

                case SourceSpan span:
                    SourceSpanConverter.Instance.WriteJson(writer, span, serializer);
                    break;

                default:
					Debug.Assert(false, $"Cannot serialize non-string annotation {pair}");
                    break;
			}
		}

        writer.WriteEndObject();
    }
}
