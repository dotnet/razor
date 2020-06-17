// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal static class SerializerExtensions
    {
        public static void RegisterConverter(this Serializer serializer, Newtonsoft.Json.JsonConverter converter)
        {
            serializer.JsonSerializer.Converters.Add(converter);
            serializer.Settings.Converters.Add(converter);
        }
    }
}
