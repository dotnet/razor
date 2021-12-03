﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Serialization;
using Microsoft.CodeAnalysis.Text;
using Moq;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class DefaultRazorProjectServiceTest : LanguageServerTestBase
    {
        private IReadOnlyList<DocumentSnapshotHandle> EmptyDocuments { get; } = Array.Empty<DocumentSnapshotHandle>();

        [Fact]
        public void UpdateProject_UpdatesProjectWorkspaceState()
        {
            // Arrange
            var projectManager = TestProjectSnapshotManager.Create(LegacyDispatcher);
            var hostProject = new HostProject("C:/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
            projectManager.ProjectAdded(hostProject);
            var projectService = CreateProjectService(Mock.Of<ProjectResolver>(MockBehavior.Strict), projectManager);
            var projectWorkspaceState = new ProjectWorkspaceState(Array.Empty<TagHelperDescriptor>(), LanguageVersion.LatestMajor);

            // Act
            projectService.UpdateProject(hostProject.FilePath, hostProject.Configuration, hostProject.RootNamespace, projectWorkspaceState, EmptyDocuments);

            // Assert
            var project = projectManager.GetLoadedProject(hostProject.FilePath);
            Assert.Same(projectWorkspaceState, project.ProjectWorkspaceState);
        }

        [Fact]
        public void UpdateProject_UpdatingDocument_MapsRelativeFilePathToActualDocument()
        {
            // Arrange
            var projectManager = TestProjectSnapshotManager.Create(LegacyDispatcher);
            var hostProject = new HostProject("C:/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
            projectManager.ProjectAdded(hostProject);
            var hostDocument = new HostDocument("C:/path/to/file.cshtml", "file.cshtml", FileKinds.Legacy);
            projectManager.DocumentAdded(hostProject, hostDocument, Mock.Of<TextLoader>(MockBehavior.Strict));
            var projectService = CreateProjectService(Mock.Of<ProjectResolver>(MockBehavior.Strict), projectManager);
            var newDocument = new DocumentSnapshotHandle("file.cshtml", "file.cshtml", FileKinds.Component);

            // Act
            projectService.UpdateProject(hostProject.FilePath, hostProject.Configuration, hostProject.RootNamespace, ProjectWorkspaceState.Default, new[] { newDocument });

            // Assert
            var project = projectManager.GetLoadedProject(hostProject.FilePath);
            var document = project.GetDocument(hostDocument.FilePath);
            Assert.NotNull(document);
            Assert.Equal(FileKinds.Component, document.FileKind);
        }

        [Fact]
        public void UpdateProject_AddsNewDocuments()
        {
            // Arrange
            var projectManager = TestProjectSnapshotManager.Create(LegacyDispatcher);
            var hostProject = new HostProject("C:/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
            projectManager.ProjectAdded(hostProject);
            var hostDocument = new HostDocument("C:/path/to/file.cshtml", "file.cshtml", FileKinds.Legacy);
            projectManager.DocumentAdded(hostProject, hostDocument, Mock.Of<TextLoader>(MockBehavior.Strict));
            var projectService = CreateProjectService(Mock.Of<ProjectResolver>(MockBehavior.Strict), projectManager);
            var oldDocument = new DocumentSnapshotHandle(hostDocument.FilePath, hostDocument.TargetPath, hostDocument.FileKind);
            var newDocument = new DocumentSnapshotHandle("C:/path/to/file2.cshtml", "file2.cshtml", FileKinds.Legacy);

            // Act
            projectService.UpdateProject(hostProject.FilePath, hostProject.Configuration, hostProject.RootNamespace, ProjectWorkspaceState.Default, new[] { oldDocument, newDocument });

            // Assert
            var project = projectManager.GetLoadedProject(hostProject.FilePath);
            var projectFilePaths = project.DocumentFilePaths.OrderBy(path => path);
            Assert.Equal(projectFilePaths, new[] { oldDocument.FilePath, newDocument.FilePath });
        }

        [Fact]
        public void UpdateProject_MovesExistingDocumentToMisc()
        {
            // Arrange
            var projectManager = TestProjectSnapshotManager.Create(LegacyDispatcher);
            ProjectSnapshot miscProject = TestProjectSnapshot.Create("C:/__MISC_PROJECT__");
            var miscHostProject = new HostProject(miscProject.FilePath, RazorConfiguration.Default, "TestRootNamespace");
            projectManager.ProjectAdded(miscHostProject);
            var hostProject = new HostProject("C:/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
            projectManager.ProjectAdded(hostProject);
            var project = projectManager.GetLoadedProject(hostProject.FilePath);
            var hostDocument = new HostDocument("C:/path/to/file.cshtml", "file.cshtml", FileKinds.Legacy);
            projectManager.DocumentAdded(hostProject, hostDocument, Mock.Of<TextLoader>(MockBehavior.Strict));
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>
                {
                    [hostDocument.FilePath] = project
                },
                miscProject);
            var projectService = CreateProjectService(projectResolver, projectManager);
            var newDocument = new DocumentSnapshotHandle("C:/path/to/file2.cshtml", "file2.cshtml", FileKinds.Legacy);

            // Act
            projectService.UpdateProject(hostProject.FilePath, hostProject.Configuration, hostProject.RootNamespace, ProjectWorkspaceState.Default, new[] { newDocument });

            // Assert
            project = projectManager.GetLoadedProject(hostProject.FilePath);
            Assert.Equal(project.DocumentFilePaths, new[] { newDocument.FilePath });

            miscProject = projectManager.GetLoadedProject(miscProject.FilePath);
            Assert.Equal(miscProject.DocumentFilePaths, new[] { hostDocument.FilePath });
        }

        [Fact]
        public void UpdateProject_KnownDocuments()
        {
            // Arrange
            var projectManager = TestProjectSnapshotManager.Create(LegacyDispatcher);
            var hostProject = new HostProject("/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
            projectManager.ProjectAdded(hostProject);
            var document = new HostDocument("/path/to/file.cshtml", "file.cshtml", FileKinds.Legacy);
            projectManager.DocumentAdded(hostProject, document, Mock.Of<TextLoader>(MockBehavior.Strict));
            var projectService = CreateProjectService(Mock.Of<ProjectResolver>(MockBehavior.Strict), projectManager);
            var newDocument = new DocumentSnapshotHandle(document.FilePath, document.TargetPath, document.FileKind);
            projectManager.AllowNotifyListeners = true;
            projectManager.Changed += (sender, args) =>
            {
                if (args.Kind == ProjectChangeKind.DocumentRemoved ||
                    args.Kind == ProjectChangeKind.DocumentChanged ||
                    args.Kind == ProjectChangeKind.DocumentAdded)
                {
                    throw new XunitException("Should have nooped");
                }
            };

            // Act & Assert
            projectService.UpdateProject(hostProject.FilePath, hostProject.Configuration, hostProject.RootNamespace, ProjectWorkspaceState.Default, new[] { newDocument });
        }

        [Fact]
        public void UpdateProject_UpdatesLegacyDocumentsAsComponents()
        {
            // Arrange
            var projectManager = TestProjectSnapshotManager.Create(LegacyDispatcher);
            var hostProject = new HostProject("C:/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
            projectManager.ProjectAdded(hostProject);
            var legacyDocument = new HostDocument("C:/path/to/file.cshtml", "file.cshtml", FileKinds.Legacy);
            projectManager.DocumentAdded(hostProject, legacyDocument, Mock.Of<TextLoader>(MockBehavior.Strict));
            var projectService = CreateProjectService(Mock.Of<ProjectResolver>(MockBehavior.Strict), projectManager);
            var newDocument = new DocumentSnapshotHandle(legacyDocument.FilePath, legacyDocument.TargetPath, FileKinds.Component);

            // Act
            projectService.UpdateProject(hostProject.FilePath, hostProject.Configuration, hostProject.RootNamespace, ProjectWorkspaceState.Default, new[] { newDocument });

            // Assert
            var project = projectManager.GetLoadedProject(hostProject.FilePath);
            var document = project.GetDocument(newDocument.FilePath);
            Assert.NotNull(document);
            Assert.Equal(FileKinds.Component, document.FileKind);
        }

        [Fact]
        public void UpdateProject_SameConfigurationDifferentRootNamespace_UpdatesRootNamespace()
        {
            // Arrange
            var projectFilePath = "C:/path/to/project.csproj";
            var ownerProject = TestProjectSnapshot.Create(projectFilePath);
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            var expectedRootNamespace = "NewRootNamespace";
            projectSnapshotManager.Setup(manager => manager.GetLoadedProject(projectFilePath))
                .Returns(ownerProject);
            projectSnapshotManager.Setup(manager => manager.ProjectWorkspaceStateChanged(It.IsAny<string>(), It.IsAny<ProjectWorkspaceState>()));
            projectSnapshotManager.Setup(manager => manager.ProjectConfigurationChanged(It.IsAny<HostProject>()))
                .Callback<HostProject>((hostProject) => Assert.Equal(expectedRootNamespace, hostProject.RootNamespace));
            var projectService = CreateProjectService(Mock.Of<ProjectResolver>(MockBehavior.Strict), projectSnapshotManager.Object);

            // Act
            projectService.UpdateProject(projectFilePath, ownerProject.Configuration, expectedRootNamespace, ProjectWorkspaceState.Default, EmptyDocuments);

            // Assert
            projectSnapshotManager.VerifyAll();
        }

        [Fact]
        public void UpdateProject_SameConfigurationAndRootNamespaceNoops()
        {
            // Arrange
            var projectFilePath = "C:/path/to/project.csproj";
            var ownerProject = TestProjectSnapshot.Create(projectFilePath);
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.GetLoadedProject(projectFilePath))
                .Returns(ownerProject);
            projectSnapshotManager.Setup(manager => manager.ProjectWorkspaceStateChanged(It.IsAny<string>(), It.IsAny<ProjectWorkspaceState>()));
            projectSnapshotManager.Setup(manager => manager.ProjectConfigurationChanged(It.IsAny<HostProject>()))
                .Throws(new XunitException("Should not have been called."));
            var projectService = CreateProjectService(Mock.Of<ProjectResolver>(MockBehavior.Strict), projectSnapshotManager.Object);

            // Act & Assert
            projectService.UpdateProject(projectFilePath, ownerProject.Configuration, "TestRootNamespace", ProjectWorkspaceState.Default, EmptyDocuments);
        }

        [Fact]
        public void UpdateProject_NullConfigurationUsesDefault()
        {
            // Arrange
            var projectFilePath = "C:/path/to/project.csproj";
            var ownerProject = TestProjectSnapshot.Create(projectFilePath);
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.GetLoadedProject(projectFilePath))
                .Returns(ownerProject);
            projectSnapshotManager.Setup(manager => manager.ProjectWorkspaceStateChanged(It.IsAny<string>(), It.IsAny<ProjectWorkspaceState>()));
            projectSnapshotManager.Setup(manager => manager.ProjectConfigurationChanged(It.IsAny<HostProject>()))
                .Callback<HostProject>((hostProject) =>
                {
                    Assert.Same(RazorDefaults.Configuration, hostProject.Configuration);
                    Assert.Equal(projectFilePath, hostProject.FilePath);
                });
            var projectService = CreateProjectService(Mock.Of<ProjectResolver>(MockBehavior.Strict), projectSnapshotManager.Object);

            // Act
            projectService.UpdateProject(projectFilePath, configuration: null, "TestRootNamespace", ProjectWorkspaceState.Default, EmptyDocuments);

            // Assert
            projectSnapshotManager.VerifyAll();
        }

        [Fact]
        public void UpdateProject_ChangesProjectToUseProvidedConfiguration()
        {
            // Arrange
            var projectFilePath = "C:/path/to/project.csproj";
            var ownerProject = TestProjectSnapshot.Create(projectFilePath);
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.GetLoadedProject(projectFilePath))
                .Returns(ownerProject);
            projectSnapshotManager.Setup(manager => manager.ProjectWorkspaceStateChanged(It.IsAny<string>(), It.IsAny<ProjectWorkspaceState>()));
            projectSnapshotManager.Setup(manager => manager.ProjectConfigurationChanged(It.IsAny<HostProject>()))
                .Callback<HostProject>((hostProject) =>
                {
                    Assert.Same(FallbackRazorConfiguration.MVC_1_1, hostProject.Configuration);
                    Assert.Equal(projectFilePath, hostProject.FilePath);
                });
            var projectService = CreateProjectService(Mock.Of<ProjectResolver>(MockBehavior.Strict), projectSnapshotManager.Object);

            // Act
            projectService.UpdateProject(projectFilePath, FallbackRazorConfiguration.MVC_1_1, "TestRootNamespace", ProjectWorkspaceState.Default, EmptyDocuments);

            // Assert
            projectSnapshotManager.VerifyAll();
        }

        [Fact]
        public void UpdateProject_UntrackedProjectNoops()
        {
            // Arrange
            var projectFilePath = "C:/path/to/project.csproj";
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.GetLoadedProject(projectFilePath))
                .Returns<ProjectSnapshot>(null);
            projectSnapshotManager.Setup(manager => manager.ProjectConfigurationChanged(It.IsAny<HostProject>()))
                .Throws(new XunitException("Should not have been called."));
            var projectService = CreateProjectService(Mock.Of<ProjectResolver>(MockBehavior.Strict), projectSnapshotManager.Object);

            // Act & Assert
            projectService.UpdateProject(projectFilePath, FallbackRazorConfiguration.MVC_1_1, "TestRootNamespace", ProjectWorkspaceState.Default, EmptyDocuments);
        }

        [Fact]
        public void CloseDocument_ClosesDocumentInOwnerProject()
        {
            // Arrange
            var expectedDocumentFilePath = "C:/path/to/document.cshtml";
            var ownerProject = TestProjectSnapshot.Create("C:/path/to/project.csproj");
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>
                {
                    [expectedDocumentFilePath] = ownerProject
                },
                TestProjectSnapshot.Create("C:/__MISC_PROJECT__"));
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentClosed(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TextLoader>()))
                .Callback<string, string, TextLoader>((projectFilePath, documentFilePath, text) =>
                {
                    Assert.Equal(ownerProject.HostProject.FilePath, projectFilePath);
                    Assert.Equal(expectedDocumentFilePath, documentFilePath);
                    Assert.NotNull(text);
                });
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act
            projectService.CloseDocument(expectedDocumentFilePath);

            // Assert
            projectSnapshotManager.VerifyAll();
        }

        [Fact]
        public void CloseDocument_ClosesDocumentInMiscellaneousProject()
        {
            // Arrange
            var expectedDocumentFilePath = "C:/path/to/document.cshtml";
            var miscellaneousProject = TestProjectSnapshot.Create("C:/__MISC_PROJECT__");
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>(),
                miscellaneousProject);
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentClosed(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TextLoader>()))
                .Callback<string, string, TextLoader>((projectFilePath, documentFilePath, text) =>
                {
                    Assert.Equal(miscellaneousProject.FilePath, projectFilePath);
                    Assert.Equal(expectedDocumentFilePath, documentFilePath);
                    Assert.NotNull(text);
                });
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act
            projectService.CloseDocument(expectedDocumentFilePath);

            // Assert
            projectSnapshotManager.VerifyAll();
        }

        [Fact]
        public void OpenDocument_OpensAlreadyAddedDocumentInOwnerProject()
        {
            // Arrange
            var expectedDocumentFilePath = "C:/path/to/document.cshtml";
            var ownerProject = TestProjectSnapshot.Create("C:/path/to/project.csproj");
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>
                {
                    [expectedDocumentFilePath] = ownerProject
                },
                TestProjectSnapshot.Create("C:/__MISC_PROJECT__"));
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentAdded(It.IsAny<HostProject>(), It.IsAny<HostDocument>(), It.IsAny<TextLoader>()))
                .Throws(new InvalidOperationException("This shouldn't have been called."));
            projectSnapshotManager.Setup(manager => manager.DocumentOpened(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SourceText>()))
                .Callback<string, string, SourceText>((projectFilePath, documentFilePath, text) =>
                {
                    Assert.Equal(ownerProject.HostProject.FilePath, projectFilePath);
                    Assert.Equal(expectedDocumentFilePath, documentFilePath);
                    Assert.NotNull(text);
                });
            var documentSnapshot = Mock.Of<DocumentSnapshot>(s => s.GetGeneratedOutputAsync() == Task.FromResult<RazorCodeDocument>(null), MockBehavior.Strict);
            var documentResolver = Mock.Of<DocumentResolver>(resolver => resolver.TryResolveDocument(It.IsAny<string>(), out documentSnapshot) == true, MockBehavior.Strict);
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object, documentResolver);
            var sourceText = SourceText.From("Hello World");

            // Act
            projectService.OpenDocument(expectedDocumentFilePath, sourceText, 1);

            // Assert
            projectSnapshotManager.Verify(manager => manager.DocumentOpened(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SourceText>()));
        }

        [Fact]
        public void OpenDocument_OpensAlreadyAddedDocumentInMiscellaneousProject()
        {
            // Arrange
            var expectedDocumentFilePath = "C:/path/to/document.cshtml";
            var miscellaneousProject = TestProjectSnapshot.Create("C:/__MISC_PROJECT__");
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>(),
                miscellaneousProject);
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentAdded(It.IsAny<HostProject>(), It.IsAny<HostDocument>(), It.IsAny<TextLoader>()))
                .Throws(new InvalidOperationException("This shouldn't have been called."));
            projectSnapshotManager.Setup(manager => manager.DocumentOpened(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SourceText>()))
                .Callback<string, string, SourceText>((projectFilePath, documentFilePath, text) =>
                {
                    Assert.Equal(miscellaneousProject.FilePath, projectFilePath);
                    Assert.Equal(expectedDocumentFilePath, documentFilePath);
                    Assert.NotNull(text);
                });
            var documentSnapshot = new Mock<DocumentSnapshot>(MockBehavior.Strict).Object;
            Mock.Get(documentSnapshot).Setup(s => s.GetGeneratedOutputAsync()).ReturnsAsync(value: null);
            var documentResolver = Mock.Of<DocumentResolver>(resolver => resolver.TryResolveDocument(It.IsAny<string>(), out documentSnapshot) == true, MockBehavior.Strict);
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object, documentResolver);
            var sourceText = SourceText.From("Hello World");

            // Act
            projectService.OpenDocument(expectedDocumentFilePath, sourceText, 1);

            // Assert
            projectSnapshotManager.Verify(manager => manager.DocumentOpened(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SourceText>()));
        }

        [Fact]
        public void OpenDocument_OpensAndAddsDocumentToOwnerProject()
        {
            // Arrange
            var expectedDocumentFilePath = "C:/path/to/document.cshtml";
            var ownerProject = TestProjectSnapshot.Create("C:/path/to/project.csproj");
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>
                {
                    [expectedDocumentFilePath] = ownerProject
                },
                TestProjectSnapshot.Create("C:/__MISC_PROJECT__"));
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentAdded(It.IsAny<HostProject>(), It.IsAny<HostDocument>(), It.IsAny<TextLoader>()))
                .Callback<HostProject, HostDocument, TextLoader>((hostProject, hostDocument, loader) =>
                {
                    Assert.Same(ownerProject.HostProject, hostProject);
                    Assert.Equal(expectedDocumentFilePath, hostDocument.FilePath);
                    Assert.NotNull(loader);
                });
            projectSnapshotManager.Setup(manager => manager.DocumentOpened(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SourceText>()))
                .Callback<string, string, SourceText>((projectFilePath, documentFilePath, text) =>
                {
                    Assert.Equal(ownerProject.HostProject.FilePath, projectFilePath);
                    Assert.Equal(expectedDocumentFilePath, documentFilePath);
                    Assert.NotNull(text);
                });
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);
            var sourceText = SourceText.From("Hello World");

            // Act
            projectService.OpenDocument(expectedDocumentFilePath, sourceText, 1);

            // Assert
            projectSnapshotManager.VerifyAll();
        }

        [Fact]
        public void AddDocument_NoopsIfDocumentIsAlreadyAdded()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            var project = Mock.Of<ProjectSnapshot>(MockBehavior.Strict);
            var projectResolver = new Mock<ProjectResolver>(MockBehavior.Strict);
            projectResolver.Setup(resolver => resolver.TryResolveProject(It.IsAny<string>(), out project, It.IsAny<bool>()))
                .Throws(new InvalidOperationException("This shouldn't have been called."));
            var alreadyOpenDoc = Mock.Of<DocumentSnapshot>(MockBehavior.Strict);
            var documentResolver = Mock.Of<DocumentResolver>(resolver => resolver.TryResolveDocument(It.IsAny<string>(), out alreadyOpenDoc), MockBehavior.Strict);
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentAdded(It.IsAny<HostProject>(), It.IsAny<HostDocument>(), It.IsAny<TextLoader>()))
                .Throws(new InvalidOperationException("This should not have been called."));
            var projectService = CreateProjectService(projectResolver.Object, projectSnapshotManager.Object, documentResolver);

            // Act & Assert
            projectService.AddDocument(documentFilePath);
        }

        [Fact]
        public void AddDocument_AddsDocumentToOwnerProject()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            var ownerProject = TestProjectSnapshot.Create("C:/path/to/project.csproj");
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>
                {
                    [documentFilePath] = ownerProject
                },
                TestProjectSnapshot.Create("C:/__MISC_PROJECT__"));
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentAdded(It.IsAny<HostProject>(), It.IsAny<HostDocument>(), It.IsAny<TextLoader>()))
                .Callback<HostProject, HostDocument, TextLoader>((hostProject, hostDocument, loader) =>
                {
                    Assert.Same(ownerProject.HostProject, hostProject);
                    Assert.Equal(documentFilePath, hostDocument.FilePath);
                    Assert.NotNull(loader);
                });
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act
            projectService.AddDocument(documentFilePath);

            // Assert
            projectSnapshotManager.VerifyAll();
        }

        [Fact]
        public void AddDocument_AddsDocumentToMiscellaneousProject()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            var miscellaneousProject = TestProjectSnapshot.Create("C:/__MISC_PROJECT__");
            var projectResolver = new TestProjectResolver(new Dictionary<string, ProjectSnapshot>(), miscellaneousProject);
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentAdded(It.IsAny<HostProject>(), It.IsAny<HostDocument>(), It.IsAny<TextLoader>()))
                .Callback<HostProject, HostDocument, TextLoader>((hostProject, hostDocument, loader) =>
                {
                    Assert.Same(miscellaneousProject.HostProject, hostProject);
                    Assert.Equal(documentFilePath, hostDocument.FilePath);
                    Assert.NotNull(loader);
                });
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act
            projectService.AddDocument(documentFilePath);

            // Assert
            projectSnapshotManager.VerifyAll();
        }

        [Fact]
        public void RemoveDocument_RemovesDocumentFromOwnerProject()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            var ownerProject = TestProjectSnapshot.Create("C:/path/to/project.csproj", new[] { documentFilePath });
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>
                {
                    [documentFilePath] = ownerProject
                },
                TestProjectSnapshot.Create("C:/__MISC_PROJECT__"));
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentRemoved(It.IsAny<HostProject>(), It.IsAny<HostDocument>()))
                .Callback<HostProject, HostDocument>((hostProject, hostDocument) =>
                {
                    Assert.Same(ownerProject.HostProject, hostProject);
                    Assert.Equal(documentFilePath, hostDocument.FilePath);
                });
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act
            projectService.RemoveDocument(documentFilePath);

            // Assert
            projectSnapshotManager.VerifyAll();
        }

        [Fact]
        public void RemoveDocument_RemovesDocumentFromMiscellaneousProject()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            var miscellaneousProject = TestProjectSnapshot.Create("C:/__MISC_PROJECT__", new[] { documentFilePath });
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>(),
                miscellaneousProject);
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentRemoved(It.IsAny<HostProject>(), It.IsAny<HostDocument>()))
                .Callback<HostProject, HostDocument>((hostProject, hostDocument) =>
                {
                    Assert.Same(miscellaneousProject.HostProject, hostProject);
                    Assert.Equal(documentFilePath, hostDocument.FilePath);
                });
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act
            projectService.RemoveDocument(documentFilePath);

            // Assert
            projectSnapshotManager.VerifyAll();
        }

        [Fact]
        public void RemoveDocument_NoopsIfOwnerProjectDoesNotContainDocument()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            var ownerProject = TestProjectSnapshot.Create("C:/path/to/project.csproj", Array.Empty<string>());
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>
                {
                    [documentFilePath] = ownerProject
                },
                TestProjectSnapshot.Create("C:/__MISC_PROJECT__"));
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentRemoved(It.IsAny<HostProject>(), It.IsAny<HostDocument>()))
                .Throws(new InvalidOperationException("Should not have been called."));
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act & Assert
            projectService.RemoveDocument(documentFilePath);
        }

        [Fact]
        public void RemoveDocument_NoopsIfMiscellaneousProjectDoesNotContainDocument()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            var miscellaneousProject = TestProjectSnapshot.Create("C:/__MISC_PROJECT__", Array.Empty<string>());
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>(),
                miscellaneousProject);
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentRemoved(It.IsAny<HostProject>(), It.IsAny<HostDocument>()))
                .Throws(new InvalidOperationException("Should not have been called."));
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act & Assert
            projectService.RemoveDocument(documentFilePath);
        }

        [Fact]
        public void UpdateDocument_ChangesDocumentInOwnerProject()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            var ownerProject = TestProjectSnapshot.Create("C:/path/to/project.csproj", new[] { documentFilePath });
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>
                {
                    [documentFilePath] = ownerProject
                },
                TestProjectSnapshot.Create("C:/__MISC_PROJECT__"));
            var newText = SourceText.From("Something New");
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentChanged(ownerProject.FilePath, documentFilePath, newText))
                .Callback<string, string, SourceText>((projectPath, documentPath, sourceText) =>
                {
                    Assert.Equal(ownerProject.FilePath, projectPath);
                    Assert.Equal(documentFilePath, documentPath);
                    Assert.Same(newText, sourceText);
                });
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act
            projectService.UpdateDocument(documentFilePath, newText, 1337);

            // Assert
            projectSnapshotManager.VerifyAll();
        }

        [Fact]
        public void UpdateDocument_ChangesDocumentInMiscProject()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            var miscellaneousProject = TestProjectSnapshot.Create("C:/__MISC_PROJECT__", new[] { documentFilePath });
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>(),
                TestProjectSnapshot.Create("C:/__MISC_PROJECT__"));
            var newText = SourceText.From("Something New");
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentChanged(miscellaneousProject.FilePath, documentFilePath, newText))
                .Callback<string, string, SourceText>((projectPath, documentPath, sourceText) =>
                {
                    Assert.Equal(miscellaneousProject.FilePath, projectPath);
                    Assert.Equal(documentFilePath, documentPath);
                    Assert.Same(newText, sourceText);
                });
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act
            projectService.UpdateDocument(documentFilePath, newText, 1337);

            // Assert
            projectSnapshotManager.VerifyAll();
        }

        [Fact]
        public void UpdateDocument_TracksKnownDocumentVersion()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            var ownerProject = TestProjectSnapshot.Create("C:/path/to/project.csproj", new[] { documentFilePath });
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>
                {
                    [documentFilePath] = ownerProject
                },
                TestProjectSnapshot.Create("C:/__MISC_PROJECT__"));
            DocumentSnapshot documentSnapshot = TestDocumentSnapshot.Create(documentFilePath);
            var documentResolver = Mock.Of<DocumentResolver>(resolver => resolver.TryResolveDocument(It.IsAny<string>(), out documentSnapshot) == true, MockBehavior.Strict);
            var documentVersionCache = new Mock<DocumentVersionCache>(MockBehavior.Strict);
            documentVersionCache.Setup(cache => cache.TrackDocumentVersion(documentSnapshot, It.IsAny<int>()))
                .Callback<DocumentSnapshot, int>((snapshot, version) =>
                {
                    Assert.Same(documentSnapshot, snapshot);
                    Assert.Equal(1337, version);
                });
            var newText = SourceText.From("Something New");
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(m => m.DocumentChanged(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SourceText>())).Verifiable();
            var projectService = CreateProjectService(
                projectResolver,
                projectSnapshotManager.Object,
                documentResolver,
                documentVersionCache.Object);

            // Act
            projectService.UpdateDocument(documentFilePath, newText, 1337);

            // Assert
            documentVersionCache.VerifyAll();
        }

        [Fact]
        public void UpdateDocument_IgnoresUnknownDocumentVersions()
        {
            // Arrange
            var documentFilePath = "C:/path/to/document.cshtml";
            var ownerProject = TestProjectSnapshot.Create("C:/path/to/project.csproj", new[] { documentFilePath });
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>
                {
                    [documentFilePath] = ownerProject
                },
                TestProjectSnapshot.Create("C:/__MISC_PROJECT__"));
            var documentVersionCache = new Mock<DocumentVersionCache>(MockBehavior.Strict);
            documentVersionCache.Setup(cache => cache.TrackDocumentVersion(It.IsAny<DocumentSnapshot>(), It.IsAny<int>()))
                .Throws<XunitException>();
            var newText = SourceText.From("Something New");
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(m => m.DocumentChanged(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SourceText>())).Verifiable();
            var projectService = CreateProjectService(
                projectResolver,
                projectSnapshotManager.Object,
                documentVersionCache: documentVersionCache.Object);

            // Act & Assert
            projectService.UpdateDocument(documentFilePath, newText, 1337);
        }

        [Fact]
        public void AddProject_AddsProjectWithDefaultConfiguration()
        {
            // Arrange
            var projectFilePath = "C:/path/to/project.csproj";
            var miscellaneousProject = TestProjectSnapshot.Create("/./__MISC_PROJECT__");
            var projectResolver = new TestProjectResolver(new Dictionary<string, ProjectSnapshot>(), miscellaneousProject);
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.ProjectAdded(It.IsAny<HostProject>()))
                .Callback<HostProject>((hostProject) =>
                {
                    Assert.Equal(projectFilePath, hostProject.FilePath);
                    Assert.Same(RazorDefaults.Configuration, hostProject.Configuration);
                });
            projectSnapshotManager.Setup(manager => manager.GetLoadedProject(It.IsAny<string>())).Returns<ProjectSnapshot>(null);
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act
            projectService.AddProject(projectFilePath);

            // Assert
            projectSnapshotManager.VerifyAll();
        }

        [Fact]
        public void AddProject_AlreadyAdded_Noops()
        {
            // Arrange
            var projectFilePath = "C:/path/to/project.csproj";
            var miscellaneousProject = TestProjectSnapshot.Create("C:/__MISC_PROJECT__");
            var projectResolver = new TestProjectResolver(new Dictionary<string, ProjectSnapshot>(), miscellaneousProject);
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.GetLoadedProject(projectFilePath))
                .Returns(Mock.Of<ProjectSnapshot>(MockBehavior.Strict));
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act
            projectService.AddProject(projectFilePath);

            // Assert
            projectSnapshotManager.VerifyAll();
        }

        [Fact]
        public void RemoveProject_RemovesProject()
        {
            // Arrange
            var projectFilePath = "C:/path/to/project.csproj";
            var ownerProject = TestProjectSnapshot.Create(projectFilePath);
            var miscellaneousProject = TestProjectSnapshot.Create("C:/__MISC_PROJECT__");
            var projectResolver = new TestProjectResolver(new Dictionary<string, ProjectSnapshot>(), miscellaneousProject);
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.GetLoadedProject(projectFilePath))
                .Returns(ownerProject);
            projectSnapshotManager.Setup(manager => manager.ProjectRemoved(ownerProject.HostProject))
                .Callback<HostProject>((hostProject) => Assert.Equal(projectFilePath, hostProject.FilePath));
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act
            projectService.RemoveProject(projectFilePath);

            // Assert
            projectSnapshotManager.VerifyAll();
        }

        [Fact]
        public void RemoveProject_NoopsIfProjectIsNotLoaded()
        {
            // Arrange
            var projectFilePath = "C:/path/to/project.csproj";
            var miscellaneousProject = TestProjectSnapshot.Create("C:/__MISC_PROJECT__");
            var projectResolver = new TestProjectResolver(new Dictionary<string, ProjectSnapshot>(), miscellaneousProject);
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.ProjectRemoved(It.IsAny<HostProject>()))
                .Throws(new InvalidOperationException("Should not have been called."));
            projectSnapshotManager.Setup(manager => manager.GetLoadedProject(projectFilePath))
                .Returns((ProjectSnapshot)null);
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act & Assert
            projectService.RemoveProject(projectFilePath);
        }

        [Fact]
        public void TryMigrateDocumentsFromRemovedProject_MigratesDocumentsToNonMiscProject()
        {
            // Arrange
            var documentFilePath1 = "C:/path/to/some/document1.cshtml";
            var documentFilePath2 = "C:/path/to/some/document2.cshtml";
            var miscellaneousProject = TestProjectSnapshot.Create("C:/__MISC_PROJECT__");
            var removedProject = TestProjectSnapshot.Create("C:/path/to/some/project.csproj", new[] { documentFilePath1, documentFilePath2 });
            var projectToBeMigratedTo = TestProjectSnapshot.Create("C:/path/to/project.csproj");
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>
                {
                    [documentFilePath1] = projectToBeMigratedTo,
                    [documentFilePath2] = projectToBeMigratedTo,
                },
                miscellaneousProject);
            var migratedDocuments = new List<HostDocument>();
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentAdded(It.IsAny<HostProject>(), It.IsAny<HostDocument>(), It.IsAny<TextLoader>()))
                .Callback<HostProject, HostDocument, TextLoader>((hostProject, hostDocument, textLoader) =>
                {
                    Assert.Same(projectToBeMigratedTo.HostProject, hostProject);
                    Assert.NotNull(textLoader);

                    migratedDocuments.Add(hostDocument);
                });
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act
            projectService.TryMigrateDocumentsFromRemovedProject(removedProject);

            // Assert
            Assert.Collection(migratedDocuments.OrderBy(doc => doc.FilePath),
                document => Assert.Equal(documentFilePath1, document.FilePath),
                document => Assert.Equal(documentFilePath2, document.FilePath));
        }

        [Fact]
        public void TryMigrateDocumentsFromRemovedProject_MigratesDocumentsToMiscProject()
        {
            // Arrange
            var documentFilePath1 = "C:/path/to/some/document1.cshtml";
            var documentFilePath2 = "C:/path/to/some/document2.cshtml";
            var miscellaneousProject = TestProjectSnapshot.Create("C:/__MISC_PROJECT__");
            var removedProject = TestProjectSnapshot.Create("C:/path/to/some/project.csproj", new[] { documentFilePath1, documentFilePath2 });
            var projectResolver = new TestProjectResolver(new Dictionary<string, ProjectSnapshot>(), miscellaneousProject);
            var migratedDocuments = new List<HostDocument>();
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentAdded(It.IsAny<HostProject>(), It.IsAny<HostDocument>(), It.IsAny<TextLoader>()))
                .Callback<HostProject, HostDocument, TextLoader>((hostProject, hostDocument, textLoader) =>
                {
                    Assert.Same(miscellaneousProject.HostProject, hostProject);
                    Assert.NotNull(textLoader);

                    migratedDocuments.Add(hostDocument);
                });
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act
            projectService.TryMigrateDocumentsFromRemovedProject(removedProject);

            // Assert
            Assert.Collection(migratedDocuments.OrderBy(doc => doc.FilePath),
                document => Assert.Equal(documentFilePath1, document.FilePath),
                document => Assert.Equal(documentFilePath2, document.FilePath));
        }

        [Fact]
        public void TryMigrateMiscellaneousDocumentsToProject_DoesNotMigrateDocumentsIfNoOwnerProject()
        {
            // Arrange
            var documentFilePath1 = "C:/path/to/document1.cshtml";
            var documentFilePath2 = "C:/path/to/document2.cshtml";
            var miscellaneousProject = TestProjectSnapshot.Create("C:/__MISC_PROJECT__", new[] { documentFilePath1, documentFilePath2 });
            var projectResolver = new TestProjectResolver(new Dictionary<string, ProjectSnapshot>(), miscellaneousProject);
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentAdded(It.IsAny<HostProject>(), It.IsAny<HostDocument>(), It.IsAny<TextLoader>()))
                .Throws(new InvalidOperationException("Should not have been called."));
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act & Assert
            projectService.TryMigrateMiscellaneousDocumentsToProject();
        }

        [Fact]
        public void TryMigrateMiscellaneousDocumentsToProject_MigratesDocumentsToNewOwnerProject()
        {
            // Arrange
            var documentFilePath1 = "C:/path/to/document1.cshtml";
            var documentFilePath2 = "C:/path/to/document2.cshtml";
            var miscellaneousProject = TestProjectSnapshot.Create("C:/__MISC_PROJECT__", new[] { documentFilePath1, documentFilePath2 });
            var projectToBeMigratedTo = TestProjectSnapshot.Create("C:/path/to/project.csproj");
            var projectResolver = new TestProjectResolver(
                new Dictionary<string, ProjectSnapshot>
                {
                    [documentFilePath1] = projectToBeMigratedTo,
                    [documentFilePath2] = projectToBeMigratedTo,
                },
                miscellaneousProject);
            var migratedDocuments = new List<HostDocument>();
            var projectSnapshotManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectSnapshotManager.Setup(manager => manager.DocumentAdded(It.IsAny<HostProject>(), It.IsAny<HostDocument>(), It.IsAny<TextLoader>()))
                .Callback<HostProject, HostDocument, TextLoader>((hostProject, hostDocument, textLoader) =>
                {
                    Assert.Same(projectToBeMigratedTo.HostProject, hostProject);
                    Assert.NotNull(textLoader);

                    migratedDocuments.Add(hostDocument);
                });
            projectSnapshotManager.Setup(manager => manager.DocumentRemoved(It.IsAny<HostProject>(), It.IsAny<HostDocument>()))
                .Callback<HostProject, HostDocument>((hostProject, hostDocument) =>
                {
                    Assert.Same(miscellaneousProject.HostProject, hostProject);

                    Assert.DoesNotContain(hostDocument, migratedDocuments);
                });
            var projectService = CreateProjectService(projectResolver, projectSnapshotManager.Object);

            // Act
            projectService.TryMigrateMiscellaneousDocumentsToProject();

            // Assert
            Assert.Collection(migratedDocuments.OrderBy(doc => doc.FilePath),
                document => Assert.Equal(documentFilePath1, document.FilePath),
                document => Assert.Equal(documentFilePath2, document.FilePath));
        }

        private DefaultRazorProjectService CreateProjectService(
            ProjectResolver projectResolver,
            ProjectSnapshotManagerBase projectSnapshotManager,
            DocumentResolver documentResolver = null,
            DocumentVersionCache documentVersionCache = null)
        {
            if (documentVersionCache is null)
            {
                documentVersionCache = new Mock<DocumentVersionCache>(MockBehavior.Strict).Object;
                Mock.Get(documentVersionCache).Setup(c => c.TrackDocumentVersion(It.IsAny<DocumentSnapshot>(), It.IsAny<int>())).Verifiable();
            }

            var filePathNormalizer = new FilePathNormalizer();
            var accessor = Mock.Of<ProjectSnapshotManagerAccessor>(a => a.Instance == projectSnapshotManager, MockBehavior.Strict);
            if (documentResolver is null)
            {
                documentResolver = new Mock<DocumentResolver>(MockBehavior.Strict).Object;
                Mock.Get(documentResolver).Setup(r => r.TryResolveDocument(It.IsAny<string>(), out It.Ref<DocumentSnapshot>.IsAny)).Returns(false);
            }

            var hostDocumentFactory = new TestHostDocumentFactory();
            var remoteTextLoaderFactory = Mock.Of<RemoteTextLoaderFactory>(factory => factory.Create(It.IsAny<string>()) == Mock.Of<TextLoader>(MockBehavior.Strict), MockBehavior.Strict);
            var projectService = new DefaultRazorProjectService(
                LegacyDispatcher,
                hostDocumentFactory,
                remoteTextLoaderFactory,
                documentResolver,
                projectResolver,
                documentVersionCache,
                filePathNormalizer,
                accessor,
                LoggerFactory);

            return projectService;
        }

        private class TestProjectResolver : ProjectResolver
        {
            private readonly IReadOnlyDictionary<string, ProjectSnapshot> _projectMappings;
            private readonly ProjectSnapshot _miscellaneousProject;

            public TestProjectResolver(IReadOnlyDictionary<string, ProjectSnapshot> projectMappings, ProjectSnapshot miscellaneousProject)
            {
                _projectMappings = projectMappings;
                _miscellaneousProject = miscellaneousProject;
            }

            public override ProjectSnapshot GetMiscellaneousProject() => _miscellaneousProject;

            public override bool TryResolveProject(string documentFilePath, out ProjectSnapshot projectSnapshot, bool enforceDocumentInProject = true)
            {
                return _projectMappings.TryGetValue(documentFilePath, out projectSnapshot);
            }
        }

        private class TestHostDocumentFactory : HostDocumentFactory
        {
            public override HostDocument Create(string filePath, string targetFilePath) => Create(filePath, targetFilePath, fileKind: null);

            public override HostDocument Create(string filePath, string targetFilePath, string fileKind)
            {
                return new HostDocument(filePath, targetFilePath, fileKind);
            }
        }
    }
}
