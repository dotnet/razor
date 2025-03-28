// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.CodeAnalysis.Razor.Protocol.DocumentMapping;

internal sealed record class RazorMapDynamicSpansParams
{
    [JsonPropertyName("csharpDocumentId")]
    public required Uri CSharpDocumentId { get; init; }

    [JsonPropertyName("textChanges")]
    public required RazorTextChange[] TextChanges { get; init; }
}
