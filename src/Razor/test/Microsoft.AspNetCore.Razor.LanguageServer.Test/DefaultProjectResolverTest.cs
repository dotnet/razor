﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class DocumentProjectResolverTest : LanguageServerTestBase
    {
        public DocumentProjectResolverTest(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        [Fact]
        public void TryResolveProject_NoProjects_ReturnsFalse()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            var projectResolver = CreateProjectResolver(() => Array.Empty<ProjectSnapshot>());

            // Act
            var result = projectResolver.TryResolveProject(documentFilePath, out var project);

            // Assert
            Assert.False(result);
            Assert.Null(project);
        }

        [Fact]
        public void TryResolveProject_OnlyMiscellaneousProjectDoesNotContainDocument_ReturnsFalse()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            DefaultProjectResolver projectResolver = null;
            var miscProject = new Mock<ProjectSnapshot>(MockBehavior.Strict);
            miscProject.Setup(p => p.FilePath)
                .Returns(() => projectResolver.MiscellaneousHostProject.FilePath);
            miscProject.Setup(p => p.GetDocument(documentFilePath))
                .Returns((DocumentSnapshot)null);
            projectResolver = CreateProjectResolver(() => new[] { miscProject.Object });

            // Act
            var result = projectResolver.TryResolveProject(documentFilePath, out var project);

            // Assert
            Assert.False(result);
            Assert.Null(project);
        }

        [Fact]
        public void TryResolveProject_OnlyMiscellaneousProjectContainsDocument_ReturnsTrue()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            DefaultProjectResolver projectResolver = null;
            var miscProject = new Mock<ProjectSnapshot>(MockBehavior.Strict);
            miscProject.Setup(p => p.FilePath)
                .Returns(() => projectResolver.MiscellaneousHostProject.FilePath);
            miscProject.Setup(p => p.GetDocument(documentFilePath)).Returns(Mock.Of<DocumentSnapshot>(MockBehavior.Strict));
            projectResolver = CreateProjectResolver(() => new[] { miscProject.Object });

            // Act
            var result = projectResolver.TryResolveProject(documentFilePath, out var project);

            // Assert
            Assert.True(result);
            Assert.Equal(miscProject.Object, project);
        }

        [Fact]
        public void TryResolveProject_UnrelatedProject_ReturnsFalse()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            var unrelatedProject = Mock.Of<ProjectSnapshot>(p => p.FilePath == "C:/other/path/to/project.csproj", MockBehavior.Strict);
            var projectResolver = CreateProjectResolver(() => new[] { unrelatedProject });

            // Act
            var result = projectResolver.TryResolveProject(documentFilePath, out var project);

            // Assert
            Assert.False(result);
            Assert.Null(project);
        }

        [Fact]
        public void TryResolveProject_OwnerProjectWithOthers_ReturnsTrue()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            var unrelatedProject = Mock.Of<ProjectSnapshot>(p => p.FilePath == "C:/other/path/to/project.csproj", MockBehavior.Strict);
            var ownerProject = Mock.Of<ProjectSnapshot>(
                p => p.FilePath == "C:/path/to/project.csproj" &&
                p.GetDocument(documentFilePath) == Mock.Of<DocumentSnapshot>(MockBehavior.Strict), MockBehavior.Strict);

            var projectResolver = CreateProjectResolver(() => new[] { unrelatedProject, ownerProject });

            // Act
            var result = projectResolver.TryResolveProject(documentFilePath, out var project);

            // Assert
            Assert.True(result);
            Assert.Same(ownerProject, project);
        }

        [Fact]
        public void TryResolveProject_MiscellaneousOwnerProjectWithOthers_EnforceDocumentTrue_ReturnsTrue()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            DefaultProjectResolver projectResolver = null;
            var miscProject = new Mock<ProjectSnapshot>(MockBehavior.Strict);
            miscProject.Setup(p => p.FilePath)
                .Returns(() => projectResolver.MiscellaneousHostProject.FilePath);
            miscProject.Setup(p => p.GetDocument(documentFilePath)).Returns(Mock.Of<DocumentSnapshot>(MockBehavior.Strict));
            var ownerProject = Mock.Of<ProjectSnapshot>(
                p => p.FilePath == "C:/path/to/project.csproj" &&
                p.GetDocument(documentFilePath) == null, MockBehavior.Strict);

            projectResolver = CreateProjectResolver(() => new[] { miscProject.Object, ownerProject });

            // Act
            var result = projectResolver.TryResolveProject(documentFilePath, out var project, enforceDocumentInProject: true);

            // Assert
            Assert.True(result);
            Assert.Same(miscProject.Object, project);
        }

        [Fact]
        public void TryResolveProject_MiscellaneousOwnerProjectWithOthers_EnforceDocumentFalse_ReturnsTrue()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            DefaultProjectResolver projectResolver = null;
            var miscProject = new Mock<ProjectSnapshot>(MockBehavior.Strict);
            miscProject.Setup(p => p.FilePath)
                .Returns(() => projectResolver.MiscellaneousHostProject.FilePath);
            miscProject.Setup(p => p.GetDocument(documentFilePath)).Returns(Mock.Of<DocumentSnapshot>(MockBehavior.Strict));
            var ownerProject = Mock.Of<ProjectSnapshot>(
                p => p.FilePath == "C:/path/to/project.csproj" &&
                p.GetDocument(documentFilePath) == null, MockBehavior.Strict);

            projectResolver = CreateProjectResolver(() => new[] { miscProject.Object, ownerProject });

            // Act
            var result = projectResolver.TryResolveProject(documentFilePath, out var project, enforceDocumentInProject: false);

            // Assert
            Assert.True(result);
            Assert.Same(ownerProject, project);
        }

        [OSSkipConditionFact(new[] { "OSX", "Linux" })]
        public void TryResolveProject_OwnerProjectDifferentCasing_ReturnsTrue()
        {
            // Arrange
            var documentFilePath = "c:/path/to/document.cshtml";
            var ownerProject = Mock.Of<ProjectSnapshot>(
                p => p.FilePath == "C:/Path/To/project.csproj" &&
                p.GetDocument(documentFilePath) == Mock.Of<DocumentSnapshot>(MockBehavior.Strict), MockBehavior.Strict);
            var projectResolver = CreateProjectResolver(() => new[] { ownerProject });

            // Act
            var result = projectResolver.TryResolveProject(documentFilePath, out var project);

            // Assert
            Assert.True(result);
            Assert.Same(ownerProject, project);
        }

        [Fact]
        public void GetMiscellaneousProject_ProjectLoaded_ReturnsExistingProject()
        {
            // Arrange
            DefaultProjectResolver projectResolver = null;
            var miscProject = new Mock<ProjectSnapshot>(MockBehavior.Strict);
            miscProject.Setup(p => p.FilePath)
                .Returns(() => projectResolver.MiscellaneousHostProject.FilePath);
            var expectedProject = miscProject.Object;
            projectResolver = CreateProjectResolver(() => new[] { expectedProject });

            // Act
            var project = projectResolver.GetMiscellaneousProject();

            // Assert
            Assert.Same(expectedProject, project);
        }

        [Fact]
        public void GetMiscellaneousProject_ProjectNotLoaded_CreatesProjectAndReturnsCreatedProject()
        {
            // Arrange
            DefaultProjectResolver projectResolver = null;
            var projects = new List<ProjectSnapshot>();
            var filePathNormalizer = new FilePathNormalizer();
            var snapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            snapshotManager.Setup(manager => manager.Projects)
                .Returns(() => projects);
            snapshotManager.Setup(manager => manager.GetLoadedProject(It.IsAny<string>()))
                .Returns<string>(filePath => projects.FirstOrDefault(p => p.FilePath == filePath));
            snapshotManager.Setup(manager => manager.ProjectAdded(It.IsAny<HostProject>()))
                .Callback<HostProject>(hostProject => projects.Add(Mock.Of<ProjectSnapshot>(p => p.FilePath == hostProject.FilePath, MockBehavior.Strict)));
            var snapshotManagerAccessor = Mock.Of<ProjectSnapshotManagerAccessor>(accessor => accessor.Instance == snapshotManager.Object, MockBehavior.Strict);
            projectResolver = new DefaultProjectResolver(LegacyDispatcher, filePathNormalizer, snapshotManagerAccessor);

            // Act
            var project = projectResolver.GetMiscellaneousProject();

            // Assert
            Assert.Single(projects);
            Assert.Equal(projectResolver.MiscellaneousHostProject.FilePath, project.FilePath);
        }

        private DefaultProjectResolver CreateProjectResolver(Func<ProjectSnapshot[]> projectFactory)
        {
            var filePathNormalizer = new FilePathNormalizer();
            var snapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            snapshotManager.Setup(manager => manager.Projects)
                .Returns(projectFactory);
            snapshotManager.Setup(manager => manager.GetLoadedProject(It.IsAny<string>()))
                .Returns<string>(filePath => projectFactory().FirstOrDefault(project => project.FilePath == filePath));
            var snapshotManagerAccessor = Mock.Of<ProjectSnapshotManagerAccessor>(accessor => accessor.Instance == snapshotManager.Object, MockBehavior.Strict);
            var projectResolver = new DefaultProjectResolver(LegacyDispatcher, filePathNormalizer, snapshotManagerAccessor);

            return projectResolver;
        }
    }
}
