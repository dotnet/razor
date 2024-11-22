// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace Microsoft.CodeAnalysis.Razor.CodeActions.Models;

internal sealed class PromoteToUsingCodeActionParams
{
    [JsonPropertyName("importsFileName")]
    public required string ImportsFileName { get; init; }

    [JsonPropertyName("usingDirective")]
    public required string UsingDirective { get; init; }

    [JsonPropertyName("removeStart")]
    public required int RemoveStart { get; init; }

    [JsonPropertyName("removeEnd")]
    public required int RemoveEnd { get; init; }
}
