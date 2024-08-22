// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;

// NOTE: As mentioned before, these have changed in future PRs, where much of the Provider logic was moved to the resolver.
// The last three properties are not used in the current implementation.
internal sealed class ExtractToComponentCodeActionParams
{
    [JsonPropertyName("uri")]
    public required Uri Uri { get; set; }

    [JsonPropertyName("extractStart")]
    public int ExtractStart { get; set; }

    [JsonPropertyName("extractEnd")]
    public int ExtractEnd { get; set; }

    [JsonPropertyName("namespace")]
    public required string Namespace { get; set; }

    [JsonPropertyName("dependencies")]
    public required List<string> Dependencies { get; set; }

    [JsonPropertyName("usedIdentifiers")]
    public required HashSet<string> UsedIdentifiers { get; set; }

    [JsonPropertyName("usedMembers")]
    public required HashSet<string> UsedMembers { get; set; }
}
