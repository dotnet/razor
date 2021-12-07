// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert
{
    internal class OnAutoInsertResponse
    {
        [JsonProperty("_vs_textEditFormat")]
        public InsertTextFormat TextEditFormat { get; set; }

        [JsonProperty("_vs_textEdit")]
        public TextEdit TextEdit { get; set; }
    }
}

