﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;
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
        var snapshotResolver = await CreateSnapshotResolverAsync(normalizedFilePath);

        // Act
        var document = await snapshotResolver.ResolveDocumentInAnyProjectAsync(documentFilePath, DisposalToken);

        // Assert
        Assert.NotNull(document);
        Assert.Equal(normalizedFilePath, document.FilePath);
    }

    [Fact]
    public async Task TryResolveDocumentInAnyProject_AsksMiscellaneousProjectForDocumentItIsTracking_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = @"C:\path\to\document.cshtml";
        var normalizedFilePath = "C:/path/to/document.cshtml";
        var projectManager = CreateProjectSnapshotManager();
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        await projectManager.UpdateAsync(async updater =>
        {
            var miscProject = await snapshotResolver.GetMiscellaneousProjectAsync(DisposalToken);
            var hostProject = new HostProject(miscProject.FilePath, miscProject.IntermediateOutputPath, FallbackRazorConfiguration.Latest, miscProject.RootNamespace);
            updater.DocumentAdded(
                hostProject.Key,
                new HostDocument(normalizedFilePath, "document.cshtml"),
                new EmptyTextLoader(normalizedFilePath));
        });

        // Act
        var document = await snapshotResolver.ResolveDocumentInAnyProjectAsync(documentFilePath, DisposalToken);

        // Assert
        Assert.NotNull(document);
        Assert.Equal(normalizedFilePath, document.FilePath);
    }

    [Fact]
    public async Task TryResolveDocumentInAnyProject_AsksPotentialParentProjectForDocumentItsNotTrackingAndMiscellaneousProjectIsNotTrackingEither_ReturnsFalse()
    {
        // Arrange
        var documentFilePath = @"C:\path\to\document.cshtml";
        var projectManager = CreateProjectSnapshotManager();
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        // Act
        var document = await snapshotResolver.ResolveDocumentInAnyProjectAsync(documentFilePath, DisposalToken);

        // Assert
        Assert.Null(document);
    }

    [Fact]
    public async Task TryResolveAllProjects_NoProjects_ReturnsFalse()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var projectManager = CreateProjectSnapshotManager();
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        // Act
        var projects = await snapshotResolver.TryResolveAllProjectsAsync(documentFilePath, DisposalToken);

        // Assert
        Assert.Empty(projects);
    }

    [Fact]
    public async Task TryResolveAllProjects_OnlyMiscellaneousProjectDoesNotContainDocument_ReturnsFalse()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var projectManager = CreateProjectSnapshotManager();
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        await snapshotResolver.GetMiscellaneousProjectAsync(DisposalToken);

        // Act
        var projects = await snapshotResolver.TryResolveAllProjectsAsync(documentFilePath, DisposalToken);

        // Assert
        Assert.Empty(projects);
    }

    [Fact]
    public async Task TryResolveAllProjects_OnlyMiscellaneousProjectContainsDocument_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = Path.Combine(TempDirectory.Instance.DirectoryPath, "document.cshtml");
        var snapshotResolver = await CreateSnapshotResolverAsync(documentFilePath, addToMiscellaneous: true);

        // Act
        var projects = await snapshotResolver.TryResolveAllProjectsAsync(documentFilePath, DisposalToken);

        // Assert
        var miscFilesProject = await snapshotResolver.GetMiscellaneousProjectAsync(DisposalToken);
        Assert.Single(projects, miscFilesProject);
    }

    [Fact]
    public async Task TryResolveAllProjects_UnrelatedProject_ReturnsFalse()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var projectManager = CreateProjectSnapshotManager();
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        await projectManager.UpdateAsync(updater =>
        {
            updater.ProjectAdded(TestProjectSnapshot.Create("C:/other/path/to/project.csproj").HostProject);
        });

        // Act
        var projects = await snapshotResolver.TryResolveAllProjectsAsync(documentFilePath, DisposalToken);

        // Assert
        Assert.Empty(projects);
    }

    [Fact]
    public async Task TryResolveAllProjects_OwnerProjectWithOthers_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = "C:/path/to/document.cshtml";
        var projectManager = CreateProjectSnapshotManager();

        var expectedProject = await projectManager.UpdateAsync(updater =>
        {
            var expectedProject = updater.CreateAndAddProject("C:/path/to/project.csproj");
            updater.CreateAndAddProject("C:/path/to/other/project.csproj");
            updater.CreateAndAddDocument(expectedProject, documentFilePath);

            return expectedProject;
        });

        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        // Act
        var projects = await snapshotResolver.TryResolveAllProjectsAsync(documentFilePath, DisposalToken);

        // Assert
        var project = Assert.Single(projects);
        AssertSnapshotsEqual(expectedProject, project);
    }

    [Fact]
    public async Task TryResolveAllProjects_MiscellaneousOwnerProjectWithOthers_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = Path.Combine(TempDirectory.Instance.DirectoryPath, "file.cshtml");
        documentFilePath = FilePathNormalizer.Normalize(documentFilePath);

        var projectManager = CreateProjectSnapshotManager();
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        var miscProject = await projectManager.UpdateAsync(async updater =>
        {
            var miscProject = (ProjectSnapshot)await snapshotResolver.GetMiscellaneousProjectAsync(DisposalToken);
            updater.CreateAndAddDocument(miscProject, documentFilePath);
            updater.CreateAndAddProject("C:/path/to/project.csproj");

            return miscProject;
        });

        // Act
        var projects = await snapshotResolver.TryResolveAllProjectsAsync(documentFilePath, DisposalToken);

        // Assert
        var project = Assert.Single(projects);
        AssertSnapshotsEqual(miscProject, project);
    }

    [OSSkipConditionFact(["OSX", "Linux"])]
    public async Task TryResolveAllProjects_OwnerProjectDifferentCasing_ReturnsTrue()
    {
        // Arrange
        var documentFilePath = "c:/path/to/document.cshtml";
        var projectManager = CreateProjectSnapshotManager();
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        var ownerProject = await projectManager.UpdateAsync(updater =>
        {
            var ownerProject = updater.CreateAndAddProject("C:/Path/To/project.csproj");
            updater.CreateAndAddDocument(ownerProject, documentFilePath);

            return ownerProject;
        });

        // Act
        var projects = await snapshotResolver.TryResolveAllProjectsAsync(documentFilePath, DisposalToken);

        // Assert
        var project = Assert.Single(projects);
        AssertSnapshotsEqual(ownerProject, project);
    }

    [Fact]
    public async Task GetMiscellaneousProject_ProjectLoaded_ReturnsExistingProject()
    {
        // Arrange
        var projectManager = CreateProjectSnapshotManager();
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        // Act
        var project = await snapshotResolver.GetMiscellaneousProjectAsync(DisposalToken);
        var inManager = projectManager.GetLoadedProject(snapshotResolver.MiscellaneousHostProject.Key);

        // Assert
        Assert.Same(inManager, project);
    }

    [Fact]
    public async Task GetMiscellaneousProject_ProjectNotLoaded_CreatesProjectAndReturnsCreatedProject()
    {
        // Arrange
        var projectManager = CreateProjectSnapshotManager();
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        // Act
        var project = await snapshotResolver.GetMiscellaneousProjectAsync(DisposalToken);

        // Assert
        Assert.Single(projectManager.GetProjects());
        Assert.Equal(snapshotResolver.MiscellaneousHostProject.FilePath, project.FilePath);
    }

    private async Task<SnapshotResolver> CreateSnapshotResolverAsync(string filePath, bool addToMiscellaneous = false)
    {
        filePath = FilePathNormalizer.Normalize(filePath);

        var projectManager = CreateProjectSnapshotManager();
        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);

        if (addToMiscellaneous)
        {
            await projectManager.UpdateAsync(async updater =>
            {
                var miscProject = (ProjectSnapshot)await snapshotResolver.GetMiscellaneousProjectAsync(DisposalToken);
                updater.CreateAndAddDocument(miscProject, filePath);
            });
        }
        else
        {
            var projectDirectory = FilePathNormalizer.GetNormalizedDirectoryName(filePath);
            var projectSnapshot = TestProjectSnapshot.Create(Path.Combine(projectDirectory, "proj.csproj"));

            await projectManager.UpdateAsync(updater =>
            {
                updater.ProjectAdded(projectSnapshot.HostProject);
                updater.CreateAndAddDocument(projectSnapshot, filePath);
            });
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
