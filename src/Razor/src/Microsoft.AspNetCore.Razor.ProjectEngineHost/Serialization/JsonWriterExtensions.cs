// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal delegate void WriteProperties<T>(JsonWriter writer, T value);
internal delegate void WriteValue<T>(JsonWriter writer, T value);

internal static class JsonWriterExtensions
{
    public static void Write(this JsonWriter writer, string propertyName, bool value)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteValue(value);
    }

    public static void WriteIfNotTrue(this JsonWriter writer, string propertyName, bool value)
    {
        if (value)
        {
            writer.Write(propertyName, value);
        }
    }

    public static void WriteIfNotFalse(this JsonWriter writer, string propertyName, bool value)
    {
        if (!value)
        {
            writer.Write(propertyName, value);
        }
    }

    public static void Write(this JsonWriter writer, string propertyName, int value)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteValue(value);
    }

    public static void WriteIfNotDefault(this JsonWriter writer, string propertyName, int value, int defaultValue = default)
    {
        if (value != defaultValue)
        {
            writer.Write(propertyName, value);
        }
    }

    public static void Write(this JsonWriter writer, string propertyName, string? value)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteValue(value);
    }

    public static void WriteIfNotDefault(this JsonWriter writer, string propertyName, string? value, string? defaultValue)
    {
        if (value != defaultValue)
        {
            writer.Write(propertyName, value);
        }
    }

    public static void WriteIfNotNull(this JsonWriter writer, string propertyName, string? value)
    {
        if (value is not null)
        {
            writer.Write(propertyName, value);
        }
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

    public static void WriteArray<T>(this JsonWriter writer, IEnumerable<T>? elements, WriteValue<T> writeElement)
    {
        if (writeElement is null)
        {
            throw new ArgumentNullException(nameof(writeElement));
        }

        if (elements is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartArray();

        foreach (var element in elements)
        {
            writeElement(writer, element);
        }

        writer.WriteEndArray();
    }

    public static void WriteArray<T>(this JsonWriter writer, string propertyName, IEnumerable<T>? elements, WriteValue<T> writeElement)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteArray(elements, writeElement);
    }

    public static void WriteArray<T>(this JsonWriter writer, IReadOnlyList<T>? elements, WriteValue<T> writeElement)
    {
        if (writeElement is null)
        {
            throw new ArgumentNullException(nameof(writeElement));
        }

        if (elements is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartArray();

        var count = elements.Count;

        for (var i = 0; i < count; i++)
        {
            writeElement(writer, elements[i]);
        }

        writer.WriteEndArray();
    }

    public static void WriteArray<T>(this JsonWriter writer, string propertyName, IReadOnlyList<T>? elements, WriteValue<T> writeElement)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteArray(elements, writeElement);
    }

    public static void WriteArray<T>(this JsonWriter writer, ImmutableArray<T> elements, WriteValue<T> writeElement)
    {
        if (writeElement is null)
        {
            throw new ArgumentNullException(nameof(writeElement));
        }

        writer.WriteStartArray();

        foreach (var element in elements)
        {
            writeElement(writer, element);
        }

        writer.WriteEndArray();
    }

    public static void WriteArray<T>(this JsonWriter writer, string propertyName, ImmutableArray<T> elements, WriteValue<T> writeElement)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteArray(elements, writeElement);
    }

    public static void WriteArrayIfNotNullOrEmpty<T>(this JsonWriter writer, string propertyName, IEnumerable<T>? elements, WriteValue<T> writeElement)
    {
        if (elements?.Any() == true)
        {
            writer.WriteArray(propertyName, elements, writeElement);
        }
    }

    public static void WriteArrayIfNotNullOrEmpty<T>(this JsonWriter writer, string propertyName, IReadOnlyList<T>? elements, WriteValue<T> writeElement)
    {
        if (elements is { Count: > 0 })
        {
            writer.WriteArray(propertyName, elements, writeElement);
        }
    }

    public static void WriteArrayIfNotDefaultOrEmpty<T>(this JsonWriter writer, string propertyName, ImmutableArray<T> elements, WriteValue<T> writeElement)
    {
        if (!elements.IsDefaultOrEmpty)
        {
            writer.WriteArray(propertyName, elements, writeElement);
        }
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
