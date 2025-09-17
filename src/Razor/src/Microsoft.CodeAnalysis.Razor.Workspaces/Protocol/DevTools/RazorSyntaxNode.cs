// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.CodeAnalysis.Razor.Protocol.DevTools;

internal sealed class SyntaxVisualizerNode
{
    [JsonPropertyName("kind")]
    public required string Kind { get; set; }

    [JsonPropertyName("spanStart")]
    public required int SpanStart { get; set; }

    [JsonPropertyName("spanEnd")]
    public required int SpanEnd { get; set; }

    [JsonPropertyName("children")]
    public required SyntaxVisualizerNode[] Children { get; set; }
}