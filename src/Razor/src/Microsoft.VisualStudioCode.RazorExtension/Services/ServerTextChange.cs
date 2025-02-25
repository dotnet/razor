// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace Microsoft.VisualStudioCode.RazorExtension.Services;

internal sealed class ServerTextChange
{
    [JsonPropertyName("span")]
    public required ServerTextSpan Span { get; set; }

    [JsonPropertyName("newText")]
    public required string NewText { get; set; }
}
