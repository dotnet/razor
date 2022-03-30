// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.AspNetCore.Razor.Test.Common;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
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
            Thread.Sleep(100);

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
            Thread.Sleep(100);

            // Assert
            Assert.Collection(serverClient.Requests,
                r => Assert.Equal(WorkspaceNames.SemanticTokensRefresh, r.Method));
        }

        [Fact]
        public void PublishWorkspaceChanged_DebouncesWorkspaceRefreshRequest()
        {
            // Arrange
            var clientSettings = GetInitializedParams(semanticRefreshEnabled: true);
            var serverClient = new TestClient(clientSettings);
            var defaultWorkspaceChangedPublisher = new DefaultWorkspaceSemanticTokensRefreshPublisher(serverClient);

            // Act
            defaultWorkspaceChangedPublisher.PublishWorkspaceSemanticTokensRefresh();
            defaultWorkspaceChangedPublisher.PublishWorkspaceSemanticTokensRefresh();
            Thread.Sleep(TimeSpan.FromMilliseconds(100));
            defaultWorkspaceChangedPublisher.PublishWorkspaceSemanticTokensRefresh();
            defaultWorkspaceChangedPublisher.PublishWorkspaceSemanticTokensRefresh();

            Thread.Sleep(TimeSpan.FromSeconds(1));

            // Assert
            Assert.Collection(serverClient.Requests,
                r => Assert.Equal(WorkspaceNames.SemanticTokensRefresh, r.Method),
                r => Assert.Equal(WorkspaceNames.SemanticTokensRefresh, r.Method));
        }

        private static InitializeParams GetInitializedParams(bool semanticRefreshEnabled)
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
