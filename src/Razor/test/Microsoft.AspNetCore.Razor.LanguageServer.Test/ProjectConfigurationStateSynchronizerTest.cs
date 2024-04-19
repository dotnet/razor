// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Serialization;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

public class ProjectConfigurationStateSynchronizerTest(ITestOutputHelper testOutput) : LanguageServerTestBase(testOutput)
{
    [Fact]
    public async Task ProjectConfigurationFileChanged_Removed_UnknownDocumentNoops()
    {
        // Arrange
        var projectService = new Mock<IRazorProjectService>(MockBehavior.Strict);

        using var synchronizer = GetSynchronizer(projectService.Object);
        var synchronizerAccessor = synchronizer.GetTestAccessor();

        var deserializerMock = new Mock<IRazorProjectInfoDeserializer>(MockBehavior.Strict);
        var args = new ProjectConfigurationFileChangeEventArgs(
            configurationFilePath: "/path/to/project.razor.bin",
            kind: RazorFileChangeKind.Removed,
            projectInfoDeserializer: deserializerMock.Object);

        // Act
        synchronizer.ProjectConfigurationFileChanged(args);

        await synchronizerAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        projectService.VerifyAll();
    }

    [Fact]
    public async Task ProjectConfigurationFileChanged_Removed_NonNormalizedPaths()
    {
        // Arrange
        var projectInfo = new RazorProjectInfo(
            "/path/to/obj/project.razor.bin",
            "path/to/project.csproj",
            RazorConfiguration.Default,
            rootNamespace: "TestRootNamespace",
            displayName: "project",
            ProjectWorkspaceState.Create(LanguageVersion.CSharp5),
            ImmutableArray<DocumentSnapshotHandle>.Empty);
        var intermediateOutputPath = FilePathNormalizer.GetNormalizedDirectoryName(projectInfo.SerializedFilePath);
        var projectKey = TestProjectKey.Create(intermediateOutputPath);
        var projectService = new StrictMock<IRazorProjectService>();
        projectService
            .Setup(x => x.AddProjectAsync(
                projectInfo.FilePath,
                intermediateOutputPath,
                It.IsAny<RazorConfiguration>(),
                projectInfo.RootNamespace,
                projectInfo.DisplayName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(projectKey);
        projectService
            .Setup(x => x.UpdateProjectAsync(
                projectKey,
                It.IsAny<RazorConfiguration>(),
                projectInfo.RootNamespace,
                projectInfo.DisplayName,
                projectInfo.ProjectWorkspaceState,
                projectInfo.Documents,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();
        projectService
            .Setup(x => x.UpdateProjectAsync(
                projectKey,
                null,
                null,
                "",
                ProjectWorkspaceState.Default,
                ImmutableArray<DocumentSnapshotHandle>.Empty,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        using var synchronizer = GetSynchronizer(projectService.Object);
        var synchronizerAccessor = synchronizer.GetTestAccessor();

        var jsonFileDeserializer = CreateDeserializer(projectInfo);
        var addArgs = new ProjectConfigurationFileChangeEventArgs("/path/to\\obj/project.razor.bin", RazorFileChangeKind.Added, jsonFileDeserializer);

        synchronizer.ProjectConfigurationFileChanged(addArgs);

        await synchronizerAccessor.WaitUntilCurrentBatchCompletesAsync();

        var removeArgs = new ProjectConfigurationFileChangeEventArgs(
            configurationFilePath: "/path/to/obj/project.razor.bin",
            kind: RazorFileChangeKind.Removed,
            projectInfoDeserializer: Mock.Of<IRazorProjectInfoDeserializer>(MockBehavior.Strict));

        // Act
        synchronizer.ProjectConfigurationFileChanged(removeArgs);

        await synchronizerAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        projectService.VerifyAll();
    }

    [Fact]
    public async Task ProjectConfigurationFileChanged_Added_CantDeserialize_Noops()
    {
        // Arrange
        var projectService = new Mock<IRazorProjectService>(MockBehavior.Strict);

        using var synchronizer = GetSynchronizer(projectService.Object);
        var synchronizerAccessor = synchronizer.GetTestAccessor();

        var deserializerMock = new Mock<IRazorProjectInfoDeserializer>(MockBehavior.Strict);
        deserializerMock
            .Setup(x => x.DeserializeFromFile(It.IsAny<string>()))
            .Returns((RazorProjectInfo)null);

        var args = new ProjectConfigurationFileChangeEventArgs(
            configurationFilePath: "/path/to/project.razor.bin",
            kind: RazorFileChangeKind.Added,
            projectInfoDeserializer: deserializerMock.Object);

        // Act
        synchronizer.ProjectConfigurationFileChanged(args);

        await synchronizerAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        projectService.VerifyAll();
    }

    [Fact]
    public async Task ProjectConfigurationFileChanged_Added_AddAndUpdatesProject()
    {
        // Arrange
        var projectInfo = new RazorProjectInfo(
            "/path/to/obj/project.razor.bin",
            "path/to/project.csproj",
            RazorConfiguration.Default,
            rootNamespace: "TestRootNamespace",
            displayName: "project",
            ProjectWorkspaceState.Create(LanguageVersion.CSharp5),
            ImmutableArray<DocumentSnapshotHandle>.Empty);
        var intermediateOutputPath = FilePathNormalizer.GetNormalizedDirectoryName(projectInfo.SerializedFilePath);
        var projectKey = TestProjectKey.Create(intermediateOutputPath);
        var projectService = new StrictMock<IRazorProjectService>();
        projectService
            .Setup(service => service.AddProjectAsync(
                projectInfo.FilePath,
                intermediateOutputPath,
                It.IsAny<RazorConfiguration>(),
                projectInfo.RootNamespace,
                projectInfo.DisplayName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(projectKey);
        projectService
            .Setup(service => service.UpdateProjectAsync(
                projectKey,
                It.IsAny<RazorConfiguration>(),
                projectInfo.RootNamespace,
                projectInfo.DisplayName,
                projectInfo.ProjectWorkspaceState,
                projectInfo.Documents, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        using var synchronizer = GetSynchronizer(projectService.Object);
        var synchronizerAccessor = synchronizer.GetTestAccessor();

        var jsonFileDeserializer = CreateDeserializer(projectInfo);
        var args = new ProjectConfigurationFileChangeEventArgs("/path/to/obj/project.razor.bin", RazorFileChangeKind.Added, jsonFileDeserializer);

        // Act
        synchronizer.ProjectConfigurationFileChanged(args);

        await synchronizerAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        projectService.VerifyAll();
    }

    [Fact]
    public async Task ProjectConfigurationFileChanged_Removed_ResetsProject()
    {
        // Arrange
        var projectInfo = new RazorProjectInfo(
            "/path/to/obj/project.razor.bin",
            "path/to/project.csproj",
            RazorConfiguration.Default,
            rootNamespace: "TestRootNamespace",
            displayName: "project",
            ProjectWorkspaceState.Create(LanguageVersion.CSharp5),
            ImmutableArray<DocumentSnapshotHandle>.Empty);
        var intermediateOutputPath = FilePathNormalizer.GetNormalizedDirectoryName(projectInfo.SerializedFilePath);
        var projectKey = TestProjectKey.Create(intermediateOutputPath);
        var projectService = new StrictMock<IRazorProjectService>();
        projectService
            .Setup(service => service.AddProjectAsync(
                projectInfo.FilePath,
                intermediateOutputPath,
                It.IsAny<RazorConfiguration>(),
                projectInfo.RootNamespace,
                projectInfo.DisplayName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(projectKey);
        projectService
            .Setup(service => service.UpdateProjectAsync(
                projectKey,
                It.IsAny<RazorConfiguration>(),
                projectInfo.RootNamespace,
                projectInfo.DisplayName,
                projectInfo.ProjectWorkspaceState,
                projectInfo.Documents,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();
        projectService
            .Setup(service => service.UpdateProjectAsync(
                projectKey,
                null,
                null,
                "",
                ProjectWorkspaceState.Default,
                ImmutableArray<DocumentSnapshotHandle>.Empty,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        using var synchronizer = GetSynchronizer(projectService.Object);
        var synchronizerAccessor = synchronizer.GetTestAccessor();

        var deserializer = CreateDeserializer(projectInfo);
        var addArgs = new ProjectConfigurationFileChangeEventArgs("/path/to/obj/project.razor.bin", RazorFileChangeKind.Added, deserializer);

        synchronizer.ProjectConfigurationFileChanged(addArgs);

        await synchronizerAccessor.WaitUntilCurrentBatchCompletesAsync();

        var removeArgs = new ProjectConfigurationFileChangeEventArgs(
            configurationFilePath: "/path/to/obj/project.razor.bin",
            kind: RazorFileChangeKind.Removed,
            projectInfoDeserializer: Mock.Of<IRazorProjectInfoDeserializer>(MockBehavior.Strict));

        // Act
        synchronizer.ProjectConfigurationFileChanged(removeArgs);

        await synchronizerAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        projectService.VerifyAll();
    }

    [Fact]
    public async Task ProjectConfigurationFileChanged_Changed_UpdatesProject()
    {
        // Arrange
        var initialProjectInfo = new RazorProjectInfo(
            "/path/to/obj/project.razor.bin",
            "path/to/project.csproj",
            RazorConfiguration.Default,
            rootNamespace: "TestRootNamespace",
            displayName: "project",
            ProjectWorkspaceState.Create(LanguageVersion.CSharp5),
            ImmutableArray<DocumentSnapshotHandle>.Empty);
        var intermediateOutputPath = FilePathNormalizer.GetNormalizedDirectoryName(initialProjectInfo.SerializedFilePath);
        var projectKey = TestProjectKey.Create(intermediateOutputPath);
        var projectService = new StrictMock<IRazorProjectService>();
        projectService
            .Setup(service => service.AddProjectAsync(
                initialProjectInfo.FilePath,
                intermediateOutputPath,
                It.IsAny<RazorConfiguration>(),
                initialProjectInfo.RootNamespace,
                initialProjectInfo.DisplayName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(projectKey);
        projectService
            .Setup(service => service.UpdateProjectAsync(
                projectKey,
                It.IsAny<RazorConfiguration>(),
                initialProjectInfo.RootNamespace,
                initialProjectInfo.DisplayName,
                initialProjectInfo.ProjectWorkspaceState,
                initialProjectInfo.Documents,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();
        var changedProjectInfo = new RazorProjectInfo(
            "/path/to/obj/project.razor.bin",
            "path/to/project.csproj",
            new(RazorLanguageVersion.Experimental,
                "TestConfiguration",
                Extensions: []),
            rootNamespace: "TestRootNamespace2",
            displayName: "project",
            ProjectWorkspaceState.Create(LanguageVersion.CSharp6),
            ImmutableArray<DocumentSnapshotHandle>.Empty);
        projectService
            .Setup(service => service.UpdateProjectAsync(
                projectKey,
                It.IsAny<RazorConfiguration>(),
                changedProjectInfo.RootNamespace,
                changedProjectInfo.DisplayName,
                changedProjectInfo.ProjectWorkspaceState,
                changedProjectInfo.Documents,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        using var synchronizer = GetSynchronizer(projectService.Object);
        var synchronizerAccessor = synchronizer.GetTestAccessor();

        var addDeserializer = CreateDeserializer(initialProjectInfo);
        var addArgs = new ProjectConfigurationFileChangeEventArgs(
            "/path/to/obj/project.razor.bin",
            RazorFileChangeKind.Added,
            addDeserializer);

        synchronizer.ProjectConfigurationFileChanged(addArgs);

        await synchronizerAccessor.WaitUntilCurrentBatchCompletesAsync();

        var changedDeserializer = CreateDeserializer(changedProjectInfo);
        var changedArgs = new ProjectConfigurationFileChangeEventArgs(
            "/path/to/obj/project.razor.bin",
            RazorFileChangeKind.Changed,
            changedDeserializer);

        // Act
        synchronizer.ProjectConfigurationFileChanged(changedArgs);

        await synchronizerAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        projectService.VerifyAll();
    }

    [Fact]
    public async Task ProjectConfigurationFileChanged_Changed_CantDeserialize_ResetsProject()
    {
        // Arrange
        var initialProjectInfo = new RazorProjectInfo(
            "/path/to/obj/project.razor.bin",
            "path/to/project.csproj",
            RazorConfiguration.Default,
            rootNamespace: "TestRootNamespace",
            displayName: "project",
            ProjectWorkspaceState.Create(LanguageVersion.CSharp5),
            ImmutableArray<DocumentSnapshotHandle>.Empty);
        var intermediateOutputPath = FilePathNormalizer.GetNormalizedDirectoryName(initialProjectInfo.SerializedFilePath);
        var projectKey = TestProjectKey.Create(intermediateOutputPath);
        var projectService = new StrictMock<IRazorProjectService>();
        projectService
            .Setup(service => service.AddProjectAsync(
                initialProjectInfo.FilePath,
                intermediateOutputPath,
                It.IsAny<RazorConfiguration>(),
                initialProjectInfo.RootNamespace,
                initialProjectInfo.DisplayName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(projectKey);
        projectService
            .Setup(service => service.UpdateProjectAsync(
                projectKey,
                It.IsAny<RazorConfiguration>(),
                initialProjectInfo.RootNamespace,
                initialProjectInfo.DisplayName,
                initialProjectInfo.ProjectWorkspaceState,
                initialProjectInfo.Documents,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();
        var changedProjectInfo = new RazorProjectInfo(
            "/path/to/obj/project.razor.bin",
            "path/to/project.csproj",
            new(RazorLanguageVersion.Experimental,
                "TestConfiguration",
                Extensions: []),
            rootNamespace: "TestRootNamespace2",
            displayName: "project",
            ProjectWorkspaceState.Create(LanguageVersion.CSharp6),
            ImmutableArray<DocumentSnapshotHandle>.Empty);

        // This is the request that happens when the server is reset
        projectService
            .Setup(service => service.UpdateProjectAsync(
                projectKey,
                null,
                null,
                "",
                ProjectWorkspaceState.Default,
                ImmutableArray<DocumentSnapshotHandle>.Empty,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        using var synchronizer = GetSynchronizer(projectService.Object);
        var synchronizerAccessor = synchronizer.GetTestAccessor();

        var addDeserializer = CreateDeserializer(initialProjectInfo);
        var addArgs = new ProjectConfigurationFileChangeEventArgs("/path/to/obj/project.razor.bin", RazorFileChangeKind.Added, addDeserializer);

        synchronizer.ProjectConfigurationFileChanged(addArgs);

        await synchronizerAccessor.WaitUntilCurrentBatchCompletesAsync();

        var changedDeserializerMock = new Mock<IRazorProjectInfoDeserializer>(MockBehavior.Strict);
        changedDeserializerMock
            .Setup(x => x.DeserializeFromFile(It.IsAny<string>()))
            .Returns((RazorProjectInfo)null);

        var changedArgs = new ProjectConfigurationFileChangeEventArgs(
            configurationFilePath: "/path/to/obj/project.razor.bin",
            kind: RazorFileChangeKind.Changed,
            projectInfoDeserializer: changedDeserializerMock.Object);

        // Act
        synchronizer.ProjectConfigurationFileChanged(changedArgs);

        await synchronizerAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        projectService.VerifyAll();
    }

    [Fact]
    public async Task ProjectConfigurationFileChanged_Changed_UntrackedProject_Noops()
    {
        // Arrange
        var projectService = new Mock<IRazorProjectService>(MockBehavior.Strict);

        using var synchronizer = GetSynchronizer(projectService.Object);
        var synchronizerAccessor = synchronizer.GetTestAccessor();

        var changedDeserializerMock = new Mock<IRazorProjectInfoDeserializer>(MockBehavior.Strict);
        changedDeserializerMock
            .Setup(x => x.DeserializeFromFile(It.IsAny<string>()))
            .Returns((RazorProjectInfo)null);

        var changedArgs = new ProjectConfigurationFileChangeEventArgs(
            configurationFilePath: "/path/to/project.razor.bin",
            kind: RazorFileChangeKind.Changed,
            projectInfoDeserializer: changedDeserializerMock.Object);

        // Act
        synchronizer.ProjectConfigurationFileChanged(changedArgs);

        await synchronizerAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        projectService.VerifyAll();
    }

    [Fact]
    public async Task ProjectConfigurationFileChanged_RemoveThenAdd_OnlyAdds()
    {
        // Arrange
        var projectInfo = new RazorProjectInfo(
            "/path/to/obj/project.razor.json",
            "path/to/project.csproj",
            RazorConfiguration.Default,
            rootNamespace: "TestRootNamespace",
            displayName: "project",
            ProjectWorkspaceState.Create(LanguageVersion.CSharp5),
            ImmutableArray<DocumentSnapshotHandle>.Empty);
        var intermediateOutputPath = FilePathNormalizer.GetNormalizedDirectoryName(projectInfo.SerializedFilePath);
        var projectKey = TestProjectKey.Create(intermediateOutputPath);
        var projectService = new StrictMock<IRazorProjectService>();
        projectService
            .Setup(service => service.AddProjectAsync(
                projectInfo.FilePath,
                intermediateOutputPath,
                It.IsAny<RazorConfiguration>(),
                projectInfo.RootNamespace,
                projectInfo.DisplayName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(projectKey);
        projectService
            .Setup(p => p.UpdateProjectAsync(
                projectKey,
                It.IsAny<RazorConfiguration>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ProjectWorkspaceState>(),
                It.IsAny<ImmutableArray<DocumentSnapshotHandle>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using var synchronizer = GetSynchronizer(projectService.Object);
        var synchronizerAccessor = synchronizer.GetTestAccessor();

        var changedDeserializer = CreateDeserializer(projectInfo);
        var removedArgs = new ProjectConfigurationFileChangeEventArgs(projectInfo.SerializedFilePath, RazorFileChangeKind.Removed, changedDeserializer);
        var addedArgs = new ProjectConfigurationFileChangeEventArgs(projectInfo.SerializedFilePath, RazorFileChangeKind.Added, changedDeserializer);
        var changedArgs = new ProjectConfigurationFileChangeEventArgs(projectInfo.SerializedFilePath, RazorFileChangeKind.Changed, changedDeserializer);

        // Act
        synchronizer.ProjectConfigurationFileChanged(addedArgs);
        synchronizer.ProjectConfigurationFileChanged(changedArgs);

        await synchronizerAccessor.WaitUntilCurrentBatchCompletesAsync();

        // Assert
        projectService.Verify(p => p.UpdateProjectAsync(
            projectKey,
            It.IsAny<RazorConfiguration>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<ProjectWorkspaceState>(),
            It.IsAny<ImmutableArray<DocumentSnapshotHandle>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        projectService.VerifyAll();
    }

    private TestProjectConfigurationStateSynchronizer GetSynchronizer(IRazorProjectService razorProjectService)
        => new(razorProjectService, LoggerFactory, TestLanguageServerFeatureOptions.Instance, TimeSpan.FromMilliseconds(5));

    private static IRazorProjectInfoDeserializer CreateDeserializer(RazorProjectInfo projectInfo)
    {
        var deserializer = new Mock<IRazorProjectInfoDeserializer>(MockBehavior.Strict);
        deserializer
            .Setup(x => x.DeserializeFromFile(It.IsAny<string>()))
            .Returns(projectInfo);

        return deserializer.Object;
    }

    private sealed class TestProjectConfigurationStateSynchronizer(
        IRazorProjectService projectService,
        ILoggerFactory loggerFactory,
        LanguageServerFeatureOptions options,
        TimeSpan delay)
        : ProjectConfigurationStateSynchronizer(projectService, loggerFactory, options, delay);
}
