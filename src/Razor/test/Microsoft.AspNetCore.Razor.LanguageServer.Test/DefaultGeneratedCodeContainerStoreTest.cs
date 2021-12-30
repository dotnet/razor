﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class DefaultGeneratedCodeContainerStoreTest : LanguageServerTestBase
    {
        public DefaultGeneratedCodeContainerStoreTest()
        {
            var documentVersionCache = Mock.Of<DocumentVersionCache>(MockBehavior.Strict);
            var csharpPublisher = Mock.Of<GeneratedDocumentPublisher>(MockBehavior.Strict);
            Store = new DefaultGeneratedDocumentContainerStore(LegacyDispatcher, documentVersionCache, csharpPublisher);
            ProjectManager = TestProjectSnapshotManager.Create(LegacyDispatcher);
            Store.Initialize(ProjectManager);
        }

        private DefaultGeneratedDocumentContainerStore Store { get; }

        private TestProjectSnapshotManager ProjectManager { get; }

        [Fact]
        public void Get_CachesValueBasedOnPath()
        {
            // Arrange
            var filePath = "C:/path/to/file.cshtml";

            // Act
            var container1 = Store.Get(filePath);
            var container2 = Store.Get(filePath);
            var otherContainer = Store.Get("C:/path/to/other/file.cshtml");

            // Assert
            Assert.Same(container1, container2);
            Assert.NotSame(container1, otherContainer);
        }

        [Fact]
        public void ProjectSnapshotManager_Changed_DocumentRemoved_EvictsCache()
        {
            // Arrange
            var documentFilePath = "C:/path/to/file.cshtml";
            var projectFilePath = "C:/path/to/project.csproj";
            var container = Store.Get(documentFilePath);
            var oldProject = TestProjectSnapshot.Create(projectFilePath, new[] { documentFilePath });
            var newProjet = TestProjectSnapshot.Create(projectFilePath);
            var args = new ProjectChangeEventArgs(oldProject, newProjet, documentFilePath, ProjectChangeKind.DocumentRemoved, false);

            // Act
            Store.ProjectSnapshotManager_Changed(null, args);
            var newContainer = Store.Get(documentFilePath);

            // Assert
            Assert.NotSame(container, newContainer);
        }

        [Fact]
        public void ProjectSnapshotManager_Changed_DocumentChanged_ClosedDoc_EvictsCache()
        {
            // Arrange
            var documentFilePath = "C:/path/to/file.cshtml";
            var projectFilePath = "C:/path/to/project.csproj";
            var container = Store.Get(documentFilePath);
            var oldProject = TestProjectSnapshot.Create(projectFilePath, new[] { documentFilePath });
            var newProjet = TestProjectSnapshot.Create(projectFilePath, new[] { documentFilePath });
            var args = new ProjectChangeEventArgs(oldProject, newProjet, documentFilePath, ProjectChangeKind.DocumentChanged, false);

            // Act
            Store.ProjectSnapshotManager_Changed(null, args);
            var newContainer = Store.Get(documentFilePath);

            // Assert
            Assert.NotSame(container, newContainer);
        }

        [Fact]
        public void ProjectSnapshotManager_Changed_DocumentChanged_OpenDoc_DoesNotEvictCache()
        {
            // Arrange
            var documentFilePath = "C:/path/to/file.cshtml";
            var projectFilePath = "C:/path/to/project.csproj";
            var container = Store.Get(documentFilePath);
            var project = new HostProject(projectFilePath, RazorConfiguration.Default, "TestRootNamespace");
            ProjectManager.ProjectAdded(project);
            var document = new HostDocument(documentFilePath, "file.cshtml");
            ProjectManager.DocumentAdded(project, document, null);
            ProjectManager.DocumentOpened(projectFilePath, documentFilePath, SourceText.From(string.Empty));
            var oldProject = TestProjectSnapshot.Create(projectFilePath, new[] { documentFilePath });
            var newProjet = TestProjectSnapshot.Create(projectFilePath, new[] { documentFilePath });
            var args = new ProjectChangeEventArgs(oldProject, newProjet, documentFilePath, ProjectChangeKind.DocumentChanged, false);

            // Act
            Store.ProjectSnapshotManager_Changed(null, args);
            var newContainer = Store.Get(documentFilePath);

            // Assert
            Assert.Same(container, newContainer);
        }
    }
}
