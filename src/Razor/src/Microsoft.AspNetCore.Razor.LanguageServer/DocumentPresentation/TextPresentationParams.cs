// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MediatR;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using VS = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation
{
    /// <summary>
    /// Class representing the parameters sent for a textDocument/_vs_textPresentation request.
    /// </summary>
    internal class TextPresentationParams : ITextDocumentIdentifierParams, IRequest<WorkspaceEdit?>, IBaseRequest, IPresentationParams
    {
        /// <inheritdoc cref="VS.VSInternalTextPresentationParams.TextDocument"/>
        [JsonProperty("_vs_textDocument")]
        public TextDocumentIdentifier TextDocument { get; set; }

        /// <inheritdoc cref="VS.VSInternalTextPresentationParams.Range"/>
        [JsonProperty("_vs_range")]
        public Range Range { get; set; }

        /// <inheritdoc cref="VS.VSInternalTextPresentationParams.Text"/>
        [JsonProperty("_vs_text")]
        public string? Text { get; set; }

        public TextPresentationParams(TextDocumentIdentifier textDocument, Range range)
        {
            TextDocument = textDocument;
            Range = range;
        }
    }
}
