// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal class RazorExtensionJsonConverter : ObjectJsonConverter<RazorExtension>
{
    public static readonly RazorExtensionJsonConverter Instance = new();

    private RazorExtensionJsonConverter()
    {
    }

    protected override RazorExtension ReadFromProperties(JsonReader reader)
        => ObjectReaders.ReadExtensionFromProperties(reader);

    protected override void WriteProperties(JsonWriter writer, RazorExtension value)
        => ObjectWriters.WriteProperties(writer, value);
}
