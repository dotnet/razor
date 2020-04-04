// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.Razor.Workspaces.Serialization;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Common.Serialization
{
    internal static class JsonConverterCollectionExtensions
    {
        public static readonly IReadOnlyList<JsonConverter> RazorConverters = new List<JsonConverter>()
        {
            TagHelperDescriptorJsonConverter.Instance,
            RazorDiagnosticJsonConverter.Instance,
            RazorExtensionJsonConverter.Instance,
            RazorConfigurationJsonConverter.Instance,
            FullProjectSnapshotHandleJsonConverter.Instance,
            ProjectSnapshotJsonConverter.Instance,
        };

        public static void RegisterRazorConverters(this JsonConverterCollection collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            for (var i = 0; i < RazorConverters.Count; i++)
            {
                collection.Add(RazorConverters[i]);
            }

            DelegatingSupportsConverter.AddDelegatingSupportsConverter(collection);
        }
    }

    internal class DelegatingSupportsConverter : JsonConverter
    {
        private JsonConverter Converter { get;}

        public static void AddDelegatingSupportsConverter(JsonConverterCollection collection)
        {
            Type supportsConverterType = typeof(PublishDiagnosticsCapability).Assembly.GetType("OmniSharp.Extensions.LanguageServer.Protocol.Serialization.Converters.SupportsConverter");
            int supportsConverterIndex = collection.ToList().FindIndex(converter =>
            {
                return converter.GetType() == supportsConverterType;
            });

            if (supportsConverterIndex >= 0)
            {
                collection[supportsConverterIndex] = new DelegatingSupportsConverter(collection[supportsConverterIndex]);
            }
        }

        private DelegatingSupportsConverter(JsonConverter converter)
        {
            Converter = converter;
        }

        public override bool CanConvert(Type objectType)
        {
            return Converter.CanConvert(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Type targetType = objectType.GetTypeInfo().GetGenericArguments()[0];

            if (targetType != typeof(PublishDiagnosticsCapability))
            {
                return Converter.ReadJson(reader, objectType, existingValue, serializer);
            }

            VsPublishDiagnosticsCapability deserializedValue = serializer.Deserialize<VsPublishDiagnosticsCapability>(reader);
            PublishDiagnosticsCapability target = new PublishDiagnosticsCapability()
            {
                // Not sure if this is correct, but this is fine for us as we don't use this capability.
                RelatedInformation = deserializedValue.TagSupport
            };

            return Supports.OfValue(target);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Converter.WriteJson(writer, value, serializer);
        }

        // Used to represent the json that VS sends over.
        private class VsPublishDiagnosticsCapability
        {
            public bool TagSupport { get; set; }
        }
    }
}
