// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace Microsoft.CodeAnalysis.Razor.Protocol;

/// <summary>
/// A representation of a Roslyn TextChange that can be serialized with System.Text.Json.
/// </summary>
internal sealed class RazorTextChange
{
    [JsonPropertyName("start")]
    public int Start { get; set; }

    [JsonPropertyName("length")]
    public int Length { get; set; }

    [JsonPropertyName("newText")]
    public string? NewText { get; set; }
}
