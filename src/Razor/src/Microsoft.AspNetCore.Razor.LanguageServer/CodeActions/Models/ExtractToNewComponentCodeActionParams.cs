// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;

internal sealed class ExtractToNewComponentCodeActionParams
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
