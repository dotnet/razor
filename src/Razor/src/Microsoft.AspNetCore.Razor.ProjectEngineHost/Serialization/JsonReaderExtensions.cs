// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal delegate void ReadPropertyValue<TData>(JsonReader reader, ref TData data);
internal delegate T ReadValue<T>(JsonReader reader);
internal delegate T ReadProperties<T>(JsonReader reader);
internal delegate void ProcessValue<T>(JsonReader reader, T arg);
internal delegate void ProcessProperties<T>(JsonReader reader, T arg);

internal sealed class PropertyMap<TData>
    where TData : struct
{
    private readonly Dictionary<string, ReadPropertyValue<TData>> _map;

    public PropertyMap(params (string, ReadPropertyValue<TData>)[] pairs)
    {
        var map = new Dictionary<string, ReadPropertyValue<TData>>(capacity: pairs.Length);

        foreach (var (key, value) in pairs)
        {
            map.Add(key, value);
        }

        _map = map;
    }

    public bool TryGetPropertyReader(string key, [MaybeNullWhen(false)] out ReadPropertyValue<TData> value)
        => _map.TryGetValue(key, out value);
}

internal static class JsonReaderExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CheckToken(this JsonReader reader, JsonToken expectedToken)
    {
        if (reader.TokenType != expectedToken)
        {
            ThrowUnexpectedTokenException(expectedToken, reader.TokenType);
        }

        [DoesNotReturn]
        static void ThrowUnexpectedTokenException(JsonToken expectedToken, JsonToken actualToken)
        {
            throw new InvalidOperationException(
                SR.FormatExpected_JSON_token_0_but_it_was_1(expectedToken, actualToken));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReadToken(this JsonReader reader, JsonToken expectedToken)
    {
        reader.CheckToken(expectedToken);
        reader.Read();
    }

    public static bool IsPropertyName(this JsonReader reader, string propertyName)
        => reader.TokenType == JsonToken.PropertyName &&
           (string?)reader.Value == propertyName;

    public static void ReadPropertyName(this JsonReader reader, string propertyName)
    {
        if (!reader.IsPropertyName(propertyName))
        {
            ThrowUnexpectedPropertyException(propertyName, (string?)reader.Value);
        }

        reader.Read();

        [DoesNotReturn]
        static void ThrowUnexpectedPropertyException(string expectedPropertyName, string? actualPropertyName)
        {
            throw new InvalidOperationException(
                SR.FormatExpected_JSON_property_0_but_it_was_1(expectedPropertyName, actualPropertyName));
        }
    }

    public static bool TryReadPropertyName(this JsonReader reader, string propertyName)
    {
        if (reader.IsPropertyName(propertyName))
        {
            reader.Read();
            return true;
        }

        return false;
    }

    public static bool TryReadNextPropertyName(this JsonReader reader, [NotNullWhen(true)] out string? propertyName)
    {
        if (reader.TokenType != JsonToken.PropertyName)
        {
            propertyName = null;
            return false;
        }

        propertyName = (string)reader.Value.AssumeNotNull();
        reader.Read();

        return true;
    }

    public static bool TryReadNull(this JsonReader reader)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            reader.Read();
            return true;
        }

        return false;
    }

    public static bool ReadBoolean(this JsonReader reader)
    {
        reader.CheckToken(JsonToken.Boolean);

        var result = Convert.ToBoolean(reader.Value);
        reader.Read();

        return result;
    }

    public static bool ReadBoolean(this JsonReader reader, string propertyName)
    {
        reader.ReadPropertyName(propertyName);

        return reader.ReadBoolean();
    }

    public static bool ReadBooleanOrDefault(this JsonReader reader, string propertyName, bool defaultValue = default)
        => reader.TryReadPropertyName(propertyName) ? reader.ReadBoolean() : defaultValue;

    public static bool ReadBooleanOrTrue(this JsonReader reader, string propertyName)
        => !reader.TryReadPropertyName(propertyName) || reader.ReadBoolean();

    public static bool ReadBooleanOrFalse(this JsonReader reader, string propertyName)
        => reader.TryReadPropertyName(propertyName) && reader.ReadBoolean();

    public static bool TryReadBoolean(this JsonReader reader, string propertyName, out bool value)
    {
        if (reader.TryReadPropertyName(propertyName))
        {
            value = reader.ReadBoolean();
            return true;
        }

        value = default;
        return false;
    }

    public static int ReadInt32(this JsonReader reader)
    {
        reader.CheckToken(JsonToken.Integer);

        var result = Convert.ToInt32(reader.Value);
        reader.Read();

        return result;
    }

    public static int ReadInt32OrDefault(this JsonReader reader, string propertyName, int defaultValue = default)
        => reader.TryReadPropertyName(propertyName) ? reader.ReadInt32() : defaultValue;

    public static int ReadInt32OrZero(this JsonReader reader, string propertyName)
        => reader.TryReadPropertyName(propertyName) ? reader.ReadInt32() : 0;

    public static bool TryReadInt32(this JsonReader reader, string propertyName, out int value)
    {
        if (reader.TryReadPropertyName(propertyName))
        {
            value = reader.ReadInt32();
            return true;
        }

        value = default;
        return false;
    }

    public static int ReadInt32(this JsonReader reader, string propertyName)
    {
        reader.ReadPropertyName(propertyName);

        return reader.ReadInt32();
    }

    public static string? ReadString(this JsonReader reader)
    {
        if (reader.TryReadNull())
        {
            return null;
        }

        reader.CheckToken(JsonToken.String);

        var result = Convert.ToString(reader.Value);
        reader.Read();

        return result;
    }

    public static string? ReadString(this JsonReader reader, string propertyName)
    {
        reader.ReadPropertyName(propertyName);

        return reader.ReadString();
    }

    public static string? ReadStringOrDefault(this JsonReader reader, string propertyName, string? defaultValue = default)
        => reader.TryReadPropertyName(propertyName) ? reader.ReadString() : defaultValue;

    public static string? ReadStringOrNull(this JsonReader reader, string propertyName)
        => reader.TryReadPropertyName(propertyName) ? reader.ReadString() : null;

    public static bool TryReadString(this JsonReader reader, string propertyName, out string? value)
    {
        if (reader.TryReadPropertyName(propertyName))
        {
            value = reader.ReadString();
            return true;
        }

        value = null;
        return false;
    }

    public static string ReadNonNullString(this JsonReader reader)
    {
        reader.CheckToken(JsonToken.String);

        var result = Convert.ToString(reader.Value).AssumeNotNull();
        reader.Read();

        return result;
    }

    public static string ReadNonNullString(this JsonReader reader, string propertyName)
    {
        reader.ReadPropertyName(propertyName);

        return reader.ReadNonNullString();
    }

    [return: MaybeNull]
    public static T ReadObject<T>(this JsonReader reader, ReadProperties<T> readProperties)
    {
        if (reader.TryReadNull())
        {
            return default;
        }

        return reader.ReadNonNullObject(readProperties);
    }

    [return: MaybeNull]
    public static T ReadObject<T>(this JsonReader reader, string propertyName, ReadProperties<T> readProperties)
    {
        reader.ReadPropertyName(propertyName);

        return reader.ReadObject(readProperties);
    }

    public static T ReadNonNullObject<T>(this JsonReader reader, ReadProperties<T> readProperties)
    {
        reader.ReadToken(JsonToken.StartObject);
        var result = readProperties(reader);
        reader.ReadToken(JsonToken.EndObject);

        return result;
    }

    public static T ReadNonNullObject<T>(this JsonReader reader, string propertyName, ReadProperties<T> readProperties)
    {
        reader.ReadPropertyName(propertyName);

        return readProperties(reader);
    }

    public static TData ReadObjectData<TData>(this JsonReader reader, PropertyMap<TData> propertyMap)
        where TData : struct
    {
        reader.ReadToken(JsonToken.StartObject);
        var result = reader.ReadProperties(propertyMap);
        reader.ReadToken(JsonToken.EndObject);

        return result;
    }

    public static void ReadObjectData<TData>(this JsonReader reader, ref TData data, PropertyMap<TData> propertyMap)
        where TData : struct
    {
        reader.ReadToken(JsonToken.StartObject);
        reader.ReadProperties(ref data, propertyMap);
        reader.ReadToken(JsonToken.EndObject);
    }

    public static TData ReadProperties<TData>(this JsonReader reader, PropertyMap<TData> propertyMap)
        where TData : struct
    {
        TData result = default;
        reader.ReadProperties(ref result, propertyMap);

        return result;
    }

    public static void ReadProperties<TData>(this JsonReader reader, ref TData data, PropertyMap<TData> propertyMap)
        where TData : struct
    {
        while (true)
        {
            switch (reader.TokenType)
            {
                case JsonToken.PropertyName:
                    var propertyName = (string)reader.Value.AssumeNotNull();

                    if (!propertyMap.TryGetPropertyReader(propertyName, out var readPropertyValue))
                    {
                        throw new InvalidOperationException(
                            SR.FormatEncountered_unexpected_JSON_property_0(propertyName));
                    }

                    reader.Read();
                    readPropertyValue(reader, ref data);

                    break;

                case JsonToken.EndObject:
                    return;

                case var token:
                    throw new InvalidOperationException(
                        SR.FormatEncountered_unexpected_JSON_token_0(token));
            }
        }
    }

    public static T[]? ReadArray<T>(this JsonReader reader, ReadValue<T> readElement)
    {
        if (reader.TryReadNull())
        {
            return null;
        }

        reader.ReadToken(JsonToken.StartArray);

        // First special case, is this an empty array?
        if (reader.TokenType == JsonToken.EndArray)
        {
            reader.Read();
            return Array.Empty<T>();
        }

        // Second special case, is this an array of one element?
        var firstElement = readElement(reader);

        if (reader.TokenType == JsonToken.EndArray)
        {
            reader.Read();
            return new[] { firstElement };
        }

        // There's more than one element, so we need to acquire a pooled list to
        // read the rest of the array elements.
        using var _ = ListPool<T>.GetPooledObject(out var elements);

        // Be sure to add the element that we already read.
        elements.Add(firstElement);

        do
        {
            var element = readElement(reader);
            elements.Add(element);
        }
        while (reader.TokenType != JsonToken.EndArray);

        reader.Read();

        return elements.ToArray();
    }

    public static T[]? ReadArray<T>(this JsonReader reader, string propertyName, ReadValue<T> readElement)
    {
        reader.ReadPropertyName(propertyName);
        return reader.ReadArray(readElement);
    }

    public static T[] ReadArrayOrEmpty<T>(this JsonReader reader, ReadValue<T> readElement)
        => reader.ReadArray(readElement) ?? Array.Empty<T>();

    public static T[] ReadArrayOrEmpty<T>(this JsonReader reader, string propertyName, ReadValue<T> readElement)
        => reader.ReadArray(propertyName, readElement) ?? Array.Empty<T>();

    public static void ProcessObject<T>(this JsonReader reader, T arg, ProcessProperties<T> processProperties)
    {
        if (reader.TryReadNull())
        {
            return;
        }

        reader.ReadToken(JsonToken.StartObject);

        while (reader.TokenType != JsonToken.EndObject)
        {
            processProperties(reader, arg);
        }

        reader.ReadToken(JsonToken.EndObject);
    }

    public static void ProcessObject<T>(this JsonReader reader, T arg, PropertyMap<T> propertyMap)
        where T : struct
    {
        reader.ReadToken(JsonToken.StartObject);
        reader.ProcessProperties(arg, propertyMap);
        reader.ReadToken(JsonToken.EndObject);
    }

    public static void ProcessProperties<T>(this JsonReader reader, T arg, PropertyMap<T> propertyMap)
        where T : struct
    {
        ref var localArg = ref arg;

        while (true)
        {
            switch (reader.TokenType)
            {
                case JsonToken.PropertyName:
                    var propertyName = (string)reader.Value.AssumeNotNull();

                    if (!propertyMap.TryGetPropertyReader(propertyName, out var readProperty))
                    {
                        throw new InvalidOperationException(
                            SR.FormatEncountered_unexpected_JSON_property_0(propertyName));
                    }

                    reader.Read();
                    readProperty(reader, ref localArg);

                    break;

                case JsonToken.EndObject:
                    return;

                case var token:
                    throw new InvalidOperationException(
                        SR.FormatEncountered_unexpected_JSON_token_0(token));
            }
        }
    }

    public static void ProcessArray<T>(this JsonReader reader, T arg, ProcessValue<T> processElement)
    {
        if (reader.TryReadNull())
        {
            return;
        }

        reader.ReadToken(JsonToken.StartArray);

        while (reader.TokenType != JsonToken.EndArray)
        {
            processElement(reader, arg);
        }

        reader.ReadToken(JsonToken.EndArray);
    }

    public static void ReadToEndOfCurrentObject(this JsonReader reader)
    {
        var nestingLevel = 0;

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonToken.StartObject:
                    nestingLevel++;
                    break;

                case JsonToken.EndObject:
                    nestingLevel--;

                    if (nestingLevel == -1)
                    {
                        return;
                    }

                    break;
            }
        }

        throw new JsonSerializationException(SR.Encountered_end_of_stream_before_end_of_object);
    }

#nullable disable

    public static TArg ReadProperties<TArg>(this JsonReader reader, Func<string, TArg, TArg> onProperty, TArg arg)
    {
        do
        {
            switch (reader.TokenType)
            {
                case JsonToken.PropertyName:
                    var propertyName = reader.Value.ToString();
                    arg = onProperty(propertyName, arg);
                    break;
                case JsonToken.EndObject:
                    return arg;
            }
        } while (reader.Read());

        return arg;
    }
}
