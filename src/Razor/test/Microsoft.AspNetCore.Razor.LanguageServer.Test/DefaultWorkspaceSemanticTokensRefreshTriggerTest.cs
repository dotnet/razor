// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Moq;
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
            var workspaceChangedPublisher = new Mock<WorkspaceSemanticTokensRefreshPublisher>(MockBehavior.Strict);
            workspaceChangedPublisher.Setup(w => w.PublishWorkspaceSemanticTokensRefresh());
            var defaultWorkspaceChangedRefresh = new DefaultWorkspaceSemanticTokensRefreshTrigger(workspaceChangedPublisher.Object);
            defaultWorkspaceChangedRefresh.Initialize(ProjectManager);

            // Act
            ProjectManager.DocumentChanged(HostProject.FilePath, HostDocument.FilePath, new EmptyTextLoader(HostDocument.FilePath));

            // Assert
            workspaceChangedPublisher.VerifyAll();
        }
    }
}
