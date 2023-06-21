// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Serialization;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

public class DefaultRazorProjectServiceTest : LanguageServerTestBase
{
    private readonly IReadOnlyList<DocumentSnapshotHandle> _emptyDocuments;

    public DefaultRazorProjectServiceTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _emptyDocuments = Array.Empty<DocumentSnapshotHandle>();
    }

    [Fact]
    public void UpdateProject_UpdatesProjectWorkspaceState()
    {
        // Arrange
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var hostProject = new HostProject("C:/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
        projectManager.ProjectAdded(hostProject);
        var projectService = CreateProjectService(projectManager);
        var projectWorkspaceState = new ProjectWorkspaceState(ImmutableArray<TagHelperDescriptor>.Empty, LanguageVersion.LatestMajor);

        // Act
        projectService.UpdateProject(hostProject.FilePath, hostProject.Configuration, hostProject.RootNamespace, projectWorkspaceState, _emptyDocuments);

        // Assert
        var project = projectManager.GetLoadedProject(hostProject.FilePath);
        Assert.Same(projectWorkspaceState, project.ProjectWorkspaceState);
    }

    [Fact]
    public void UpdateProject_UpdatingDocument_MapsRelativeFilePathToActualDocument()
    {
        // Arrange
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var hostProject = new HostProject("C:/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
        projectManager.ProjectAdded(hostProject);
        var hostDocument = new HostDocument("C:/path/to/file.cshtml", "file.cshtml", FileKinds.Legacy);
        projectManager.DocumentAdded(hostProject, hostDocument, Mock.Of<TextLoader>(MockBehavior.Strict));
        var projectService = CreateProjectService(projectManager);
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
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var hostProject = new HostProject("C:/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
        projectManager.ProjectAdded(hostProject);
        var hostDocument = new HostDocument("C:/path/to/file.cshtml", "file.cshtml", FileKinds.Legacy);
        projectManager.DocumentAdded(hostProject, hostDocument, Mock.Of<TextLoader>(MockBehavior.Strict));
        var projectService = CreateProjectService(projectManager);
        var oldDocument = new DocumentSnapshotHandle(hostDocument.FilePath, hostDocument.TargetPath, hostDocument.FileKind);
        var newDocument = new DocumentSnapshotHandle("C:/path/to/file2.cshtml", "file2.cshtml", FileKinds.Legacy);

        // Act
        projectService.UpdateProject(hostProject.FilePath, hostProject.Configuration, hostProject.RootNamespace, ProjectWorkspaceState.Default, new[] { oldDocument, newDocument });

        // Assert
        var project = projectManager.GetLoadedProject(hostProject.FilePath);
        var projectFilePaths = project.DocumentFilePaths.OrderBy(path => path);
        Assert.Equal(new[] { oldDocument.FilePath, newDocument.FilePath }, projectFilePaths);
    }

    [Fact]
    public void UpdateProject_MovesDocumentsFromMisc()
    {
        // Arrange
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var hostDocument = new HostDocument("C:/path/to/file.cshtml", "file.cshtml", FileKinds.Legacy);
        var hostProject = new HostProject("C:/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
        projectManager.ProjectAdded(hostProject);
        var project = projectManager.GetLoadedProject(hostProject.FilePath);
        var projectService = CreateProjectService(projectManager, out var snapshotResolver);
        var addedDocument = new DocumentSnapshotHandle("C:/path/to/file.cshtml", "file.cshtml", FileKinds.Legacy);
        var miscProject = (ProjectSnapshot)snapshotResolver.GetMiscellaneousProject();
        var miscProjectSnapshot = projectManager.GetLoadedProject(miscProject.FilePath);
        projectManager.ProjectAdded(miscProject.HostProject);
        projectManager.DocumentAdded(miscProject.HostProject, hostDocument, Mock.Of<TextLoader>(MockBehavior.Strict));

        // Act
        projectService.UpdateProject(hostProject.FilePath, hostProject.Configuration, hostProject.RootNamespace, ProjectWorkspaceState.Default, new[] { addedDocument });

        // Assert
        project = projectManager.GetLoadedProject(hostProject.FilePath);
        var projectFilePaths = project.DocumentFilePaths.OrderBy(path => path);
        Assert.Equal(projectFilePaths, new[] { addedDocument.FilePath });
        miscProjectSnapshot = projectManager.GetLoadedProject(miscProject.FilePath);
        Assert.Empty(miscProjectSnapshot.DocumentFilePaths);
    }

    [Fact]
    public void UpdateProject_MovesExistingDocumentToMisc()
    {
        // Arrange
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var hostProject = new HostProject("C:/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
        projectManager.ProjectAdded(hostProject);
        var project = projectManager.GetLoadedProject(hostProject.FilePath);
        var hostDocument = new HostDocument("C:/path/to/file.cshtml", "file.cshtml", FileKinds.Legacy);
        projectManager.DocumentAdded(hostProject, hostDocument, Mock.Of<TextLoader>(MockBehavior.Strict));
        var projectService = CreateProjectService(projectManager, out var snapshotResolver);
        var newDocument = new DocumentSnapshotHandle("C:/path/to/file2.cshtml", "file2.cshtml", FileKinds.Legacy);

        // Act
        projectService.UpdateProject(hostProject.FilePath, hostProject.Configuration, hostProject.RootNamespace, ProjectWorkspaceState.Default, new[] { newDocument });

        // Assert
        project = projectManager.GetLoadedProject(hostProject.FilePath);
        Assert.Equal(project.DocumentFilePaths, new[] { newDocument.FilePath });

        var miscProject = snapshotResolver.GetMiscellaneousProject();
        Assert.Equal(miscProject.DocumentFilePaths, new[] { hostDocument.FilePath });
    }

    [Fact]
    public void UpdateProject_KnownDocuments()
    {
        // Arrange
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var hostProject = new HostProject("/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
        projectManager.ProjectAdded(hostProject);
        var document = new HostDocument("/path/to/file.cshtml", "file.cshtml", FileKinds.Legacy);
        projectManager.DocumentAdded(hostProject, document, Mock.Of<TextLoader>(MockBehavior.Strict));
        var projectService = CreateProjectService(projectManager);
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
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var hostProject = new HostProject("C:/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
        projectManager.ProjectAdded(hostProject);
        var legacyDocument = new HostDocument("C:/path/to/file.cshtml", "file.cshtml", FileKinds.Legacy);
        projectManager.DocumentAdded(hostProject, legacyDocument, Mock.Of<TextLoader>(MockBehavior.Strict));
        var projectService = CreateProjectService(projectManager);
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
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        projectSnapshotManager.ProjectAdded(ownerProject.HostProject);
        var expectedRootNamespace = "NewRootNamespace";
        var projectService = CreateProjectService(projectSnapshotManager);

        // Act
        projectService.UpdateProject(projectFilePath, ownerProject.Configuration, expectedRootNamespace, ProjectWorkspaceState.Default, _emptyDocuments);

        // Assert
        var project = projectSnapshotManager.GetLoadedProject(ownerProject.FilePath);
        Assert.Equal(expectedRootNamespace, project.RootNamespace);
    }

    [Fact]
    public void UpdateProject_SameConfigurationAndRootNamespaceNoops()
    {
        // Arrange
        var projectFilePath = "C:/path/to/project.csproj";
        var ownerProject = TestProjectSnapshot.Create(projectFilePath);
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        projectSnapshotManager.AllowNotifyListeners = true;

        projectSnapshotManager.ProjectAdded(ownerProject.HostProject);
        var projectService = CreateProjectService(projectSnapshotManager);

        projectSnapshotManager.Changed += (s, e) =>
        {
            Assert.NotEqual(ProjectChangeKind.ProjectChanged, e.Kind);
        };

        // Act & Assert
        projectService.UpdateProject(projectFilePath, ownerProject.Configuration, "TestRootNamespace", ProjectWorkspaceState.Default, _emptyDocuments);
    }

    [Fact]
    public void UpdateProject_NullConfigurationUsesDefault()
    {
        // Arrange
        var projectFilePath = "C:/path/to/project.csproj";
        var ownerProject = TestProjectSnapshot.Create(projectFilePath);
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        projectSnapshotManager.ProjectAdded(ownerProject.HostProject);
        var projectService = CreateProjectService(projectSnapshotManager);

        // Act
        projectService.UpdateProject(projectFilePath, configuration: null, "TestRootNamespace", ProjectWorkspaceState.Default, _emptyDocuments);

        // Assert
        var project = projectSnapshotManager.GetLoadedProject(projectFilePath);
        Assert.Equal(RazorDefaults.Configuration, project.Configuration);
    }

    [Fact]
    public void UpdateProject_ChangesProjectToUseProvidedConfiguration()
    {
        // Arrange
        var projectFilePath = "C:/path/to/project.csproj";
        var ownerProject = TestProjectSnapshot.Create(projectFilePath);
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        projectSnapshotManager.ProjectAdded(ownerProject.HostProject);
        var projectService = CreateProjectService(projectSnapshotManager);

        // Act
        projectService.UpdateProject(projectFilePath, FallbackRazorConfiguration.MVC_1_1, "TestRootNamespace", ProjectWorkspaceState.Default, _emptyDocuments);

        // Assert
        var project = projectSnapshotManager.GetLoadedProject(projectFilePath);
        Assert.Equal(FallbackRazorConfiguration.MVC_1_1, project.Configuration);
    }

    [Fact]
    public void UpdateProject_UntrackedProjectNoops()
    {
        // Arrange
        var projectFilePath = "C:/path/to/project.csproj";
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        projectSnapshotManager.Changed += (s, e) =>
        {
            throw new XunitException("Should have nooped");
        };

        var projectService = CreateProjectService(projectSnapshotManager);

        // Act & Assert
        projectService.UpdateProject(projectFilePath, FallbackRazorConfiguration.MVC_1_1, "TestRootNamespace", ProjectWorkspaceState.Default, _emptyDocuments);
    }

    [Fact]
    public void CloseDocument_ClosesDocumentInOwnerProject()
    {
        // Arrange
        var expectedDocumentFilePath = "C:/path/to/document.cshtml";
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var ownerProject = projectSnapshotManager.CreateAndAddProject("C:/path/to/project.csproj");
        projectSnapshotManager.CreateAndAddDocument(ownerProject, expectedDocumentFilePath);
        projectSnapshotManager.DocumentOpened(ownerProject.FilePath, expectedDocumentFilePath, SourceText.From(string.Empty));
        var projectService = CreateProjectService(projectSnapshotManager);

        Assert.True(projectSnapshotManager.IsDocumentOpen(expectedDocumentFilePath));

        // Act
        projectService.CloseDocument(expectedDocumentFilePath);

        // Assert
        Assert.False(projectSnapshotManager.IsDocumentOpen(expectedDocumentFilePath));
    }

    [Fact]
    public void CloseDocument_ClosesDocumentInMiscellaneousProject()
    {
        // Arrange
        var projectSnapshotManager = CreateSnapshotManagerWithDocumentInMisc(out var expectedDocumentFilePath, out var miscellaneousProject);
        projectSnapshotManager.DocumentOpened(miscellaneousProject.FilePath, expectedDocumentFilePath, SourceText.From(string.Empty));
        var projectService = CreateProjectService(projectSnapshotManager);

        Assert.True(projectSnapshotManager.IsDocumentOpen(expectedDocumentFilePath));

        // Act
        projectService.CloseDocument(expectedDocumentFilePath);

        // Assert
        Assert.False(projectSnapshotManager.IsDocumentOpen(expectedDocumentFilePath));
    }

    [Fact]
    public void OpenDocument_OpensAlreadyAddedDocumentInOwnerProject()
    {
        // Arrange
        var expectedDocumentFilePath = "C:/path/to/document.cshtml";
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var ownerProject = projectSnapshotManager.CreateAndAddProject("C:/path/to/project.csproj");
        projectSnapshotManager.CreateAndAddDocument(ownerProject, expectedDocumentFilePath);
        var projectService = CreateProjectService(projectSnapshotManager);
        var sourceText = SourceText.From("Hello World");

        Assert.False(projectSnapshotManager.IsDocumentOpen(expectedDocumentFilePath));

        // Act
        projectService.OpenDocument(expectedDocumentFilePath, sourceText, 1);

        // Assert
        Assert.True(projectSnapshotManager.IsDocumentOpen(expectedDocumentFilePath));
    }

    [Fact]
    public void OpenDocument_OpensAlreadyAddedDocumentInMiscellaneousProject()
    {
        // Arrange
        var projectSnapshotManager = CreateSnapshotManagerWithDocumentInMisc(out var expectedDocumentFilePath, out var _);
        
        var projectService = CreateProjectService(projectSnapshotManager);
        var sourceText = SourceText.From("Hello World");

        Assert.False(projectSnapshotManager.IsDocumentOpen(expectedDocumentFilePath));

        // Act
        projectService.OpenDocument(expectedDocumentFilePath, sourceText, 1);

        // Assert
        Assert.True(projectSnapshotManager.IsDocumentOpen(expectedDocumentFilePath));
    }

    [Fact]
    public void OpenDocument_OpensAndAddsDocumentToOwnerProject()
    {
        // Arrange
        var expectedDocumentFilePath = "C:/path/to/document.cshtml";
        var ownerProject = TestProjectSnapshot.Create("C:/path/to/project.csproj");
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        projectSnapshotManager.ProjectAdded(ownerProject.HostProject);

        var projectService = CreateProjectService(projectSnapshotManager);
        var sourceText = SourceText.From("Hello World");

        // Act
        projectService.OpenDocument(expectedDocumentFilePath, sourceText, 1);

        // Assert
        var project = projectSnapshotManager.GetLoadedProject(ownerProject.FilePath);
        Assert.Contains(expectedDocumentFilePath, project.DocumentFilePaths);
        Assert.True(projectSnapshotManager.IsDocumentOpen(expectedDocumentFilePath));
    }

    [Fact]
    public void AddDocument_NoopsIfDocumentIsAlreadyAdded()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var project = projectSnapshotManager.CreateAndAddProject("C:/path/to/project.csproj");
        projectSnapshotManager.CreateAndAddDocument(project, documentFilePath);
        var projectService = CreateProjectService(projectSnapshotManager);

        projectSnapshotManager.Changed += (s, e) =>
        {
            throw new XunitException("Shoudl not be called");
        };

        // Act & Assert
        projectService.AddDocument(documentFilePath);
    }

    [Fact]
    public void AddDocument_AddsDocumentToOwnerProject()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var ownerProject = TestProjectSnapshot.Create("C:/path/to/project.csproj");
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        projectSnapshotManager.ProjectAdded(ownerProject.HostProject);

        var projectService = CreateProjectService(projectSnapshotManager);

        // Act
        projectService.AddDocument(documentFilePath);

        // Assert
        var projects = projectSnapshotManager.GetProjects();
        Assert.Single(projects);
        Assert.Single(projects.Single().DocumentFilePaths);
        Assert.Equal(documentFilePath, projects.Single().DocumentFilePaths.Single());
    }

    [Fact]
    public void AddDocument_AddsDocumentToMiscellaneousProject()
    {
        // Arrange
        var snapshotManager = CreateSnapshotManagerWithDocumentInMisc(out var documentFilePath, out var miscellaneousProject);
        var projectService = CreateProjectService(snapshotManager);

        // Act
        projectService.AddDocument(documentFilePath);

        // Assert
        var projects = snapshotManager.GetProjects();
        Assert.Single(projects);
        var project = (ProjectSnapshot)projects.Single();
        Assert.Same(miscellaneousProject.HostProject, project.HostProject);
        Assert.Single(project.DocumentFilePaths);
    }

    [Fact]
    public void RemoveDocument_RemovesDocumentFromOwnerProject()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var ownerProject = projectSnapshotManager.CreateAndAddProject("C:/path/to/project.csproj");
        projectSnapshotManager.CreateAndAddDocument(ownerProject, documentFilePath);

        var projectService = CreateProjectService(projectSnapshotManager);

        // Act
        projectService.RemoveDocument(documentFilePath);

        // Assert
        var project = projectSnapshotManager.GetProjects().Single(p => p.FilePath == ownerProject.FilePath);
        Assert.Empty(project.DocumentFilePaths);
    }

    [Fact]
    public void RemoveOpenDocument_RemovesDocumentFromOwnerProject_MovesToMiscellaneousProject()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var projectSnapshotManager = CreateSnapshotManagerWithDocumentInMisc(out var _, out var miscellaneousProject);
        var ownerProject = projectSnapshotManager.CreateAndAddProject("C:/path/to/project.csproj");
        projectSnapshotManager.CreateAndAddDocument(ownerProject, documentFilePath);
        var projectService = CreateProjectService(projectSnapshotManager);

        projectService.OpenDocument(documentFilePath, SourceText.From(string.Empty), 1);

        // Act
        projectService.RemoveDocument(documentFilePath);

        // Assert
        var project = projectSnapshotManager.GetLoadedProject(miscellaneousProject.FilePath);
        Assert.Contains(documentFilePath, project.DocumentFilePaths);
    }

    [Fact]
    public void RemoveDocument_RemovesDocumentFromMiscellaneousProject()
    {
        // Arrange
        var projectSnapshotManager = CreateSnapshotManagerWithDocumentInMisc(out var documentFilePath, out var miscellaneousProject);
        var projectService = CreateProjectService(projectSnapshotManager);

        // Act
        projectService.RemoveDocument(documentFilePath);

        // Assert
        var project = projectSnapshotManager.GetProjects().Single(p => p.FilePath == miscellaneousProject.FilePath);
        Assert.Empty(project.DocumentFilePaths);
    }

    [Fact]
    public void RemoveDocument_NoopsIfOwnerProjectDoesNotContainDocument()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var ownerProject = projectSnapshotManager.CreateAndAddProject("C:/path/to/project.csproj");
        var projectService = CreateProjectService(projectSnapshotManager);

        projectSnapshotManager.Changed += (s, e) =>
        {
            throw new XunitException("Should not be called");
        };

        // Act & Assert
        projectService.RemoveDocument(documentFilePath);
    }

    [Fact]
    public void RemoveDocument_NoopsIfMiscellaneousProjectDoesNotContainDocument()
    {
        // Arrange
        var documentFilePath = "C:/path/that/does/not/exist.cshtml";
        var projectSnapshotManager = CreateSnapshotManagerWithDocumentInMisc(out var _, out var miscellaneousProject);
        var projectService = CreateProjectService(projectSnapshotManager);

        projectSnapshotManager.Changed += (s, e) =>
        {
            throw new XunitException("Should not be called");
        };

        // Act & Assert
        projectService.RemoveDocument(documentFilePath);
    }

    [Fact]
    public void UpdateDocument_ChangesDocumentInOwnerProject()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        projectSnapshotManager.AllowNotifyListeners = true;
        var ownerProject = projectSnapshotManager.CreateAndAddProject("C:/path/to/project.csproj");
        projectSnapshotManager.CreateAndAddDocument(ownerProject, documentFilePath);
        var projectService = CreateProjectService(projectSnapshotManager);
        var newText = SourceText.From("Something New");

        // Act
        var raised = Assert.Raises<ProjectChangeEventArgs>(
            a => projectSnapshotManager.Changed += a,
            a => projectSnapshotManager.Changed -= a,
            () => projectService.UpdateDocument(documentFilePath, newText, 1337));

        // Assert
        Assert.Equal(ProjectChangeKind.DocumentChanged, raised.Arguments.Kind);
        Assert.Equal(documentFilePath, raised.Arguments.DocumentFilePath);
    }

    [Fact]
    public void UpdateDocument_ChangesDocumentInMiscProject()
    {
        // Arrange
        var projectSnapshotManager = CreateSnapshotManagerWithDocumentInMisc(out var documentFilePath, out var miscellaneousProject);
        projectSnapshotManager.AllowNotifyListeners = true;
        var newText = SourceText.From("Something New");
        var projectService = CreateProjectService(projectSnapshotManager);

        // Act
        var raised = Assert.Raises<ProjectChangeEventArgs>(
            a => projectSnapshotManager.Changed += a,
            a => projectSnapshotManager.Changed -= a,
            () => projectService.UpdateDocument(documentFilePath, newText, 1337));

        // Assert
        Assert.Equal(ProjectChangeKind.DocumentChanged, raised.Arguments.Kind);
        Assert.Equal(documentFilePath, raised.Arguments.DocumentFilePath);
    }

    [Fact]
    public void UpdateDocument_TracksKnownDocumentVersion()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var ownerProject = TestProjectSnapshot.Create("C:/path/to/project.csproj", new[] { documentFilePath });
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        projectSnapshotManager.ProjectAdded(ownerProject.HostProject);
        var documentSnapshot = projectSnapshotManager.CreateAndAddDocument(ownerProject, documentFilePath);
        var documentVersionCache = new DefaultDocumentVersionCache();
        documentVersionCache.Initialize(projectSnapshotManager);
        documentVersionCache.TrackDocumentVersion(documentSnapshot, 1);

        var newText = SourceText.From("Something New");
        var projectService = CreateProjectService(
            projectSnapshotManager,
            documentVersionCache);

        // Act
        projectService.UpdateDocument(documentFilePath, newText, 1337);

        // Assert
        Assert.True(documentVersionCache.TryGetLatestVersionFromPath(documentFilePath, out var version));
        Assert.Equal(1337, version.Value);
    }

    [Fact]
    public void UpdateDocument_IgnoresUnknownDocumentVersions()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var ownerProject = TestProjectSnapshot.Create("C:/path/to/project.csproj", new[] { documentFilePath });
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        projectSnapshotManager.ProjectAdded(ownerProject.HostProject);
        var documentSnapshot = projectSnapshotManager.CreateAndAddDocument(ownerProject, documentFilePath);
        var documentVersionCache = new DefaultDocumentVersionCache();
        documentVersionCache.Initialize(projectSnapshotManager);

        var projectService = CreateProjectService(
            projectSnapshotManager,
            documentVersionCache: documentVersionCache);

        // Act & Assert
        Assert.False(documentVersionCache.TryGetLatestVersionFromPath(documentFilePath, out var _));
    }

    [Fact]
    public void AddProject_AddsProjectWithDefaultConfiguration()
    {
        // Arrange
        var projectFilePath = "C:/path/to/project.csproj";
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var projectService = CreateProjectService(projectSnapshotManager);

        // Act
        projectService.AddProject(projectFilePath, rootNamespace: null);

        // Assert
        var projects = projectSnapshotManager.GetProjects().Where(p => p.FilePath == projectFilePath);
        Assert.Single(projects);
    }

    [Fact]
    public void AddProject_AlreadyAdded_Noops()
    {
        // Arrange
        var projectFilePath = "C:/path/to/project.csproj";

        // use with misc already added so we can check change event
        var projectSnapshotManager = CreateSnapshotManagerWithDocumentInMisc(out var _, out var _1);
        projectSnapshotManager.CreateAndAddProject(projectFilePath);
        var projectService = CreateProjectService(projectSnapshotManager);

        projectSnapshotManager.Changed += (s, e) =>
        {
            throw new XunitException("Unexpected change call");
        };

        // Act
        projectService.AddProject(projectFilePath, rootNamespace: null);

        // Assert
        // Exception would happen in changed callback
    }

    [Fact]
    public void RemoveProject_RemovesProject()
    {
        // Arrange
        var projectFilePath = "C:/path/to/project.csproj";
        // use with misc already added so we can check change event
        var projectSnapshotManager = CreateSnapshotManagerWithDocumentInMisc(out var _, out var _1);
        projectSnapshotManager.CreateAndAddProject(projectFilePath);
        var projectService = CreateProjectService(projectSnapshotManager);

        Assert.NotNull(projectSnapshotManager.GetLoadedProject(projectFilePath));

        // Act
        projectService.RemoveProject(projectFilePath);

        // Assert
        Assert.Null(projectSnapshotManager.GetLoadedProject(projectFilePath));
    }

    [Fact]
    public void RemoveProject_NoopsIfProjectIsNotLoaded()
    {
        // Arrange
        var projectFilePath = "C:/path/to/project.csproj";
        var projectSnapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        var projectService = CreateProjectService(projectSnapshotManager);

        projectSnapshotManager.Changed += (s, e) =>
        {
            throw new XunitException("Should not have been called");
        };

        // Act & Assert
        projectService.RemoveProject(projectFilePath);
    }

    [Fact]
    public void TryMigrateDocumentsFromRemovedProject_MigratesDocumentsToNonMiscProject()
    {
        // Arrange
        var documentFilePath1 = "C:/path/to/some/document1.cshtml";
        var documentFilePath2 = "C:/path/to/some/document2.cshtml";
        var projectSnapshotManager = CreateSnapshotManagerWithDocumentInMisc(out var _, out var miscellaneousProject);
        var removedProject = TestProjectSnapshot.Create("C:/path/to/some/project.csproj", new[] { documentFilePath1, documentFilePath2 });
        var projectToBeMigratedTo = projectSnapshotManager.CreateAndAddProject("C:/path/to/project.csproj");
        var projectService = CreateProjectService(projectSnapshotManager);

        // Act
        projectService.TryMigrateDocumentsFromRemovedProject(removedProject);

        // Assert
        var project = projectSnapshotManager.GetLoadedProject(projectToBeMigratedTo.FilePath);
        Assert.Contains(documentFilePath1, project.DocumentFilePaths);
        Assert.Contains(documentFilePath2, project.DocumentFilePaths);
    }

    [Fact]
    public void TryMigrateDocumentsFromRemovedProject_MigratesDocumentsToMiscProject()
    {
        // Arrange
        var documentFilePath1 = "C:/path/to/some/document1.cshtml";
        var documentFilePath2 = "C:/path/to/some/document2.cshtml";
        var projectSnapshotManager = CreateSnapshotManagerWithDocumentInMisc(out var _, out var miscellaneousProject);
        var removedProject = TestProjectSnapshot.Create("C:/path/to/some/project.csproj", new[] { documentFilePath1, documentFilePath2 });
        var projectService = CreateProjectService(projectSnapshotManager);

        // Act
        projectService.TryMigrateDocumentsFromRemovedProject(removedProject);

        // Assert
        var project = projectSnapshotManager.GetLoadedProject(miscellaneousProject.FilePath);
        Assert.Contains(documentFilePath1, project.DocumentFilePaths);
        Assert.Contains(documentFilePath2 , project.DocumentFilePaths);
    }

    [Fact]
    public void TryMigrateMiscellaneousDocumentsToProject_DoesNotMigrateDocumentsIfNoOwnerProject()
    {
        // Arrange
        var documentFilePath1 = "C:/path/to/document1.cshtml";
        var documentFilePath2 = "C:/path/to/document2.cshtml";
        var projectSnapshotManager = CreateSnapshotManagerWithDocumentInMisc(out var _, out var miscellaneousProject);
        projectSnapshotManager.CreateAndAddDocument(miscellaneousProject, documentFilePath1);
        projectSnapshotManager.CreateAndAddDocument(miscellaneousProject, documentFilePath2);
        var projectService = CreateProjectService(projectSnapshotManager);

        projectSnapshotManager.Changed += (s, e) =>
        {
            throw new XunitException("Should not have been called");
        };

        // Act & Assert
        projectService.TryMigrateMiscellaneousDocumentsToProject();
    }

    [Fact]
    public void TryMigrateMiscellaneousDocumentsToProject_MigratesDocumentsToNewOwnerProject()
    {
        // Arrange
        var documentFilePath1 = "C:/path/to/document1.cshtml";
        var documentFilePath2 = "C:/path/to/document2.cshtml";
        var projectSnapshotManager = CreateSnapshotManagerWithDocumentInMisc(out var _, out var miscellaneousProject);
        projectSnapshotManager.CreateAndAddDocument(miscellaneousProject, documentFilePath1);
        projectSnapshotManager.CreateAndAddDocument(miscellaneousProject, documentFilePath2);
        var projectToBeMigratedTo = projectSnapshotManager.CreateAndAddProject("C:/path/to/project.csproj");
        var projectService = CreateProjectService(projectSnapshotManager);

        // Act
        projectService.TryMigrateMiscellaneousDocumentsToProject();

        // Assert
        var project = projectSnapshotManager.GetLoadedProject(projectToBeMigratedTo.FilePath);
        Assert.Contains(documentFilePath1, project.DocumentFilePaths);
        Assert.Contains(documentFilePath2, project.DocumentFilePaths);
    }

    private DefaultRazorProjectService CreateProjectService(
        ProjectSnapshotManagerBase projectSnapshotManager,
        DocumentVersionCache documentVersionCache = null)
        => CreateProjectService(projectSnapshotManager, out var _, documentVersionCache);

    private DefaultRazorProjectService CreateProjectService(
        ProjectSnapshotManagerBase projectSnapshotManager,
        out SnapshotResolver snapshotResolver,
        DocumentVersionCache documentVersionCache = null)
    {
        if (documentVersionCache is null)
        {
            documentVersionCache = new Mock<DocumentVersionCache>(MockBehavior.Strict).Object;
            Mock.Get(documentVersionCache).Setup(c => c.TrackDocumentVersion(It.IsAny<IDocumentSnapshot>(), It.IsAny<int>())).Verifiable();
        }

        var accessor = Mock.Of<ProjectSnapshotManagerAccessor>(a => a.Instance == projectSnapshotManager, MockBehavior.Strict);
        snapshotResolver = new SnapshotResolver(accessor);

        var remoteTextLoaderFactory = Mock.Of<RemoteTextLoaderFactory>(factory => factory.Create(It.IsAny<string>()) == Mock.Of<TextLoader>(MockBehavior.Strict), MockBehavior.Strict);
        var projectService = new DefaultRazorProjectService(
            LegacyDispatcher,
            remoteTextLoaderFactory,
            snapshotResolver,
            documentVersionCache,
            accessor,
            LoggerFactory);

        return projectService;
    }

    private TestProjectSnapshotManager CreateSnapshotManagerWithDocumentInMisc(out string documentFilePath, out TestProjectSnapshot miscellaneousProject)
    {
        var projectDirectory = TempDirectory.Instance.DirectoryPath;
        documentFilePath = FilePathNormalizer.Normalize(Path.Join(projectDirectory, "document.cshtml"));
        var projectPath = FilePathNormalizer.Normalize(Path.Join(projectDirectory, "__MISC_RAZOR_PROJECT__"));
        miscellaneousProject = TestProjectSnapshot.Create(projectPath);
        var snapshotManager = TestProjectSnapshotManager.Create(ErrorReporter);
        snapshotManager.ProjectAdded(miscellaneousProject.HostProject);
        snapshotManager.CreateAndAddDocument(miscellaneousProject, documentFilePath);

        return snapshotManager;
    }
}
