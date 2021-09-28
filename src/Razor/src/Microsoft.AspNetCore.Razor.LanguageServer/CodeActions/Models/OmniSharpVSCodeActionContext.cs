// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models
{
    internal record OmniSharpVSCodeActionContext : CodeActionContext
    {
        public static readonly PlatformExtensionConverter<CodeActionContext, OmniSharpVSCodeActionContext> JsonConverter = new();

        [Optional]
        [JsonProperty("_vs_selectionRange")]
        public Range SelectionRange { get; init; }
    }
}
