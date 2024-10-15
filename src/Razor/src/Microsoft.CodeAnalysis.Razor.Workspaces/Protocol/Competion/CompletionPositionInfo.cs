// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Protocol.Completion;

[DataContract]
internal record struct CompletionPositionInfo
{
    [DataMember(Name = "provisionalTextEdit")]
    [JsonPropertyName("provisionalTextEdit")]
    public TextEdit? ProvisionalTextEdit { get; set; }

    [DataMember(Name = "provisionalPositionInfo")]
    [JsonPropertyName("provisionalPositionInfo")]
    public required DocumentPositionInfo DocumentPositionInfo { get; set; }

    [DataMember(Name = "shouldIncludeSnippets")]
    [JsonPropertyName("shouldIncludeSnippets")]
    public bool ShouldIncludeSnippets { get; set; }
}
