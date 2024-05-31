// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Endpoints;

internal class UpdateBufferRequest
{
    [JsonPropertyName("hostDocumentVersion")]
    public int? HostDocumentVersion { get; init; }

    [JsonPropertyName("projectKeyId")]
    public string? ProjectKeyId { get; init; }

    [JsonPropertyName("hostDocumentFilePath")]
    public string? HostDocumentFilePath { get; init; }

    [JsonPropertyName("changes")]
    public required IReadOnlyList<TextChange> Changes { get; init; }

    [JsonPropertyName("previousWasEmpty")]
    public bool PreviousWasEmpty { get; set; }
}
