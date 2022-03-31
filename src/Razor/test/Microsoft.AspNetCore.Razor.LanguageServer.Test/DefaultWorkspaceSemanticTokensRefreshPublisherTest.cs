// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
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
            var errorReporter = new TestErrorReporter();
            var defaultWorkspaceChangedPublisher = new DefaultWorkspaceSemanticTokensRefreshPublisher(serverClient, errorReporter);
            var testAccessor = defaultWorkspaceChangedPublisher.GetTestAccessor();

            // Act
            defaultWorkspaceChangedPublisher.PublishWorkspaceSemanticTokensRefresh();
            testAccessor.WaitForEmpty();

            // Assert
            Assert.Equal(0, serverClient.Requests.Count);
        }

        [Fact]
        public void PublishWorkspaceChanged_SendsWorkspaceRefreshRequest_WhenSupported()
        {
            // Arrange
            var clientSettings = GetInitializedParams(semanticRefreshEnabled: true);
            var serverClient = new TestClient(clientSettings);
            var errorReporter = new TestErrorReporter();
            var defaultWorkspaceChangedPublisher = new DefaultWorkspaceSemanticTokensRefreshPublisher(serverClient, errorReporter);
            var testAccessor = defaultWorkspaceChangedPublisher.GetTestAccessor();

            // Act
            defaultWorkspaceChangedPublisher.PublishWorkspaceSemanticTokensRefresh();
            testAccessor.WaitForEmpty();

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
            var errorReporter = new TestErrorReporter();
            var defaultWorkspaceChangedPublisher = new DefaultWorkspaceSemanticTokensRefreshPublisher(serverClient, errorReporter);
            var testAccessor = defaultWorkspaceChangedPublisher.GetTestAccessor();

            // Act
            defaultWorkspaceChangedPublisher.PublishWorkspaceSemanticTokensRefresh();
            defaultWorkspaceChangedPublisher.PublishWorkspaceSemanticTokensRefresh();
            testAccessor.WaitForEmpty();
            defaultWorkspaceChangedPublisher.PublishWorkspaceSemanticTokensRefresh();
            defaultWorkspaceChangedPublisher.PublishWorkspaceSemanticTokensRefresh();
            testAccessor.WaitForEmpty();

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

        private class TestErrorReporter : ErrorReporter
        {
            public override void ReportError(Exception exception)
            {
                throw new NotImplementedException();
            }

            public override void ReportError(Exception exception, ProjectSnapshot project)
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
