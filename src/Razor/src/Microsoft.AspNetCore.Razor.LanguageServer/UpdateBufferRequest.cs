// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class UpdateBufferRequest
{
    [JsonPropertyName("hostDocumentVersion")]
    public int? HostDocumentVersion { get; set; }

    [JsonPropertyName("projectKeyId")]
    public string? ProjectKeyId { get; set; }

    [JsonPropertyName("hostDocumentFilePath")]
    public required string HostDocumentFilePath { get; set; }

    [JsonPropertyName("changes")]
    public required IReadOnlyList<TextChange> Changes { get; set; }

    [JsonPropertyName("previousWasEmpty")]
    public bool PreviousWasEmpty { get; set; }
}
