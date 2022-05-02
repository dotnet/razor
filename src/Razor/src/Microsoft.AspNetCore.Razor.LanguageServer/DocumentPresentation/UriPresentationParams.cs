// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using MediatR;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation
{
    /// <summary>
    /// Class representing the parameters sent for a textDocument/_vs_textPresentation request.
    /// </summary>
    internal class UriPresentationParams : ITextDocumentIdentifierParams, IRequest<WorkspaceEdit?>, IBaseRequest
    {
        /// <summary>
        /// Gets or sets the identifier for the text document to be operate on.
        /// </summary>
        [JsonProperty("_vs_textDocument")]
        public TextDocumentIdentifier TextDocument { get; set; }

        /// <summary>
        /// Gets or sets the range.
        /// </summary>
        [JsonProperty("_vs_range")]
        public Range Range { get; set; }

        /// <summary>
        /// Gets or sets the URI values. Valid for DropKind.Uris.
        /// </summary>
        [JsonProperty("_vs_uris")]
        public Uri[]? Uris { get; set; }

        public UriPresentationParams(TextDocumentIdentifier textDocument, Range range)
        {
            TextDocument = textDocument;
            Range = range;
        }
    }
}
