// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor.Serialization;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Serialization
{
    internal class CodeActionDataJsonConverter : JsonConverter
    {
        public static readonly CodeActionDataJsonConverter Instance = new CodeActionDataJsonConverter();

        public override bool CanConvert(Type objectType)
        {
            return false;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.StartObject)
            {
                return null;
            }

            var extensionName = string.Empty;

            reader.ReadProperties(propertyName =>
            {
                switch (propertyName)
                {
                    case "CustomTags":
                        if (reader.Read())
                        {
                            extensionName = (string)reader.Value;
                        }
                        break;
                }
            });

            return new SerializedRazorExtension(extensionName);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
