// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.Razor.LanguageClient.WrapWithTag;

/// <summary>
/// Class representing the response of an WrapWithTag response.
/// Matches corresponding class in Web Tools' Html language server
/// </summary>
internal class VSInternalWrapWithTagResponse
{
    public VSInternalWrapWithTagResponse(Range tagRange, TextEdit[] textEdits)
    {
        TagRange = tagRange;
        TextEdits = textEdits;
    }

    /// <summary>
    /// Gets or sets the range of the wrapping tag.
    /// </summary>
    [JsonPropertyName("_vs_tagRange")]
    public Range TagRange { get; }

    /// <summary>
    /// Gets or sets the text edits.
    /// </summary>
    [JsonPropertyName("_vs_textEdits")]
    public TextEdit[] TextEdits
    {
        get;
        set;
    }
}
