using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

internal sealed class RazorCodeGenerationOptionsConverter : JsonConverter<RazorCodeGenerationOptions>
{
    private const string ValuePropertyName = "_";

    public override RazorCodeGenerationOptions? ReadJson(JsonReader reader, Type objectType, RazorCodeGenerationOptions? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.StartObject)
        {
            return null;
        }

        var designTime = reader.ReadPropertyName(nameof(RazorCodeGenerationOptions.DesignTime)).ReadAsBoolean().GetValueOrDefault();
        reader.ReadPropertyName(ValuePropertyName).Read();
        var result = designTime ? RazorCodeGenerationOptions.CreateDesignTime(factory) : RazorCodeGenerationOptions.Create(factory);
        reader.AssertTokenAndAdvance(JsonToken.EndObject);
        return result;

        void factory(RazorCodeGenerationOptionsBuilder builder)
        {
            serializer.Populate(reader, builder);
        }
    }

    public override void WriteJson(JsonWriter writer, RazorCodeGenerationOptions? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName(nameof(RazorCodeGenerationOptions.DesignTime));
        writer.WriteValue(value.DesignTime);
        writer.WritePropertyName(ValuePropertyName);

        var jObject = JObject.FromObject(value);
        jObject.Remove(nameof(RazorCodeGenerationOptions.DesignTime));
        jObject.WriteTo(writer);

        writer.WriteEndObject();
    }
}
