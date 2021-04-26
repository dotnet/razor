// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Tooltip
{
    public abstract class DefaultTagHelperDescriptionFactoryTestBase
    {
        internal static ClientNotifierServiceBase GetLanguageServer(bool supportsVisualStudioExtensions = false)
        {
            var initializeParams = new InitializeParams
            {
                Capabilities = new PlatformAgnosticClientCapabilities
                {
                    TextDocument = new TextDocumentClientCapabilities
                    {
                        Completion = new Supports<CompletionCapability>
                        {
                            Value = new CompletionCapability
                            {
                                CompletionItem = new CompletionItemCapabilityOptions
                                {
                                    SnippetSupport = true,
                                    DocumentationFormat = new Container<MarkupKind>(MarkupKind.Markdown)
                                }
                            }
                        }
                    },
                    SupportsVisualStudioExtensions = supportsVisualStudioExtensions,
                }
            };

            var languageServer = new Mock<ClientNotifierServiceBase>(MockBehavior.Strict);
            languageServer.SetupGet(server => server.ClientSettings)
                .Returns(initializeParams);

            return languageServer.Object;
        }
    }
}
