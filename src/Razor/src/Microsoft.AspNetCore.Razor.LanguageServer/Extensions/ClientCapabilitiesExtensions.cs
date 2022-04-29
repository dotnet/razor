// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using OmniSharpClientCapabilities = OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities.ClientCapabilities;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    internal static class ClientCapabilitiesExtensions
    {
        public static VSInternalClientCapabilities ToVSClientCapabilities(this OmniSharpClientCapabilities omniSharpClientCapabilities, LspSerializer serializer)
        {
            var serializedOmniSharpClientCapabilities = serializer.SerializeObject(omniSharpClientCapabilities);
            var vsClientCapabilities = serializer.DeserializeObject<VSInternalClientCapabilities>(serializedOmniSharpClientCapabilities);
            return vsClientCapabilities;
        }
    }
}
