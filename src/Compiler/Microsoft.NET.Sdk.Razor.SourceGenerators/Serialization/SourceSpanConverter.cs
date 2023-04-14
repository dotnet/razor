using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

internal sealed class SourceSpanConverter : JsonConverter<SourceSpan?>
{
    public override SourceSpan? ReadJson(JsonReader reader, Type objectType, SourceSpan? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var span = JObject.Load(reader);

        if (span == null)
        {
            return null;
        }

        return new SourceSpan(
            filePath: span[nameof(SourceSpan.FilePath)]!.Value<string>(),
            absoluteIndex: span[nameof(SourceSpan.AbsoluteIndex)]!.Value<int>(),
            lineIndex: span[nameof(SourceSpan.LineIndex)]!.Value<int>(),
            characterIndex: span[nameof(SourceSpan.CharacterIndex)]!.Value<int>(),
            length: span[nameof(SourceSpan.Length)]!.Value<int>(),
            lineCount: span[nameof(SourceSpan.LineCount)]!.Value<int>(),
            endCharacterIndex: span[nameof(SourceSpan.EndCharacterIndex)]!.Value<int>());
    }

    public override void WriteJson(JsonWriter writer, SourceSpan? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName(nameof(SourceSpan.FilePath));
        writer.WriteValue(value.Value.FilePath);
        writer.WritePropertyName(nameof(SourceSpan.AbsoluteIndex));
        writer.WriteValue(value.Value.AbsoluteIndex);
        writer.WritePropertyName(nameof(SourceSpan.LineIndex));
        writer.WriteValue(value.Value.LineIndex);
        writer.WritePropertyName(nameof(SourceSpan.CharacterIndex));
        writer.WriteValue(value.Value.CharacterIndex);
        writer.WritePropertyName(nameof(SourceSpan.Length));
        writer.WriteValue(value.Value.Length);
        writer.WritePropertyName(nameof(SourceSpan.LineCount));
        writer.WriteValue(value.Value.LineCount);
        writer.WritePropertyName(nameof(SourceSpan.EndCharacterIndex));
        writer.WriteValue(value.Value.EndCharacterIndex);
        writer.WriteEndObject();
    }
}
