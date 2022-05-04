// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.Text.Adornments;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using ObjectContentConverter = Microsoft.VisualStudio.LanguageServer.Protocol.ObjectContentConverter;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    /// <summary>
    /// VS-specific completion item based off of LSP's VSCompletionItem.
    /// </summary>
    internal record VSCompletionItem : CompletionItem
    {
        [JsonProperty("_vs_description")]
        [JsonConverter(typeof(ObjectContentConverter))]
        public ClassifiedTextElement? Description { get; set; }

        [JsonProperty("_vs_commitCharacters")]
        public Container<VSCommitCharacter>? VsCommitCharacters { get; set; }
    }
}
