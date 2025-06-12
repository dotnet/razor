// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace Microsoft.CodeAnalysis.Razor.Protocol.Debugging;

internal class RazorProximityExpressionsParams
{
    [JsonPropertyName("uri")]
    [JsonConverter(typeof(DocumentUriConverter))]
    public required DocumentUri Uri { get; init; }

    [JsonPropertyName("position")]
    public required Position Position { get; init; }

    [JsonPropertyName("hostDocumentSyncVersion")]
    public required long HostDocumentSyncVersion { get; init; }
}
