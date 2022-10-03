// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal record RazorLanguageServerCapability(
        bool LanguageQuery,
        bool RangeMapping,
        bool EditMapping,
        bool MonitorProjectConfigurationFilePath,
        bool BreakpointSpan,
        bool ProximityExpressions)
    {
        private const string RazorCapabilityKey = "razor";
        private static readonly RazorLanguageServerCapability s_default = new RazorLanguageServerCapability(
            LanguageQuery: true,
            RangeMapping: true,
            EditMapping: true,
            MonitorProjectConfigurationFilePath: true,
            BreakpointSpan: true,
            ProximityExpressions: true);

        public static void AddTo(ServerCapabilities capabilities)
        {
            // We have to use the experimental capabilities bucket here because not all platforms maintain custom capabilities. For instance
            // in Visual Studio scenarios it will deserialize server capabilties into what it believes is "valid" and then will re-pass said
            // server capabilities to our client side code having lost the information of the custom capabilities. To avoid this we use the
            // experimental bag since it's part of the official LSP spec. This approach enables us to work with any client.
            capabilities.Experimental ??= new Dictionary<string, JToken> {
                { RazorCapabilityKey, JToken.FromObject(s_default) }
            };
        }

        public static bool TryGet(JToken token, [NotNullWhen(true)] out RazorLanguageServerCapability? razorCapability)
        {
            if (token is not JObject jobject)
            {
                razorCapability = null;
                return false;
            }

            if (!jobject.TryGetValue("experimental", out var experimentalCapabilitiesToken))
            {
                razorCapability = null;
                return false;
            }

            if (experimentalCapabilitiesToken is not JObject experimentalCapabilities)
            {
                razorCapability = null;
                return false;
            }

            if (!experimentalCapabilities.TryGetValue(RazorCapabilityKey, out var razorCapabilityToken))
            {
                razorCapability = null;
                return false;
            }

            razorCapability = razorCapabilityToken.ToObject<RazorLanguageServerCapability>();
            return razorCapability is not null;
        }
    }
}
