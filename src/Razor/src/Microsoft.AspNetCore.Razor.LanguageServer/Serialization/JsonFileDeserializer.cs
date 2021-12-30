﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.IO;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Serialization
{
    internal abstract class JsonFileDeserializer
    {
        public static readonly JsonFileDeserializer Instance = new DefaultJsonFileDeserializer();

        public abstract TValue Deserialize<TValue>(string filePath) where TValue : class;

        private class DefaultJsonFileDeserializer : JsonFileDeserializer
        {
            private readonly LspSerializer _serializer;

            public DefaultJsonFileDeserializer()
            {
                _serializer = new LspSerializer();
                _serializer.RegisterRazorConverters();
            }

            public override TValue Deserialize<TValue>(string filePath) where TValue : class
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                try
                {
                    var deserializedValue = (TValue)_serializer.JsonSerializer.Deserialize(reader, typeof(TValue));
                    return deserializedValue;
                }
                catch
                {
                    // Swallow deserialization exceptions. There's many reasons they can happen, all out of our control.
                    return null;
                }
            }
        }
    }
}
