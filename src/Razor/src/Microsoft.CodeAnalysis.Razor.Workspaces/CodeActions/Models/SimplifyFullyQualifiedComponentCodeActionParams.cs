// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.CodeAnalysis.Razor.CodeActions.Models;

internal sealed class SimplifyFullyQualifiedComponentCodeActionParams
{
    [JsonPropertyName("fullyQualifiedName")]
    public required string FullyQualifiedName { get; set; }

    [JsonPropertyName("@namespace")]
    public required string Namespace { get; set; }
}
