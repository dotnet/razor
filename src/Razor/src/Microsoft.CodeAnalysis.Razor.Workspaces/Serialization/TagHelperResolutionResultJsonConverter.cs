// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;

namespace Microsoft.CodeAnalysis.Razor.Serialization
{
    internal class TagHelperResolutionResultJsonConverter : JsonConverter
    {
        public static readonly TagHelperResolutionResultJsonConverter Instance = new TagHelperResolutionResultJsonConverter();

        public override bool CanConvert(Type objectType)
        {
            return typeof(TagHelperResolutionResult).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType != JsonToken.StartObject || !reader.Read())
            {
                return null;
            }

            var descriptors = new List<TagHelperDescriptor>();
            var descriptorConverter = TagHelperDescriptorJsonConverter.Instance;

            if (reader.TokenType != JsonToken.PropertyName ||
                reader.Value.ToString() != nameof(TagHelperResolutionResult.Descriptors) ||
                !reader.Read() ||
                reader.TokenType != JsonToken.StartArray ||
                !reader.Read())
            {
                return null;
            }

            do
            {
                var descriptor = descriptorConverter.ReadJson(reader, typeof(TagHelperDescriptor), existingValue: null, serializer) as TagHelperDescriptor;
                descriptors.Add(descriptor);

                if (reader.TokenType == JsonToken.EndObject)
                {
                    reader.Read();
                }
            } while (reader.TokenType != JsonToken.EndArray);

            return new TagHelperResolutionResult(descriptors, Array.Empty<RazorDiagnostic>());
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var result = (TagHelperResolutionResult)value;
            var descriptorConverter = TagHelperDescriptorJsonConverter.Instance;

            writer.WriteStartObject();

            writer.WritePropertyName(nameof(TagHelperResolutionResult.Descriptors));
            writer.WriteStartArray();
            foreach (var descriptor in result.Descriptors)
            {
                descriptorConverter.WriteJson(writer, descriptor, serializer);
            }
            writer.WriteEndArray();

            // NOTE: This doesn't currently support razor diagnostics

            writer.WriteEndObject();
        }
    }
}
