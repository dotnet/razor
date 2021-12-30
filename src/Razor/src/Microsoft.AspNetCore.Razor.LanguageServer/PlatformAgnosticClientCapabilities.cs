// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Newtonsoft.Json;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    /// <summary>
    /// These client capabilities represent the superset of client capabilities from VS and VSCode.
    /// </summary>
    internal class PlatformAgnosticClientCapabilities : ClientCapabilities
    {
        public static readonly PlatformExtensionConverter<ClientCapabilities, PlatformAgnosticClientCapabilities> JsonConverter = new PlatformExtensionConverter<ClientCapabilities, PlatformAgnosticClientCapabilities>();

        [JsonProperty("_vs_supportsVisualStudioExtensions")]
        public bool SupportsVisualStudioExtensions { get; set; } = false;
    }
}
