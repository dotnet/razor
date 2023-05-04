// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal class RazorDiagnosticJsonConverter : ObjectJsonConverter<RazorDiagnostic>
{
    public static readonly RazorDiagnosticJsonConverter Instance = new();

    private RazorDiagnosticJsonConverter()
    {
    }

    protected override RazorDiagnostic ReadFromProperties(JsonReader reader)
        => ObjectReaders.ReadDiagnosticFromProperties(reader);

    protected override void WriteProperties(JsonWriter writer, RazorDiagnostic value)
        => ObjectWriters.WriteProperties(writer, value);
}
