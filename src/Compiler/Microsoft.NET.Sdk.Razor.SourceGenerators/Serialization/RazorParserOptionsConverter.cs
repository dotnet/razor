using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.Serialization;
using Newtonsoft.Json;
using System;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

internal sealed class RazorParserOptionsConverter : JsonConverter<RazorParserOptions>
{
    private const string ValuePropertyName = "_";

    public override RazorParserOptions? ReadJson(JsonReader reader, Type objectType, RazorParserOptions? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.StartObject)
        {
            return null;
        }

        var designTime = reader.ReadPropertyName(nameof(RazorParserOptions.DesignTime)).ReadAsBoolean().GetValueOrDefault();
        var fileKind = reader.ReadPropertyName(nameof(RazorParserOptions.FileKind)).ReadAsString();
        reader.ReadPropertyName(ValuePropertyName).Read();
        var result = designTime ? RazorParserOptions.CreateDesignTime(factory, fileKind) : RazorParserOptions.Create(factory, fileKind);
        reader.AssertTokenAndAdvance(JsonToken.EndObject);
        return result;

        void factory(RazorParserOptionsBuilder builder)
        {
            serializer.Populate(reader, builder);
        }
    }

    public override void WriteJson(JsonWriter writer, RazorParserOptions? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName(nameof(RazorParserOptions.DesignTime));
        writer.WriteValue(value.DesignTime);
        writer.WritePropertyName(nameof(RazorParserOptions.FileKind));
        writer.WriteValue(value.FileKind);
        writer.WritePropertyName(ValuePropertyName);
        writer.WriteStartObject();
        writer.WritePropertyName(nameof(RazorParserOptions.ParseLeadingDirectives));
        writer.WriteValue(value.ParseLeadingDirectives);
        writer.WritePropertyName(nameof(RazorParserOptions.Version));
        writer.WriteValue(value.Version.ToString());
        writer.WritePropertyName(nameof(RazorParserOptions.Directives));
        writer.WriteStartArray();

        foreach (var directive in value.Directives)
        {
            serializer.Serialize(writer, directive);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
