using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

internal sealed class DirectiveTokenDescriptorConverter : JsonConverter<DirectiveTokenDescriptor>
{
    public override DirectiveTokenDescriptor? ReadJson(JsonReader reader, Type objectType, DirectiveTokenDescriptor? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);

        if (obj == null || !Enum.TryParse<DirectiveTokenKind>(obj[nameof(DirectiveTokenDescriptor.Kind)]!.Value<string>(), out var kind))
        {
            return null;
        }

        return DirectiveTokenDescriptor.CreateToken(
            kind: kind,
            optional: obj[nameof(DirectiveTokenDescriptor.Optional)]!.Value<bool>(),
            name: obj[nameof(DirectiveTokenDescriptor.Name)]!.Value<string>(),
            description: obj[nameof(DirectiveTokenDescriptor.Description)]!.Value<string>());
    }

    public override void WriteJson(JsonWriter writer, DirectiveTokenDescriptor? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        JObject.FromObject(value).WriteTo(writer);
    }
}
