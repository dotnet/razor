// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using OmniSharpClientCapabilities = OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities.ClientCapabilities;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    internal static class ClientCapabilitiesExtensions
    {
        public static VSInternalClientCapabilities ToVSClientCapabilities(this OmniSharpClientCapabilities omniSharpClientCapabilities, LspSerializer serializer)
        {
            var jsonCapturingCapabilities = omniSharpClientCapabilities as ICaptureJson;
            if (jsonCapturingCapabilities is null)
            {
                throw new InvalidOperationException("Client capability deserializers not registered, failed to convert to " + nameof(ICaptureJson));
            }

            var vsClientCapabilities = jsonCapturingCapabilities.Json.ToObject<VSInternalClientCapabilities>(serializer.JsonSerializer);
            if (vsClientCapabilities is null)
            {
                throw new InvalidOperationException("Failed to convert client capabilities");
            }

            return vsClientCapabilities;
        }
    }
}
