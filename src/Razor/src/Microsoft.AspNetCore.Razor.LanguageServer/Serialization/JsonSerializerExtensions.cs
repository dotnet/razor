// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Serialization;

internal static class JsonSerializerExtensions
{
    public static void RegisterRazorConverters(this JsonSerializer serializer)
    {
        if (serializer is null)
        {
            throw new ArgumentNullException(nameof(serializer));
        }

        // In all of the below we add our converters to both the serializer settings and the actual
        // JsonSerializer. The reasoning behind this choice is that OmniSharp framework is not consistent
        // in using one over the other so we want to protect ourselves.

        serializer.Converters.RegisterRazorConverters();

        AddConverter(serializer, PlatformAgnosticClientCapabilities.JsonConverter);
    }

    public static void RegisterVSInternalExtensionConverters(this JsonSerializer serializer)
    {
        if (serializer is null)
        {
            throw new ArgumentNullException(nameof(serializer));
        }

        // In all of the below we add our converters to both the serializer settings and the actual
        // JsonSerializer. The reasoning behind this choice is that OmniSharp framework is not consistent
        // in using one over the other so we want to protect ourselves.

        // We create a temporary serializer because the VS API's only have extension methods for adding converters to the top-level serializer type; therefore,for adding converters to the top-level serializer type; therefore,
        // we effectively create a bag that the VS APIs can add to and then extract the added converters to add to the LSP serializer.

        serializer.AddVSInternalExtensionConverters();
    }

    private static void AddConverter(JsonSerializer serializer, JsonConverter converter)
    {
        serializer.Converters.Add(converter);
    }
}
