// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert
{
    public class OnAutoInsertRegistrationOptions : ITextDocumentRegistrationOptions
    {
        public DocumentSelector DocumentSelector { get; set; }

        [JsonProperty("_vs_triggerCharacters")]
        public Container<string> TriggerCharacters { get; set; }
    }
}
