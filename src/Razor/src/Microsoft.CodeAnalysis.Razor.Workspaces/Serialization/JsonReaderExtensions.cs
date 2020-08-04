// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace Microsoft.CodeAnalysis.Razor.Serialization
{
    internal static class JsonReaderExtensions
    {
        public static bool ReadTokenAndAdvance(this JsonReader reader, JsonToken expectedTokenType, out object value)
        {
            value = reader.Value;
            return reader.TokenType == expectedTokenType && reader.Read();
        }

        public static void ReadProperties(this JsonReader reader, Action<string> onProperty)
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        var propertyName = reader.Value.ToString();
                        onProperty(propertyName);
                        break;
                    case JsonToken.EndObject:
                        return;
                }
            }
        }

        public static string ReadNextStringProperty(this JsonReader reader, string propertyName)
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        Debug.Assert(reader.Value.ToString() == propertyName);
                        if (reader.Read())
                        {
                            var value = (string)reader.Value;
                            return value;
                        }
                        else
                        {
                            return null;
                        }
                }
            }

            throw new JsonSerializationException($"Could not find string property '{propertyName}'.");
        }

        public static (int? hash, string propertyValue) ReadPropertyAndHash(this JsonReader reader, string propertyName)
        {
            int? hash = null;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        var curProperty = reader.Value.ToString();
                        if (reader.Read())
                        {
                            if (curProperty == RazorSerializationConstants.HashCodePropertyName)
                            {
                                hash = Convert.ToInt32(reader.Value);
                            }
                            else if (curProperty == propertyName)
                            {
                                var value = (string)reader.Value;
                                return (hash, value);
                            }
                            else
                            {
                                throw new JsonSerializationException($"Encountered unknown property when looking for hash or '{propertyName}'.");
                            }
                        }
                        else
                        {
                            return default;
                        }
                        break;
                }
            }

            throw new JsonSerializationException($"Could not find string property '{propertyName}' with hash.");
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

            throw new JsonSerializationException($"Could not read till end of object, end of stream. Got '{reader.TokenType}'.");
        }
    }
}
