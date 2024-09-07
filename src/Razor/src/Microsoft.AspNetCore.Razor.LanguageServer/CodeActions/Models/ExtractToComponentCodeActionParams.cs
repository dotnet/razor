// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;

// NOTE: As mentioned before, these have changed in future PRs, where much of the Provider logic was moved to the resolver.
// The last three properties are not used in the current implementation.
internal sealed class ExtractToComponentCodeActionParams
{
    [JsonPropertyName("uri")]
    public required Uri Uri { get; set; }

    [JsonPropertyName("extractStart")]
    public required int ExtractStart { get; set; }

    [JsonPropertyName("extractEnd")]
    public required int ExtractEnd { get; set; }

    [JsonPropertyName("hasEventHandlerOrExpression")]
    public required bool HasEventHandlerOrExpression { get; set; }

    [JsonPropertyName("hasAtCodeBlock")]
    public required bool HasAtCodeBlock { get; set; }

    [JsonPropertyName("usingDirectives")]
    public required string[] UsingDirectives {  get; set; }

    [JsonPropertyName("dedentWhitespaceString")]
    public required string DedentWhitespaceString {  get; set; }

    [JsonPropertyName("namespace")]
    public required string Namespace { get; set; }
}
