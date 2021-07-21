// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal class OmniSharpVSCompletionContext : CompletionContext
    {
        public static readonly PlatformExtensionConverter<CompletionContext, OmniSharpVSCompletionContext> JsonConverter = new PlatformExtensionConverter<CompletionContext, OmniSharpVSCompletionContext>();

        [JsonProperty("_vs_invokeKind")]
        public OmniSharpVSCompletionInvokeKind InvokeKind { get; set; }
    }
}
