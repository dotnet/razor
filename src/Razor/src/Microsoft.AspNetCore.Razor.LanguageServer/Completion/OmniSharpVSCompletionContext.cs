// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal record OmniSharpVSCompletionContext : CompletionContext
    {
        public static readonly PlatformExtensionConverter<CompletionContext, OmniSharpVSCompletionContext> JsonConverter = new PlatformExtensionConverter<CompletionContext, OmniSharpVSCompletionContext>();

        [JsonProperty("_vs_invokeKind")]
        public OmniSharpVSCompletionInvokeKind InvokeKind { get; init; }
    }
}
