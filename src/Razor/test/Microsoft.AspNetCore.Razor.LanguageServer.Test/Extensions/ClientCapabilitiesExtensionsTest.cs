// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using Xunit;
using OmniSharpClientCapabilities = OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities.ClientCapabilities;
using OmniSharpCompletionCapability = OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities.CompletionCapability;
using OmniSharpCompletionSupport = OmniSharp.Extensions.LanguageServer.Protocol.Supports<OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities.CompletionCapability?>;
using OmniSharpTextDocumentClientCapability = OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities.TextDocumentClientCapabilities;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions
{
    public class ClientCapabilitiesExtensionsTest
    {
        [Fact]
        public void ToVSClientCapabilities_WorksAsExpected()
        {
            // Arrange
            var experimentalCapability = new Dictionary<string, JToken>()
            {
                ["test"] = "Hello World"
            };
            var omniSharpCapabilities = new OmniSharpClientCapabilities()
            {
                Experimental = experimentalCapability,
                TextDocument = new OmniSharpTextDocumentClientCapability()
                {
                    Completion = new OmniSharpCompletionSupport(
                        new OmniSharpCompletionCapability()
                        {
                            ContextSupport = true,
                        }),
                }
            };
            var serializer = new LspSerializer();
            serializer.RegisterVSInternalExtensionConverters();

            // Act
            var vsCapabilities = omniSharpCapabilities.ToVSClientCapabilities(serializer);

            // Assert
            Assert.NotNull(vsCapabilities.Experimental);
            var actualExperimental = JObject.FromObject(vsCapabilities.Experimental!);
            var expectedExperimental = JObject.FromObject(experimentalCapability);

            Assert.Equal(expectedExperimental, actualExperimental);
            Assert.NotNull(vsCapabilities.TextDocument);
            Assert.True(vsCapabilities.TextDocument?.Completion?.ContextSupport);
        }
    }
}
