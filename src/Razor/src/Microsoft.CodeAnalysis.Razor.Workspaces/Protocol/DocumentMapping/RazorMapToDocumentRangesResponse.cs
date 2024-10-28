// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Protocol.DocumentMapping;

internal class RazorMapToDocumentRangesResponse
{
    [JsonPropertyName("ranges")]
    public required Range[] Ranges { get; init; }

    [JsonPropertyName("hostDocumentVersion")]
    public int? HostDocumentVersion { get; init; }
}

internal sealed record class RazorMapToDocumentEditsResponse
{
    [JsonPropertyName("edits")]
    public required TextChange[] Edits { get; init; }

    [JsonPropertyName("hostDocumentVersion")]
    public int? HostDocumentVersion { get; init; }
}
