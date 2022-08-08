// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Moq;
using Xunit;
using Xunit.Sdk;
using static Microsoft.CodeAnalysis.Razor.Workspaces.DefaultRazorDynamicFileInfoProvider;

namespace Microsoft.VisualStudio.LanguageServices.Razor.ProjectSystem
{
    public class DefaultRazorDynamicFileInfoProviderTest : WorkspaceTestBase
    {
        public DefaultRazorDynamicFileInfoProviderTest()
        {
            DocumentServiceFactory = new DefaultRazorDocumentServiceProviderFactory();
            EditorFeatureDetector = Mock.Of<LSPEditorFeatureDetector>(MockBehavior.Strict);
            ProjectSnapshotManager = new TestProjectSnapshotManager(Workspace)
            {
                AllowNotifyListeners = true
            };
            var hostProject = new HostProject("C:\\project.csproj", RazorConfiguration.Default, rootNamespace: "TestNamespace");
            ProjectSnapshotManager.ProjectAdded(hostProject);
            var hostDocument1 = new HostDocument("C:\\document1.razor", "document1.razor", FileKinds.Component);
            ProjectSnapshotManager.DocumentAdded(hostProject, hostDocument1, new EmptyTextLoader(hostDocument1.FilePath));
            var hostDocument2 = new HostDocument("C:\\document2.razor", "document2.razor", FileKinds.Component);
            ProjectSnapshotManager.DocumentAdded(hostProject, hostDocument2, new EmptyTextLoader(hostDocument2.FilePath));
            Project = ProjectSnapshotManager.GetSnapshot(hostProject);
            Document1 = (DefaultDocumentSnapshot)Project.GetDocument(hostDocument1.FilePath);
            Document2 = (DefaultDocumentSnapshot)Project.GetDocument(hostDocument2.FilePath);

            Provider = new DefaultRazorDynamicFileInfoProvider(DocumentServiceFactory, EditorFeatureDetector, TestLanguageServerFeatureOptions.Instance);
            TestAccessor = Provider.GetTestAccessor();
            Provider.Initialize(ProjectSnapshotManager);

            var lspDocumentContainer = new Mock<DynamicDocumentContainer>(MockBehavior.Strict);
            lspDocumentContainer.SetupSet(c => c.SupportsDiagnostics = true).Verifiable();
            lspDocumentContainer.Setup(container => container.GetTextLoader(It.IsAny<string>())).Returns(new EmptyTextLoader(string.Empty));
            LSPDocumentContainer = lspDocumentContainer.Object;
        }

        private DefaultRazorDynamicFileInfoProvider Provider { get; }

        private TestAccessor TestAccessor { get; }

        private RazorDocumentServiceProviderFactory DocumentServiceFactory { get; }
        private LSPEditorFeatureDetector EditorFeatureDetector { get; }

        private TestProjectSnapshotManager ProjectSnapshotManager { get; }

        private DefaultProjectSnapshot Project { get; }

        private DefaultDocumentSnapshot Document1 { get; }

        private DefaultDocumentSnapshot Document2 { get; }

        private DynamicDocumentContainer LSPDocumentContainer { get; }

        [Fact]
        public void UpdateLSPFileInfo_UnknownFile_Noops()
        {
            // Arrange
            Provider.Updated += (sender, args) => throw new XunitException("Should not have been called.");

            // Act & Assert
            var documentContainer = new Mock<DynamicDocumentContainer>(MockBehavior.Strict);
            documentContainer.SetupSet(c => c.SupportsDiagnostics = true).Verifiable();
            Provider.UpdateLSPFileInfo(new Uri("C:/this/does/not/exist.razor"), documentContainer.Object);
        }

        [Fact]
        public async Task UpdateLSPFileInfo_Updates()
        {
            // Arrange
            await TestAccessor.GetDynamicFileInfoAsync(Project.FilePath, Document1.FilePath, CancellationToken.None).ConfigureAwait(false);
            var called = false;
            Provider.Updated += (sender, args) => called = true;

            // Act
            Provider.UpdateLSPFileInfo(new Uri(Document1.FilePath), LSPDocumentContainer);

            // Assert
            Assert.True(called);
        }

        [Fact]
        public async Task UpdateLSPFileInfo_ProjectRemoved_Noops()
        {
            // Arrange
            await TestAccessor.GetDynamicFileInfoAsync(Project.FilePath, Document1.FilePath, CancellationToken.None).ConfigureAwait(false);
            var called = false;
            Provider.Updated += (sender, args) => called = true;
            ProjectSnapshotManager.ProjectRemoved(Project.HostProject);

            // Act
            Provider.UpdateLSPFileInfo(new Uri(Document1.FilePath), LSPDocumentContainer);

            // Assert
            Assert.False(called);
        }

        [Fact]
        public async Task UpdateLSPFileInfo_SolutionClosing_ClearsAllDocuments()
        {
            // Arrange
            await TestAccessor.GetDynamicFileInfoAsync(Project.FilePath, Document1.FilePath, CancellationToken.None).ConfigureAwait(false);
            await TestAccessor.GetDynamicFileInfoAsync(Project.FilePath, Document2.FilePath, CancellationToken.None).ConfigureAwait(false);
            Provider.Updated += (sender, documentFilePath) => throw new InvalidOperationException("Should not have been called!");

            ProjectSnapshotManager.SolutionClosed();
            ProjectSnapshotManager.DocumentClosed(Project.FilePath, Document1.FilePath, new EmptyTextLoader(string.Empty));

            // Act & Assert
            Provider.UpdateLSPFileInfo(new Uri(Document2.FilePath), LSPDocumentContainer);
            Provider.UpdateLSPFileInfo(new Uri(Document1.FilePath), LSPDocumentContainer);
        }
    }
}
