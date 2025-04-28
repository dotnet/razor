﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Microsoft.VisualStudio.Razor.LanguageClient.WrapWithTag;

/// <summary>
/// Class representing the response of an WrapWithTag response.
/// Matches corresponding class in Web Tools' Html language server
/// </summary>
[DataContract]
internal class VSInternalWrapWithTagResponse
{
    public VSInternalWrapWithTagResponse(LspRange tagRange, TextEdit[] textEdits)
    {
        TagRange = tagRange;
        TextEdits = textEdits;
    }

    /// <summary>
    /// Gets or sets the range of the wrapping tag.
    /// </summary>
    [DataMember(Name = "_vs_tagRange")]
    [JsonPropertyName("_vs_tagRange")]
    public LspRange TagRange { get; }

    /// <summary>
    /// Gets or sets the text edits.
    /// </summary>
    [DataMember(Name = "_vs_textEdits")]
    [JsonPropertyName("_vs_textEdits")]
    public TextEdit[] TextEdits
    {
        get;
        set;
    }
}
