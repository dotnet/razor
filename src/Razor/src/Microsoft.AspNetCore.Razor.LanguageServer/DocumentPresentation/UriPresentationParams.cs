// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using MediatR;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using VS = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation
{
    /// <summary>
    /// Class representing the parameters sent for a textDocument/_vs_textPresentation request.
    /// </summary>
    internal class UriPresentationParams : ITextDocumentIdentifierParams, IRequest<WorkspaceEdit?>, IBaseRequest
    {
        /// <inheritdoc cref="VS.VSInternalUriPresentationParams.TextDocument"/>
        [JsonProperty("_vs_textDocument")]
        public TextDocumentIdentifier TextDocument { get; set; }

        /// <inheritdoc cref="VS.VSInternalUriPresentationParams.Range"/>
        [JsonProperty("_vs_range")]
        public Range Range { get; set; }

        /// <inheritdoc cref="VS.VSInternalUriPresentationParams.Uris"/>
        [JsonProperty("_vs_uris")]
        public Uri[]? Uris { get; set; }

        public UriPresentationParams(TextDocumentIdentifier textDocument, Range range)
        {
            TextDocument = textDocument;
            Range = range;
        }
    }
}
