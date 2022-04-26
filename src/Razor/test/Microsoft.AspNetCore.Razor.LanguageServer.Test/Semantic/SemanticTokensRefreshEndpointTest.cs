// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test.Semantic
{
    public class SemanticTokensRefreshEndpointTest
    {
        [Fact]
        public async Task Handle_QueuesRefresh()
        {
            // Arrange
            var clientSettings = GetInitializedParams(semanticRefreshEnabled: true);
            var serverClient = new TestClient(clientSettings);
            var errorReporter = new TestErrorReporter();
            using var semanticTokensRefreshPublisher = new DefaultWorkspaceSemanticTokensRefreshPublisher(serverClient, errorReporter);
            var semanticTokensCacheService = new DefaultSemanticTokensCacheService();
            var refreshEndpoint = new SemanticTokensRefreshEndpoint(semanticTokensRefreshPublisher, semanticTokensCacheService);
            var refreshParams = new SemanticTokensRefreshParams();

            // Act
            await refreshEndpoint.Handle(refreshParams, CancellationToken.None);
            semanticTokensRefreshPublisher.GetTestAccessor().WaitForEmpty();

            // Assert
            Assert.Equal(WorkspaceNames.SemanticTokensRefresh, serverClient.Requests.Single().Method);
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

        private class TestErrorReporter : ErrorReporter
        {
            public override void ReportError(Exception exception)
            {
                throw new NotImplementedException();
            }

            public override void ReportError(Exception exception, ProjectSnapshot? project)
            {
                throw new NotImplementedException();
            }

            public override void ReportError(Exception exception, Project workspaceProject)
            {
                throw new NotImplementedException();
            }
        }
    }
}
