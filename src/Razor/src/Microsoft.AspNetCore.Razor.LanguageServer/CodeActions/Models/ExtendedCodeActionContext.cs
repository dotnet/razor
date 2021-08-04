// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models
{
    internal class ExtendedCodeActionContext : CodeActionContext
    {
        [Optional]
        [JsonProperty("_vs_selectionRange")]
        public Range SelectionRange { get; set; }
    }
}
