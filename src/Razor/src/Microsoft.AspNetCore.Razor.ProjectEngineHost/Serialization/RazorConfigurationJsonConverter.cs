// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal class RazorConfigurationJsonConverter : ObjectJsonConverter<RazorConfiguration>
{
    public static readonly RazorConfigurationJsonConverter Instance = new();

    public RazorConfigurationJsonConverter()
    {
    }

    protected override RazorConfiguration ReadFromProperties(JsonReader reader)
        => ObjectReaders.ReadConfigurationFromProperties(reader);

    protected override void WriteProperties(JsonWriter writer, RazorConfiguration value)
        => ObjectWriters.WriteProperties(writer, value);
}
