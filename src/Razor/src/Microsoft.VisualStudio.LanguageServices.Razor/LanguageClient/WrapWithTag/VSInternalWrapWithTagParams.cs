// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.Razor.LanguageClient.WrapWithTag;

/// <summary>
/// Class representing the parameters sent for a textDocument/_vsweb_wrapWithTag request.
/// Matches corresponding class in Web Tools' Html language server
/// </summary>
internal class VSInternalWrapWithTagParams
{
    public VSInternalWrapWithTagParams(Range range,
                                       string tagName,
                                       FormattingOptions options,
                                       VersionedTextDocumentIdentifier textDocument)
    {
        Range = range;
        Options = options;
        TagName = tagName;
        TextDocument = textDocument;
    }

    /// <summary>
    /// Gets or sets the identifier for the text document to be operate on.
    /// </summary>
    [JsonPropertyName("_vs_textDocument")]
    public VersionedTextDocumentIdentifier TextDocument
    {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the selection range to be wrapped.
    /// </summary>
    [JsonPropertyName("_vs_range")]
    public Range Range
    {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the wrapping tag name.
    /// </summary>
    [JsonPropertyName("_vs_tagName")]
    public string TagName
    {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the formatting options.
    /// </summary>
    [JsonPropertyName("_vs_options")]
    public FormattingOptions Options
    {
        get;
        set;
    }
}
