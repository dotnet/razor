using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.Serialization;
using Newtonsoft.Json;
using System;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

internal sealed class DirectiveDescriptorConverter : JsonConverter<DirectiveDescriptor>
{
    private const string ValuePropertyName = "_";

    public override DirectiveDescriptor? ReadJson(JsonReader reader, Type objectType, DirectiveDescriptor? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.StartObject)
        {
            return null;
        }

        var directive = reader.ReadPropertyName(nameof(DirectiveDescriptor.Directive)).ReadAsString();
        if (!Enum.TryParse<DirectiveKind>(reader.ReadPropertyName(nameof(DirectiveDescriptor.Kind)).ReadAsString(), out var kind))
        {
            return null;
        }

        reader.ReadPropertyName(ValuePropertyName).Read();
        var result = DirectiveDescriptor.CreateDirective(directive, kind, builder => serializer.Populate(reader, builder));
        reader.AssertTokenAndAdvance(JsonToken.EndObject);
        return result;
    }

    public override void WriteJson(JsonWriter writer, DirectiveDescriptor? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName(nameof(DirectiveDescriptor.Directive));
        writer.WriteValue(value.Directive);
        writer.WritePropertyName(nameof(DirectiveDescriptor.Kind));
        writer.WriteValue(value.Kind.ToString());
        writer.WritePropertyName(ValuePropertyName);
        writer.WriteStartObject();
        writer.WritePropertyName(nameof(DirectiveDescriptor.Description));
        writer.WriteValue(value.Description);
        writer.WritePropertyName(nameof(DirectiveDescriptor.DisplayName));
        writer.WriteValue(value.DisplayName);
        writer.WritePropertyName(nameof(DirectiveDescriptor.Usage));
        writer.WriteValue(value.Usage.ToString());
        writer.WritePropertyName(nameof(DirectiveDescriptor.Tokens));
        writer.WriteStartArray();

        foreach (var token in value.Tokens)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(nameof(DirectiveTokenDescriptor.Kind));
            writer.WriteValue(token.Kind.ToString());
            writer.WritePropertyName(nameof(DirectiveTokenDescriptor.Optional));
            writer.WriteValue(token.Optional);
            writer.WritePropertyName(nameof(DirectiveTokenDescriptor.Name));
            writer.WriteValue(token.Name);
            writer.WritePropertyName(nameof(DirectiveTokenDescriptor.Description));
            writer.WriteValue(token.Description);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
