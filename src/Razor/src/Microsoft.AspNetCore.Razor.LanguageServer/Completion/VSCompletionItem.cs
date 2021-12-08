// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    /// <summary>
    /// VS-specific completion item based off of LSP's VSCompletionItem.
    /// </summary>
    internal record VSCompletionItem : CompletionItem
    {
        [JsonProperty("_vs_description")]
        public VSClassifiedTextElement Description { get; set; }
    }
}
