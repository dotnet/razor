// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using MediatR;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.WrapWithTag
{
    /// <summary>
    /// Class representing the parameters sent for a textDocument/_vsweb_wrapWithTag request.
    /// </summary>
    internal class WrapWithTagParams : ITextDocumentIdentifierParams, IRequest<WrapWithTagResponse>, IBaseRequest
    {
        /// <summary>
        /// Gets or sets the identifier for the text document to be operate on.
        /// </summary>
        [JsonProperty("_vs_textDocument")]
        public TextDocumentIdentifier TextDocument { get; set; }

        /// <summary>
        /// Gets or sets the selection range to be wrapped.
        /// </summary>
        [JsonProperty("_vs_range")]
        public Range? Range { get; set; }

        /// <summary>
        /// Gets or sets the wrapping tag name.
        /// </summary>
        [JsonProperty("_vs_tagName")]
        public string? TagName { get; set; }

        /// <summary>
        /// Gets or sets the formatting options.
        /// </summary>
        [JsonProperty("_vs_options")]
        public FormattingOptions? Options { get; set; }

        public WrapWithTagParams(TextDocumentIdentifier textDocument)
        {
            TextDocument = textDocument;
        }
    }
}
