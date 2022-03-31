// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class DefaultWorkspaceSemanticTokensRefreshTriggerTest : LanguageServerTestBase
    {
        public DefaultWorkspaceSemanticTokensRefreshTriggerTest()
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
            var workspaceChangedPublisher = new Mock<IWorkspaceSemanticTokensRefreshPublisher>(MockBehavior.Strict);
            workspaceChangedPublisher.Setup(w => w.PublishWorkspaceSemanticTokensRefresh());
            var clientLanguageServer = new Mock<IClientLanguageServer>(MockBehavior.Strict);
            var defaultWorkspaceChangedRefresh = new TestDefaultWorkspaceSemanticTokensRefreshTrigger(clientLanguageServer.Object, workspaceChangedPublisher.Object);
            defaultWorkspaceChangedRefresh.Initialize(ProjectManager);

            // Act
            var newDocument = new HostDocument("/path/to/newFile.razor", "newFile.razor");
            ProjectManager.DocumentAdded(HostProject, newDocument, new EmptyTextLoader(newDocument.FilePath));

            // Assert
            workspaceChangedPublisher.VerifyAll();
        }

        private class TestDefaultWorkspaceSemanticTokensRefreshTrigger : DefaultWorkspaceSemanticTokensRefreshTrigger
        {
            private readonly IWorkspaceSemanticTokensRefreshPublisher _workspaceSemanticTokensRefreshPublisher;

            internal TestDefaultWorkspaceSemanticTokensRefreshTrigger(IClientLanguageServer clientLanguageServer, IWorkspaceSemanticTokensRefreshPublisher workspaceSemanticTokensRefreshPublisher) : base(clientLanguageServer)
            {
                _workspaceSemanticTokensRefreshPublisher = workspaceSemanticTokensRefreshPublisher;
            }

            internal override IWorkspaceSemanticTokensRefreshPublisher GetWorkspaceSemanticTokensRefreshPublisher(ProjectSnapshotManagerBase projectManager)
            {
                return _workspaceSemanticTokensRefreshPublisher;
            }
        }
    }
}
