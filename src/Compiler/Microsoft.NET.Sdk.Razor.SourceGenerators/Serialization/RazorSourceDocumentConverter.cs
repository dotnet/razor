using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.Serialization;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Text;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

internal sealed class RazorSourceDocumentConverter : JsonConverter<RazorSourceDocument>
{
    private const string ContentPropertyName = "Content";

    public override RazorSourceDocument? ReadJson(JsonReader reader, Type objectType, RazorSourceDocument? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType != JsonToken.StartObject)
        {
            return null;
        }

        Encoding? encoding = null;
        string? filePath = null;
        string? relativePath = null;
        string? content = null;
        reader.ReadProperties(propertyName =>
        {
            switch (propertyName)
            {
                case nameof(RazorSourceDocument.Encoding):
                    encoding = serializer.Deserialize<Encoding>(reader);
                    break;
                case nameof(RazorSourceDocument.FilePath):
                    filePath = reader.ReadAsString();
                    break;
                case nameof(RazorSourceDocument.RelativePath):
                    relativePath = reader.ReadAsString();
                    break;
                case nameof(ContentPropertyName):
                    content = reader.ReadAsString();
                    break;
            }
        });
        return RazorSourceDocument.Create(content, encoding, new RazorSourceDocumentProperties(filePath, relativePath));
    }

    public override void WriteJson(JsonWriter writer, RazorSourceDocument? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName(nameof(RazorSourceDocument.Encoding));
        serializer.Serialize(writer, value.Encoding);
        writer.WritePropertyName(nameof(RazorSourceDocument.FilePath));
        writer.WriteValue(value.FilePath);
        writer.WritePropertyName(nameof(RazorSourceDocument.RelativePath));
        writer.WriteValue(value.RelativePath);
        writer.WritePropertyName(ContentPropertyName);

        var content = ArrayPool<char>.Shared.Rent(value.Length);
        value.CopyTo(0, content, 0, value.Length);

        using (StringBuilderPool.GetPooledObject(out var stringBuilder))
        {
            stringBuilder.EnsureCapacity(value.Length);
            stringBuilder.Append(content);
            writer.WriteValue(stringBuilder.ToString());
        }

        ArrayPool<char>.Shared.Return(content);

        writer.WriteEndObject();
    }
}
