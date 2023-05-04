// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal abstract class ObjectJsonConverter<T> : JsonConverter<T>
    where T : class
{
    protected abstract T? ReadFromProperties(JsonReader reader);
    protected abstract void WriteProperties(JsonWriter writer, T value);

    public sealed override T? ReadJson(JsonReader reader, Type objectType, T? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        reader.ReadToken(JsonToken.StartObject);

        var result = ReadFromProperties(reader);

        // JSON.NET serialization expects that we don't advance passed the end object token,
        // but we should verify that it's there.
        reader.CheckToken(JsonToken.EndObject);

        return result;
    }

    public sealed override void WriteJson(JsonWriter writer, T? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        WriteProperties(writer, value);
        writer.WriteEndObject();
    }
}
