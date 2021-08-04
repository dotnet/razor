// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal class PlatformAgnosticCompletionCapability : CompletionCapability
    {
        public static readonly PlatformExtensionConverter<CompletionCapability, PlatformAgnosticCompletionCapability> JsonConverter = new PlatformExtensionConverter<CompletionCapability, PlatformAgnosticCompletionCapability>();

        [JsonProperty("_vs_completionList")]
        public VSCompletionListCapability VSCompletionList { get; set; }
    }
}
