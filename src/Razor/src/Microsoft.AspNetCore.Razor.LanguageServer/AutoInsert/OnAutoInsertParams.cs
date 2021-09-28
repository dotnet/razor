// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MediatR;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert
{
    internal class OnAutoInsertParams : ITextDocumentIdentifierParams, IRequest<OnAutoInsertResponse>, IBaseRequest
    {
        [JsonProperty("_vs_textDocument")]
        public TextDocumentIdentifier TextDocument { get; set; }

        [JsonProperty("_vs_position")]
        public Position Position { get; set; }

        [JsonProperty("_vs_ch")]
        public string Character { get; set; }

        [JsonProperty("_vs_options")]
        public FormattingOptions Options { get; set; }
    }
}
