// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.CodeAnalysis.Razor.MapCode;

internal sealed record CSharpFocusLocationsAndNodes(
    [property: JsonPropertyName("focusLocations")]
    LspLocation[][] FocusLocations,
    [property: JsonPropertyName("csharpNodeBodies")]
    string[] CSharpNodeBodies);
