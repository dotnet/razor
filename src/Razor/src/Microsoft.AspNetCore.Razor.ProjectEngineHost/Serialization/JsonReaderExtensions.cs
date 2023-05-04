// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal delegate void ReadPropertyValue<TData>(JsonReader reader, ref TData data);

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

    public static int ReadInt32(this JsonReader reader)
    {
        reader.CheckToken(JsonToken.Integer);

        var result = Convert.ToInt32(reader.Value);
        reader.Read();

        return result;
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

                    if (!propertyMap.TryGetPropertyReader(propertyName, out var readProperty))
                    {
                        throw new InvalidOperationException(
                            SR.FormatEncountered_unexpected_JSON_property_0(propertyName));
                    }

                    reader.Read();
                    readProperty(reader, ref data);

                    break;

                case JsonToken.EndObject:
                    return;

                case var token:
                    throw new InvalidOperationException(
                        SR.FormatEncountered_unexpected_JSON_token_0(token));
            }
        }
    }

#nullable disable

    public static bool ReadTokenAndAdvance(this JsonReader reader, JsonToken expectedTokenType, out object value)
    {
        value = reader.Value;
        return reader.TokenType == expectedTokenType && reader.Read();
    }

    public static void ReadProperties<TArg>(this JsonReader reader, Action<string, TArg> onProperty, TArg arg)
    {
        do
        {
            switch (reader.TokenType)
            {
                case JsonToken.PropertyName:
                    var propertyName = reader.Value.ToString();
                    onProperty(propertyName, arg);
                    break;
                case JsonToken.EndObject:
                    return;
            }
        } while (reader.Read());
    }

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

    public static bool TryReadNextProperty<TReturn>(this JsonReader reader, string propertyName, out TReturn value)
    {
        do
        {
            switch (reader.TokenType)
            {
                case JsonToken.PropertyName:
                    // Ensures we're at the expected property & the reader
                    // can read the property value.
                    if (reader.Value.ToString() == propertyName &&
                        reader.Read())
                    {
                        value = (TReturn)reader.Value;
                        return true;
                    }
                    else
                    {
                        value = default;
                        return false;
                    }
            }
        } while (reader.Read());

        throw new JsonSerializationException($"Could not find string property '{propertyName}'.");
    }
}
