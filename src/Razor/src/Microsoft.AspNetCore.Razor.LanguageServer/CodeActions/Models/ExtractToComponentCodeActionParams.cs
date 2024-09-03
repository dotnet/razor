// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;

// NOTE: As mentioned before, these have changed in future PRs, where much of the Provider logic was moved to the resolver.
// The last three properties are not used in the current implementation.
internal sealed class ExtractToComponentCodeActionParams
{
    [JsonPropertyName("uri")]
    public required Uri Uri { get; set; }

    [JsonPropertyName("selectStart")]
    public required Position SelectStart { get; set; }

    [JsonPropertyName("selectEnd")]
    public required Position SelectEnd { get; set; }

    [JsonPropertyName("absoluteIndex")]
    public required int AbsoluteIndex { get; set; }

    [JsonPropertyName("namespace")]
    public required string Namespace { get; set; }
}
