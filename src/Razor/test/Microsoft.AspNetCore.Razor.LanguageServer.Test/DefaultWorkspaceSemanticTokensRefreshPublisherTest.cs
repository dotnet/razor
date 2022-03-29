// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Test.Common;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class DefaultWorkspaceSemanticTokensRefreshPublisherTest : LanguageServerTestBase
    {
        [Fact]
        public void PublishWorkspaceChanged_DoesNotSendWorkspaceRefreshRequest_WhenNotSupported()
        {
            // Arrange
            var clientSettings = GetInitializedParams(semanticRefreshEnabled: false);
            var serverClient = new TestClient(clientSettings);
            var defaultWorkspaceChangedPublisher = new DefaultWorkspaceSemanticTokensRefreshPublisher(serverClient);

            // Act
            defaultWorkspaceChangedPublisher.PublishWorkspaceSemanticTokensRefresh();

            // Assert
            Assert.Equal(0, serverClient.Requests.Count);
        }

        [Fact]
        public void PublishWorkspaceChanged_SendsWorkspaceRefreshRequest_WhenSupported()
        {
            // Arrange
            var clientSettings = GetInitializedParams(semanticRefreshEnabled: true);
            var serverClient = new TestClient(clientSettings);
            var defaultWorkspaceChangedPublisher = new DefaultWorkspaceSemanticTokensRefreshPublisher(serverClient);

            // Act
            defaultWorkspaceChangedPublisher.PublishWorkspaceSemanticTokensRefresh();

            // Assert
            Assert.Collection(serverClient.Requests,
                r => Assert.Equal(WorkspaceNames.SemanticTokensRefresh, r.Method));
        }

        private InitializeParams GetInitializedParams(bool semanticRefreshEnabled)
        {
            return new InitializeParams
            {
                Capabilities = new ClientCapabilities
                {
                    Workspace = new WorkspaceClientCapabilities
                    {
                        SemanticTokens = new Supports<SemanticTokensWorkspaceCapability>(new SemanticTokensWorkspaceCapability
                        {
                            RefreshSupport = semanticRefreshEnabled
                        })
                    }
                }
            };
        }
    }
}
