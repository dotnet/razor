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
    /// <summary>
    /// Text edit that should be applied to generated C# document prior to invoking completion
    /// </summary>
    /// <remarks>
    /// Provisional completion happens when the user just type "." in something like @DateTime.
    /// and the dot is initially in HTML rather than C#. Since we don't want HTML completions
    /// in that case, we cheat and modify C# buffer immediately but temporarily, not waiting for
    /// reparse/regen, before showing completion.
    /// </remarks>
    [DataMember(Name = "provisionalTextEdit")]
    [JsonPropertyName("provisionalTextEdit")]
    public TextEdit? ProvisionalTextEdit { get; set; }

    /// <summary>
    /// Document position mapping data for language mappings
    /// </summary>
    [DataMember(Name = "documentPositionInfo")]
    [JsonPropertyName("documentPositionInfo")]
    public required DocumentPositionInfo DocumentPositionInfo { get; set; }

    /// <summary>
    /// Indicates that snippets should be added to delegated completion list (currently for HTML only)
    /// </summary>
    [DataMember(Name = "shouldIncludeDelegationSnippets")]
    [JsonPropertyName("shouldIncludeDelegationSnippets")]
    public bool ShouldIncludeDelegationSnippets { get; set; }
}
