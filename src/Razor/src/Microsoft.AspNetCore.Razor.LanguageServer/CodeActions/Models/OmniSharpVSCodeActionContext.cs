// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models
{
    internal class OmniSharpVSCodeActionContext : CodeActionContext
    {
        public static readonly PlatformExtensionConverter<CodeActionContext, OmniSharpVSCodeActionContext> JsonConverter = new();

        [JsonProperty("_vs_selectionRange", NullValueHandling = NullValueHandling.Ignore)]
        public Range? SelectionRange { get; set; }
    }
}
