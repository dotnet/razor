// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal static class ObjectWriters
{
    public static void Write(JsonWriter writer, RazorExtension value)
        => writer.WriteObject(value, WriteProperties);

    public static void WriteProperties(JsonWriter writer, RazorExtension value)
    {
        writer.Write(nameof(value.ExtensionName), value.ExtensionName);
    }

    public static void Write(JsonWriter writer, RazorConfiguration? value)
        => writer.WriteObject(value, WriteProperties);

    public static void WriteProperties(JsonWriter writer, RazorConfiguration value)
    {
        writer.Write(nameof(value.ConfigurationName), value.ConfigurationName);

        if (value.LanguageVersion == RazorLanguageVersion.Experimental)
        {
            writer.Write(nameof(value.LanguageVersion), "Experimental");
        }
        else
        {
            writer.Write(nameof(value.LanguageVersion), value.LanguageVersion.ToString());
        }

        writer.WriteArray(nameof(value.Extensions), value.Extensions, Write);
    }
}
