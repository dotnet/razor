// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test;
public class SnapshotResolverTest(ITestOutputHelper testOutput) : LanguageServerTestBase(testOutput)
{
    [Fact]
    public void TryResolveDocumentInAnyProject_AsksPotentialParentProjectForDocumentItsTracking_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = @"C:\path\to\document.cshtml";
        var normalizedFilePath = "C:/path/to/document.cshtml";
        var snapshotResolver = CreateSnapshotResolver(normalizedFilePath);

        // Act
        var result = snapshotResolver.TryResolveDocumentInAnyProject(documentFilePath, out var document);

        // Assert
        Assert.True(result);
        Assert.NotNull(document);
        Assert.Equal(normalizedFilePath, document.FilePath);
    }

    [Fact]
    public void TryResolveDocumentInAnyProject_AsksMiscellaneousProjectForDocumentItIsTracking_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = @"C:\path\to\document.cshtml";
        var normalizedFilePath = "C:/path/to/document.cshtml";
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);
        var miscProject = snapshotResolver.GetMiscellaneousProject();

        var hostProject = new HostProject(miscProject.FilePath, miscProject.IntermediateOutputPath, FallbackRazorConfiguration.Latest, miscProject.RootNamespace);
        projectManager.DocumentAdded(
            hostProject.Key,
            new HostDocument(normalizedFilePath, "document.cshtml"),
            new EmptyTextLoader(normalizedFilePath));

        // Act
        var result = snapshotResolver.TryResolveDocumentInAnyProject(documentFilePath, out var document);

        // Assert
        Assert.True(result);
        Assert.NotNull(document);
        Assert.Equal(normalizedFilePath, document.FilePath);
    }

    [Fact]
    public void TryResolveDocumentInAnyProject_AsksPotentialParentProjectForDocumentItsNotTrackingAndMiscellaneousProjectIsNotTrackingEither_ReturnsFalse()
    {
        // Arrange
        var documentFilePath = @"C:\path\to\document.cshtml";
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        // Act
        var result = snapshotResolver.TryResolveDocumentInAnyProject(documentFilePath, out var document);

        // Assert
        Assert.False(result);
        Assert.Null(document);
    }

    [Fact]
    public void TryResolveAllProjects_NoProjects_ReturnsFalse()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        // Act
        var result = snapshotResolver.TryResolveAllProjects(documentFilePath, out var projects);

        // Assert
        Assert.False(result);
        Assert.Empty(projects);
    }

    [Fact]
    public void TryResolveAllProjects_OnlyMiscellaneousProjectDoesNotContainDocument_ReturnsFalse()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);
        _ = snapshotResolver.GetMiscellaneousProject();

        // Act
        var result = snapshotResolver.TryResolveAllProjects(documentFilePath, out var projects);

        // Assert
        Assert.False(result);
        Assert.Empty(projects);
    }

    [Fact]
    public void TryResolveAllProjects_OnlyMiscellaneousProjectContainsDocument_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = Path.Join(TempDirectory.Instance.DirectoryPath, "document.cshtml");
        var snapshotResolver = CreateSnapshotResolver(documentFilePath, addToMiscellaneous: true);

        // Act
        var result = snapshotResolver.TryResolveAllProjects(documentFilePath, out var projects);

        // Assert
        Assert.True(result);
        Assert.Single(projects, snapshotResolver.GetMiscellaneousProject());
    }

    [Fact]
    public void TryResolveAllProjects_UnrelatedProject_ReturnsFalse()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);
        projectManager.ProjectAdded(TestProjectSnapshot.Create("C:/other/path/to/project.csproj").HostProject);

        // Act
        var result = snapshotResolver.TryResolveAllProjects(documentFilePath, out var projects);

        // Assert
        Assert.False(result);
        Assert.Empty(projects);
    }

    [Fact]
    public void TryResolveAllProjects_OwnerProjectWithOthers_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        var expectedProject = projectManager.CreateAndAddProject("C:/path/to/project.csproj");
        projectManager.CreateAndAddProject("C:/path/to/other/project.csproj");
        projectManager.CreateAndAddDocument(expectedProject, documentFilePath);

        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        // Act
        var result = snapshotResolver.TryResolveAllProjects(documentFilePath, out var projects);

        // Assert
        Assert.True(result);
        AssertSnapshotsEqual(expectedProject, projects.Single());
    }

    [Fact]
    public void TryResolveAllProjects_MiscellaneousOwnerProjectWithOthers_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = Path.Join(TempDirectory.Instance.DirectoryPath, "file.cshtml");
        documentFilePath = FilePathNormalizer.Normalize(documentFilePath);

        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);
        var miscProject = (ProjectSnapshot)snapshotResolver.GetMiscellaneousProject();
        projectManager.CreateAndAddDocument(miscProject, documentFilePath);
        projectManager.CreateAndAddProject("C:/path/to/project.csproj");

        // Act
        var result = snapshotResolver.TryResolveAllProjects(documentFilePath, out var projects);

        // Assert
        Assert.True(result);
        AssertSnapshotsEqual(miscProject, projects.Single());
    }

    [OSSkipConditionFact(new[] { "OSX", "Linux" })]
    public void TryResolveAllProjects_OwnerProjectDifferentCasing_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = "c:/path/to/document.cshtml";
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        var ownerProject = projectManager.CreateAndAddProject("C:/Path/To/project.csproj");
        projectManager.CreateAndAddDocument(ownerProject, documentFilePath);

        // Act
        var result = snapshotResolver.TryResolveAllProjects(documentFilePath, out var projects);

        // Assert
        Assert.True(result);
        AssertSnapshotsEqual(ownerProject, projects.Single());
    }

    [Fact]
    public void GetMiscellaneousProject_ProjectLoaded_ReturnsExistingProject()
    {
        // Arrange
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        // Act
        var project = snapshotResolver.GetMiscellaneousProject();
        var inManager = projectManager.GetLoadedProject(snapshotResolver.MiscellaneousHostProject.Key);

        // Assert
        Assert.Same(inManager, project);
    }

    [Fact]
    public void GetMiscellaneousProject_ProjectNotLoaded_CreatesProjectAndReturnsCreatedProject()
    {
        // Arrange
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        // Act
        var project = snapshotResolver.GetMiscellaneousProject();

        // Assert
        Assert.Single(projectManager.GetProjects());
        Assert.Equal(snapshotResolver.MiscellaneousHostProject.FilePath, project.FilePath);
    }

    private SnapshotResolver CreateSnapshotResolver(string filePath, bool addToMiscellaneous = false)
        => CreateSnapshotResolver(filePath, out var _, addToMiscellaneous);

    private SnapshotResolver CreateSnapshotResolver(string filePath, out TestProjectSnapshotManager projectManager, bool addToMiscellaneous = false)
    {
        filePath = FilePathNormalizer.Normalize(filePath);

        projectManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        if (addToMiscellaneous)
        {
            var miscProject = (ProjectSnapshot)snapshotResolver.GetMiscellaneousProject();
            projectManager.CreateAndAddDocument(miscProject, filePath);
        }
        else
        {
            var projectDirectory = FilePathNormalizer.GetNormalizedDirectoryName(filePath);
            var projectSnapshot = TestProjectSnapshot.Create(Path.Join(projectDirectory, "proj.csproj"));

            projectManager.ProjectAdded(projectSnapshot.HostProject);
            projectManager.CreateAndAddDocument(projectSnapshot, filePath);

        }

        return snapshotResolver;
    }

    private static void AssertSnapshotsEqual(IProjectSnapshot first, IProjectSnapshot second)
    {
        Assert.Equal(first.FilePath, second.FilePath);
        Assert.Equal(first.CSharpLanguageVersion, second.CSharpLanguageVersion);
        Assert.Equal(first.RootNamespace, second.RootNamespace);
    }
}
