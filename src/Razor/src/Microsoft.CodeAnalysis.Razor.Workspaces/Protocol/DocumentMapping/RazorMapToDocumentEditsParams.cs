// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace Microsoft.CodeAnalysis.Razor.Protocol.DocumentMapping;

internal sealed record class RazorMapToDocumentEditsParams
{
    [JsonPropertyName("kind")]
    public RazorLanguageKind Kind { get; init; }

    [JsonPropertyName("razorDocumentUri")]
    [JsonConverter(typeof(DocumentUriConverter))]
    public required DocumentUri RazorDocumentUri { get; init; }

    [JsonPropertyName("textChanges")]
    public required RazorTextChange[] TextChanges { get; init; }
}
