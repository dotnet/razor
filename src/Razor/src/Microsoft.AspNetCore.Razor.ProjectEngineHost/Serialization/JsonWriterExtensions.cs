// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal delegate void WriteProperties<T>(JsonWriter writer, T value);

internal static class JsonWriterExtensions
{
    public static void Write(this JsonWriter writer, string propertyName, bool value)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteValue(value);
    }

    public static void Write(this JsonWriter writer, string propertyName, int value)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteValue(value);
    }

    public static void Write(this JsonWriter writer, string propertyName, string? value)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteValue(value);
    }

    public static void WriteObject<T>(this JsonWriter writer, string propertyName, T? value, WriteProperties<T> writeProperties)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteObject(value, writeProperties);
    }

    public static void WriteObject<T>(this JsonWriter writer, T? value, WriteProperties<T> writeProperties)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();
        writeProperties(writer, value);
        writer.WriteEndObject();
    }

#nullable disable

    public static void WritePropertyArray<T>(this JsonWriter writer, string propertyName, IEnumerable<T> collection, JsonSerializer serializer)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (var item in collection)
        {
            serializer.Serialize(writer, item);
        }

        writer.WriteEndArray();
    }
}
