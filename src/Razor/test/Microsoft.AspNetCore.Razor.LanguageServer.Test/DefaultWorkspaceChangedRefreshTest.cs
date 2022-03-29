// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class DefaultWorkspaceChangedPublisherTest : LanguageServerTestBase
    {
        [Fact]
        public void PublishWorkspaceChanged_DoesNotSendWorkspaceRefreshRequest_WhenNotSupported()
        {
            // Arrange
            var clientSettings = new InitializeParams
            {
                Capabilities = new ClientCapabilities
                {
                    Workspace = new WorkspaceClientCapabilities
                    {
                        SemanticTokens = new Supports<SemanticTokensWorkspaceCapability>(new SemanticTokensWorkspaceCapability
                        {
                            RefreshSupport = false
                        })
                    }
                }
            };
            var serverClient = new TestClient(clientSettings);
            var defaultWorkspaceChangedPublisher = new DefaultWorkspaceChangedPublisher(serverClient);

            // Act
            defaultWorkspaceChangedPublisher.PublishWorkspaceChanged();

            // Assert
            Assert.Equal(0, serverClient.Requests.Count);
        }

        [Fact]
        public void PublishWorkspaceChanged_SendsWorkspaceRefreshRequest_WhenSupported()
        {
            // Arrange
            var clientSettings = new InitializeParams
            {
                Capabilities = new ClientCapabilities
                {
                    Workspace = new WorkspaceClientCapabilities
                    {
                        SemanticTokens = new Supports<SemanticTokensWorkspaceCapability>(new SemanticTokensWorkspaceCapability
                        {
                            RefreshSupport = true
                        })
                    }
                }
            };
            var serverClient = new TestClient(clientSettings);
            var defaultWorkspaceChangedPublisher = new DefaultWorkspaceChangedPublisher(serverClient);

            // Act
            defaultWorkspaceChangedPublisher.PublishWorkspaceChanged();

            // Assert
            Assert.Collection(serverClient.Requests,
                r => Assert.Equal(WorkspaceNames.SemanticTokensRefresh, r.Method));
        }
    }

    public class DefaultWorkspaceChangedRefreshTest : LanguageServerTestBase
    {
        public DefaultWorkspaceChangedRefreshTest()
        {
            ProjectManager = TestProjectSnapshotManager.Create(LegacyDispatcher);
            ProjectManager.AllowNotifyListeners = true;
            HostProject = new HostProject("/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
            ProjectManager.ProjectAdded(HostProject);
            HostDocument = new HostDocument("/path/to/file.razor", "file.razor");
            ProjectManager.DocumentAdded(HostProject, HostDocument, new EmptyTextLoader(HostDocument.FilePath));
        }

        private TestProjectSnapshotManager ProjectManager { get; }
        private HostProject HostProject { get; }
        private HostDocument HostDocument { get; }

        [Fact]
        public void PublishesOnWorkspaceUpdate()
        {
            // Arrange
            var workspaceChangedPublisher = new Mock<WorkspaceChangedPublisher>(MockBehavior.Strict);
            workspaceChangedPublisher.Setup(w => w.PublishWorkspaceChanged());
            var defaultWorkspaceChangedRefresh = new DefaultWorkspaceChangedRefresh(workspaceChangedPublisher.Object);
            defaultWorkspaceChangedRefresh.Initialize(ProjectManager);

            // Act
            ProjectManager.DocumentChanged(HostProject.FilePath, HostDocument.FilePath, new EmptyTextLoader(HostDocument.FilePath));

            // Assert
            workspaceChangedPublisher.VerifyAll();
        }
    }
}
