// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test;

public class SnapshotResolverTest(ITestOutputHelper testOutput) : LanguageServerTestBase(testOutput)
{
    [Fact]
    public async Task TryResolveDocumentInAnyProject_AsksPotentialParentProjectForDocumentItsTracking_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = @"C:\path\to\document.cshtml";
        var normalizedFilePath = "C:/path/to/document.cshtml";
        var (snapshotResolver, _) = await CreateSnapshotResolverAsync(normalizedFilePath);

        // Act
        IDocumentSnapshot document = null;
        var result = await RunOnDispatcherAsync(() =>
        {
            return snapshotResolver.TryResolveDocumentInAnyProject(documentFilePath, out document);
        });

        // Assert
        Assert.True(result);
        Assert.Equal(normalizedFilePath, document.FilePath);
    }

    [Fact]
    public async Task TryResolveDocumentInAnyProject_AsksMiscellaneousProjectForDocumentItIsTracking_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = @"C:\path\to\document.cshtml";
        var normalizedFilePath = "C:/path/to/document.cshtml";
        var projectSnapshotManagerAccessor = new TestProjectSnapshotManagerAccessor(TestProjectSnapshotManager.Create(Dispatcher, ErrorReporter));
        var snapshotResolver = new SnapshotResolver(projectSnapshotManagerAccessor, LoggerFactory);

        await RunOnDispatcherAsync(() =>
        {
            var miscProject = snapshotResolver.GetMiscellaneousProject();
            var hostProject = new HostProject(miscProject.FilePath, miscProject.IntermediateOutputPath, FallbackRazorConfiguration.Latest, miscProject.RootNamespace);
            projectSnapshotManagerAccessor.Instance.DocumentAdded(
                hostProject.Key,
                new HostDocument(normalizedFilePath, "document.cshtml"),
                new EmptyTextLoader(normalizedFilePath));
        });

        // Act
        IDocumentSnapshot document = null;
        var result = await RunOnDispatcherAsync(() =>
        {
            return snapshotResolver.TryResolveDocumentInAnyProject(documentFilePath, out document);
        });

        // Assert
        Assert.True(result);
        Assert.NotNull(document);
        Assert.Equal(normalizedFilePath, document.FilePath);
    }

    [Fact]
    public async Task TryResolveDocumentInAnyProject_AsksPotentialParentProjectForDocumentItsNotTrackingAndMiscellaneousProjectIsNotTrackingEither_ReturnsFalse()
    {
        // Arrange
        var documentFilePath = @"C:\path\to\document.cshtml";
        var projectSnapshotManagerAccessor = new TestProjectSnapshotManagerAccessor(TestProjectSnapshotManager.Create(Dispatcher, ErrorReporter));
        var snapshotResolver = new SnapshotResolver(projectSnapshotManagerAccessor, LoggerFactory);

        // Act
        IDocumentSnapshot document = null;
        var result = await RunOnDispatcherAsync(() =>
        {
            return snapshotResolver.TryResolveDocumentInAnyProject(documentFilePath, out document);
        });

        // Assert
        Assert.False(result);
        Assert.Null(document);
    }

    [Fact]
    public async Task TryResolveAllProjects_NoProjects_ReturnsFalse()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(TestProjectSnapshotManager.Create(Dispatcher, ErrorReporter)), LoggerFactory);

        // Act
        IProjectSnapshot[] projects = null;
        var result = await RunOnDispatcherAsync(() =>
        {
            return snapshotResolver.TryResolveAllProjects(documentFilePath, out projects);
        });

        // Assert
        Assert.False(result);
        Assert.Empty(projects);
    }

    [Fact]
    public async Task TryResolveAllProjects_OnlyMiscellaneousProjectDoesNotContainDocument_ReturnsFalse()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var snapshotResolver = new SnapshotResolver(
            new TestProjectSnapshotManagerAccessor(TestProjectSnapshotManager.Create(Dispatcher, ErrorReporter)), LoggerFactory);

        await RunOnDispatcherAsync(() =>
        {
            _ = snapshotResolver.GetMiscellaneousProject();
        });

        // Act
        IProjectSnapshot[] projects = null;
        var result = await RunOnDispatcherAsync(() =>
        {
            return snapshotResolver.TryResolveAllProjects(documentFilePath, out projects);
        });

        // Assert
        Assert.False(result);
        Assert.Empty(projects);
    }

    [Fact]
    public async Task TryResolveAllProjects_OnlyMiscellaneousProjectContainsDocument_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = Path.Combine(TempDirectory.Instance.DirectoryPath, "document.cshtml");
        var (snapshotResolver, _) = await CreateSnapshotResolverAsync(documentFilePath, addToMiscellaneous: true);

        // Act
        IProjectSnapshot[] projects = null;
        var result = await RunOnDispatcherAsync(() =>
        {
            return snapshotResolver.TryResolveAllProjects(documentFilePath, out projects);
        });

        // Assert
        Assert.True(result);
        Assert.Single(projects, snapshotResolver.GetMiscellaneousProject());
    }

    [Fact]
    public async Task TryResolveAllProjects_UnrelatedProject_ReturnsFalse()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var snapshotManager = TestProjectSnapshotManager.Create(Dispatcher, ErrorReporter);
        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(snapshotManager), LoggerFactory);

        await RunOnDispatcherAsync(() =>
        {
            snapshotManager.ProjectAdded(TestProjectSnapshot.Create("C:/other/path/to/project.csproj").HostProject);
        });

        // Act
        IProjectSnapshot[] projects = null;
        var result = await RunOnDispatcherAsync(() =>
        {
            return snapshotResolver.TryResolveAllProjects(documentFilePath, out projects);
        });

        // Assert
        Assert.False(result);
        Assert.Empty(projects);
    }

    [Fact]
    public async Task TryResolveAllProjects_OwnerProjectWithOthers_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var snapshotManager = TestProjectSnapshotManager.Create(Dispatcher, ErrorReporter);

        var expectedProject = await RunOnDispatcherAsync(() =>
        {
            var expectedProject = snapshotManager.CreateAndAddProject("C:/path/to/project.csproj");
            snapshotManager.CreateAndAddProject("C:/path/to/other/project.csproj");
            snapshotManager.CreateAndAddDocument(expectedProject, documentFilePath);

            return expectedProject;
        });

        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(snapshotManager), LoggerFactory);

        // Act
        IProjectSnapshot[] projects = null;
        var result = await RunOnDispatcherAsync(() =>
        {
            return snapshotResolver.TryResolveAllProjects(documentFilePath, out projects);
        });

        // Assert
        Assert.True(result);
        AssertSnapshotsEqual(expectedProject, projects.Single());
    }

    [Fact]
    public async Task TryResolveAllProjects_MiscellaneousOwnerProjectWithOthers_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = Path.Combine(TempDirectory.Instance.DirectoryPath, "file.cshtml");
        documentFilePath = FilePathNormalizer.Normalize(documentFilePath);

        var snapshotManager = TestProjectSnapshotManager.Create(Dispatcher, ErrorReporter);
        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(snapshotManager), LoggerFactory);

        var miscProject = await RunOnDispatcherAsync(() =>
        {
            var miscProject = (ProjectSnapshot)snapshotResolver.GetMiscellaneousProject();
            snapshotManager.CreateAndAddDocument(miscProject, documentFilePath);
            snapshotManager.CreateAndAddProject("C:/path/to/project.csproj");

            return miscProject;
        });

        // Act
        var result = snapshotResolver.TryResolveAllProjects(documentFilePath, out var projects);

        // Assert
        Assert.True(result);
        AssertSnapshotsEqual(miscProject, projects.Single());
    }

    [OSSkipConditionFact(new[] { "OSX", "Linux" })]
    public async Task TryResolveAllProjects_OwnerProjectDifferentCasing_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = "c:/path/to/document.cshtml";
        var snapshotManager = TestProjectSnapshotManager.Create(Dispatcher, ErrorReporter);
        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(snapshotManager), LoggerFactory);

        var ownerProject = await RunOnDispatcherAsync(() =>
        {
            var ownerProject = snapshotManager.CreateAndAddProject("C:/Path/To/project.csproj");
            snapshotManager.CreateAndAddDocument(ownerProject, documentFilePath);

            return ownerProject;
        });

        // Act
        IProjectSnapshot[] projects = null;
        var result = await RunOnDispatcherAsync(() =>
        {
            return snapshotResolver.TryResolveAllProjects(documentFilePath, out projects);
        });

        // Assert
        Assert.True(result);
        AssertSnapshotsEqual(ownerProject, projects.Single());
    }

    [Fact]
    public async Task GetMiscellaneousProject_ProjectLoaded_ReturnsExistingProject()
    {
        // Arrange
        var snapshotManager = TestProjectSnapshotManager.Create(Dispatcher, ErrorReporter);
        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(snapshotManager), LoggerFactory);

        // Act
        var project = await RunOnDispatcherAsync(snapshotResolver.GetMiscellaneousProject);
        var inManager = snapshotManager.GetLoadedProject(snapshotResolver.MiscellaneousHostProject.Key);

        // Assert
        Assert.Same(inManager, project);
    }

    [Fact]
    public async Task GetMiscellaneousProject_ProjectNotLoaded_CreatesProjectAndReturnsCreatedProject()
    {
        // Arrange
        var snapshotManager = TestProjectSnapshotManager.Create(Dispatcher, ErrorReporter);
        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(snapshotManager), LoggerFactory);

        // Act
        var project = await RunOnDispatcherAsync(snapshotResolver.GetMiscellaneousProject);

        // Assert
        Assert.Single(snapshotManager.GetProjects());
        Assert.Equal(snapshotResolver.MiscellaneousHostProject.FilePath, project.FilePath);
    }

    private async Task<(SnapshotResolver, TestProjectSnapshotManager)> CreateSnapshotResolverAsync(string filePath, bool addToMiscellaneous = false)
    {
        filePath = FilePathNormalizer.Normalize(filePath);

        var snapshotManager = TestProjectSnapshotManager.Create(Dispatcher, ErrorReporter);
        var snapshotResolver = new SnapshotResolver(new TestProjectSnapshotManagerAccessor(snapshotManager), LoggerFactory);

        if (addToMiscellaneous)
        {
            await RunOnDispatcherAsync(() =>
            {
                var miscProject = (ProjectSnapshot)snapshotResolver.GetMiscellaneousProject();
                snapshotManager.CreateAndAddDocument(miscProject, filePath);
            });
        }
        else
        {
            var projectDirectory = FilePathNormalizer.GetNormalizedDirectoryName(filePath);
            var projectSnapshot = TestProjectSnapshot.Create(Path.Combine(projectDirectory, "proj.csproj"));

            await RunOnDispatcherAsync(() =>
            {
                snapshotManager.ProjectAdded(projectSnapshot.HostProject);
                snapshotManager.CreateAndAddDocument(projectSnapshot, filePath);
            });
        }

        return (snapshotResolver, snapshotManager);
    }

    private static void AssertSnapshotsEqual(IProjectSnapshot first, IProjectSnapshot second)
    {
        Assert.Equal(first.FilePath, second.FilePath);
        Assert.Equal(first.CSharpLanguageVersion, second.CSharpLanguageVersion);
        Assert.Equal(first.RootNamespace, second.RootNamespace);
    }
}
