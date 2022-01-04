﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Serialization;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Serialization;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class ProjectConfigurationStateSynchronizerTest : LanguageServerTestBase
    {
        [Fact]
        public void ProjectConfigurationFileChanged_Removed_UnknownDocumentNoops()
        {
            // Arrange
            var projectService = new Mock<RazorProjectService>(MockBehavior.Strict);
            var synchronizer = GetSynchronizer(projectService.Object);
            var jsonFileDeserializer = Mock.Of<JsonFileDeserializer>(MockBehavior.Strict);
            var args = new ProjectConfigurationFileChangeEventArgs("/path/to/project.razor.json", RazorFileChangeKind.Removed, jsonFileDeserializer);

            // Act
            synchronizer.ProjectConfigurationFileChanged(args);

            // Assert
            projectService.VerifyAll();
        }

        [Fact]
        public void ProjectConfigurationFileChanged_Removed_NonNormalizedPaths()
        {
            // Arrange
            var projectRazorJson = new ProjectRazorJson(
                "/path/to/obj/project.razor.json",
                "path/to/project.csproj",
                RazorConfiguration.Default,
                rootNamespace: "TestRootNamespace",
                new ProjectWorkspaceState(Array.Empty<TagHelperDescriptor>(), LanguageVersion.CSharp5),
                Array.Empty<DocumentSnapshotHandle>());
            var projectService = new Mock<RazorProjectService>(MockBehavior.Strict);
            projectService.Setup(service => service.AddProject(projectRazorJson.FilePath)).Verifiable();
            projectService.Setup(service => service.UpdateProject(
                projectRazorJson.FilePath,
                projectRazorJson.Configuration,
                projectRazorJson.RootNamespace,
                projectRazorJson.ProjectWorkspaceState,
                projectRazorJson.Documents)).Verifiable();
            projectService.Setup(service => service.UpdateProject(
                 projectRazorJson.FilePath,
                 null,
                 null,
                 ProjectWorkspaceState.Default,
                 Array.Empty<DocumentSnapshotHandle>())).Verifiable();
            var synchronizer = GetSynchronizer(projectService.Object);
            var jsonFileDeserializer = CreateJsonFileDeserializer(projectRazorJson);
            var addArgs = new ProjectConfigurationFileChangeEventArgs("/path/to\\obj/project.razor.json", RazorFileChangeKind.Added, jsonFileDeserializer);
            synchronizer.ProjectConfigurationFileChanged(addArgs);
            WaitForEnqueueAsync(synchronizer).Wait();
            var removeArgs = new ProjectConfigurationFileChangeEventArgs("/path/to/obj/project.razor.json", RazorFileChangeKind.Removed, Mock.Of<JsonFileDeserializer>(MockBehavior.Strict));

            // Act
            synchronizer.ProjectConfigurationFileChanged(removeArgs);
            WaitForEnqueueAsync(synchronizer).Wait();

            // Assert
            projectService.VerifyAll();
        }

        [Fact]
        public void ProjectConfigurationFileChanged_Added_CantDeserialize_Noops()
        {
            // Arrange
            var projectService = new Mock<RazorProjectService>(MockBehavior.Strict);
            var synchronizer = GetSynchronizer(projectService.Object);
            var jsonFileDeserializer = Mock.Of<JsonFileDeserializer>(d => d.Deserialize<ProjectRazorJson>(It.IsAny<string>()) == null, MockBehavior.Strict);
            var args = new ProjectConfigurationFileChangeEventArgs("/path/to/project.razor.json", RazorFileChangeKind.Added, jsonFileDeserializer);

            // Act
            synchronizer.ProjectConfigurationFileChanged(args);

            // Assert
            projectService.VerifyAll();
        }

        [Fact]
        public void ProjectConfigurationFileChanged_Added_AddAndUpdatesProject()
        {
            // Arrange
            var projectRazorJson = new ProjectRazorJson(
                "/path/to/obj/project.razor.json",
                "path/to/project.csproj",
                RazorConfiguration.Default,
                rootNamespace: "TestRootNamespace",
                new ProjectWorkspaceState(Array.Empty<TagHelperDescriptor>(), LanguageVersion.CSharp5),
                Array.Empty<DocumentSnapshotHandle>());
            var projectService = new Mock<RazorProjectService>(MockBehavior.Strict);
            projectService.Setup(service => service.AddProject(projectRazorJson.FilePath)).Verifiable();
            projectService.Setup(service => service.UpdateProject(
                projectRazorJson.FilePath,
                projectRazorJson.Configuration,
                projectRazorJson.RootNamespace,
                projectRazorJson.ProjectWorkspaceState,
                projectRazorJson.Documents)).Verifiable();
            var synchronizer = GetSynchronizer(projectService.Object);
            var jsonFileDeserializer = CreateJsonFileDeserializer(projectRazorJson);
            var args = new ProjectConfigurationFileChangeEventArgs("/path/to/obj/project.razor.json", RazorFileChangeKind.Added, jsonFileDeserializer);

            // Act
            synchronizer.ProjectConfigurationFileChanged(args);
            WaitForEnqueueAsync(synchronizer).Wait();

            // Assert
            projectService.VerifyAll();
        }

        [Fact]
        public void ProjectConfigurationFileChanged_Removed_ResetsProject()
        {
            // Arrange
            var projectRazorJson = new ProjectRazorJson(
                "/path/to/obj/project.razor.json",
                "path/to/project.csproj",
                RazorConfiguration.Default,
                rootNamespace: "TestRootNamespace",
                new ProjectWorkspaceState(Array.Empty<TagHelperDescriptor>(), LanguageVersion.CSharp5),
                Array.Empty<DocumentSnapshotHandle>());
            var projectService = new Mock<RazorProjectService>(MockBehavior.Strict);
            projectService.Setup(service => service.AddProject(projectRazorJson.FilePath)).Verifiable();
            projectService.Setup(service => service.UpdateProject(
                projectRazorJson.FilePath,
                projectRazorJson.Configuration,
                projectRazorJson.RootNamespace,
                projectRazorJson.ProjectWorkspaceState,
                projectRazorJson.Documents)).Verifiable();
            projectService.Setup(service => service.UpdateProject(
                 projectRazorJson.FilePath,
                 null,
                 null,
                 ProjectWorkspaceState.Default,
                 Array.Empty<DocumentSnapshotHandle>())).Verifiable();
            var synchronizer = GetSynchronizer(projectService.Object);
            var jsonFileDeserializer = CreateJsonFileDeserializer(projectRazorJson);
            var addArgs = new ProjectConfigurationFileChangeEventArgs("/path/to/obj/project.razor.json", RazorFileChangeKind.Added, jsonFileDeserializer);
            synchronizer.ProjectConfigurationFileChanged(addArgs);
            WaitForEnqueueAsync(synchronizer).Wait();
            var removeArgs = new ProjectConfigurationFileChangeEventArgs("/path/to/obj/project.razor.json", RazorFileChangeKind.Removed, Mock.Of<JsonFileDeserializer>(MockBehavior.Strict));

            // Act
            synchronizer.ProjectConfigurationFileChanged(removeArgs);
            WaitForEnqueueAsync(synchronizer).Wait();

            // Assert
            projectService.VerifyAll();
        }

        [Fact]
        public void ProjectConfigurationFileChanged_Changed_UpdatesProject()
        {
            // Arrange
            var initialProjectRazorJson = new ProjectRazorJson(
                "/path/to/obj/project.razor.json",
                "path/to/project.csproj",
                RazorConfiguration.Default,
                rootNamespace: "TestRootNamespace",
                new ProjectWorkspaceState(Array.Empty<TagHelperDescriptor>(), LanguageVersion.CSharp5),
                Array.Empty<DocumentSnapshotHandle>());
            var projectService = new Mock<RazorProjectService>(MockBehavior.Strict);
            projectService.Setup(service => service.AddProject(initialProjectRazorJson.FilePath)).Verifiable();
            projectService.Setup(service => service.UpdateProject(
                initialProjectRazorJson.FilePath,
                initialProjectRazorJson.Configuration,
                initialProjectRazorJson.RootNamespace,
                initialProjectRazorJson.ProjectWorkspaceState,
                initialProjectRazorJson.Documents)).Verifiable();
            var changedProjectRazorJson = new ProjectRazorJson(
                "/path/to/obj/project.razor.json",
                "path/to/project.csproj",
                RazorConfiguration.Create(
                    RazorLanguageVersion.Experimental,
                    "TestConfiguration",
                    Array.Empty<RazorExtension>()),
                rootNamespace: "TestRootNamespace2",
                new ProjectWorkspaceState(Array.Empty<TagHelperDescriptor>(), LanguageVersion.CSharp6),
                Array.Empty<DocumentSnapshotHandle>());
            projectService.Setup(service => service.UpdateProject(
                changedProjectRazorJson.FilePath,
                changedProjectRazorJson.Configuration,
                changedProjectRazorJson.RootNamespace,
                changedProjectRazorJson.ProjectWorkspaceState,
                changedProjectRazorJson.Documents)).Verifiable();
            var synchronizer = GetSynchronizer(projectService.Object);
            var addDeserializer = CreateJsonFileDeserializer(initialProjectRazorJson);
            var addArgs = new ProjectConfigurationFileChangeEventArgs("path/to/obj/project.razor.json", RazorFileChangeKind.Added, addDeserializer);
            synchronizer.ProjectConfigurationFileChanged(addArgs);

            WaitForEnqueueAsync(synchronizer).Wait();

            var changedDeserializer = CreateJsonFileDeserializer(changedProjectRazorJson);
            var changedArgs = new ProjectConfigurationFileChangeEventArgs("path/to/obj/project.razor.json", RazorFileChangeKind.Changed, changedDeserializer);

            // Act
            synchronizer.ProjectConfigurationFileChanged(changedArgs);
            WaitForEnqueueAsync(synchronizer).Wait();

            // Assert
            projectService.VerifyAll();
        }

        [Fact]
        public void ProjectConfigurationFileChanged_Changed_CantDeserialize_ResetsProject()
        {
            // Arrange
            var initialProjectRazorJson = new ProjectRazorJson(
                "/path/to/obj/project.razor.json",
                "path/to/project.csproj",
                RazorConfiguration.Default,
                rootNamespace: "TestRootNamespace",
                new ProjectWorkspaceState(Array.Empty<TagHelperDescriptor>(), LanguageVersion.CSharp5),
                Array.Empty<DocumentSnapshotHandle>());
            var projectService = new Mock<RazorProjectService>(MockBehavior.Strict);
            projectService.Setup(service => service.AddProject(initialProjectRazorJson.FilePath)).Verifiable();
            projectService.Setup(service => service.UpdateProject(
                initialProjectRazorJson.FilePath,
                initialProjectRazorJson.Configuration,
                initialProjectRazorJson.RootNamespace,
                initialProjectRazorJson.ProjectWorkspaceState,
                initialProjectRazorJson.Documents)).Verifiable();
            var changedProjectRazorJson = new ProjectRazorJson(
                "/path/to/obj/project.razor.json",
                "path/to/project.csproj",
                RazorConfiguration.Create(
                    RazorLanguageVersion.Experimental,
                    "TestConfiguration",
                    Array.Empty<RazorExtension>()),
                rootNamespace: "TestRootNamespace2",
                new ProjectWorkspaceState(Array.Empty<TagHelperDescriptor>(), LanguageVersion.CSharp6),
                Array.Empty<DocumentSnapshotHandle>());

            // This is the request that happens when the server is reset
            projectService.Setup(service => service.UpdateProject(
                 initialProjectRazorJson.FilePath,
                 null,
                 null,
                 ProjectWorkspaceState.Default,
                 Array.Empty<DocumentSnapshotHandle>())).Verifiable();
            var synchronizer = GetSynchronizer(projectService.Object);
            var addDeserializer = CreateJsonFileDeserializer(initialProjectRazorJson);
            var addArgs = new ProjectConfigurationFileChangeEventArgs("/path/to/obj/project.razor.json", RazorFileChangeKind.Added, addDeserializer);
            synchronizer.ProjectConfigurationFileChanged(addArgs);
            WaitForEnqueueAsync(synchronizer).Wait();
            var changedDeserializer = Mock.Of<JsonFileDeserializer>(d => d.Deserialize<ProjectRazorJson>(It.IsAny<string>()) == null, MockBehavior.Strict);
            var changedArgs = new ProjectConfigurationFileChangeEventArgs("/path/to/obj/project.razor.json", RazorFileChangeKind.Changed, changedDeserializer);

            // Act
            synchronizer.ProjectConfigurationFileChanged(changedArgs);
            WaitForEnqueueAsync(synchronizer).Wait();

            // Assert
            projectService.VerifyAll();
        }

        [Fact]
        public void ProjectConfigurationFileChanged_Changed_UntrackedProject_Noops()
        {
            // Arrange
            var projectService = new Mock<RazorProjectService>(MockBehavior.Strict);
            var synchronizer = GetSynchronizer(projectService.Object);
            var changedDeserializer = Mock.Of<JsonFileDeserializer>(d => d.Deserialize<ProjectRazorJson>(It.IsAny<string>()) == null, MockBehavior.Strict);
            var changedArgs = new ProjectConfigurationFileChangeEventArgs("/path/to/project.razor.json", RazorFileChangeKind.Changed, changedDeserializer);

            // Act
            synchronizer.ProjectConfigurationFileChanged(changedArgs);
            WaitForEnqueueAsync(synchronizer, hasTask: false).Wait();

            // Assert
            projectService.VerifyAll();
        }

        [Fact]
        public void ProjectConfigurationFileChanged_RemoveThenAdd_OnlyAdds()
        {
            // Arrange
            var projectRazorJson = new ProjectRazorJson(
                "/path/to/obj/project.razor.json",
                "path/to/project.csproj",
                RazorConfiguration.Default,
                rootNamespace: "TestRootNamespace",
                new ProjectWorkspaceState(Array.Empty<TagHelperDescriptor>(), LanguageVersion.CSharp5),
                Array.Empty<DocumentSnapshotHandle>());

            var filePath = "path/to/project.csproj";
            var projectService = new Mock<RazorProjectService>(MockBehavior.Strict);
            projectService.Setup(service => service.AddProject(projectRazorJson.FilePath)).Verifiable();
            projectService.Setup(p => p.UpdateProject(
                filePath,
                It.IsAny<RazorConfiguration>(),
                It.IsAny<string>(),
                It.IsAny<ProjectWorkspaceState>(),
                It.IsAny<IReadOnlyList<DocumentSnapshotHandle>>()));

            var synchronizer = GetSynchronizer(projectService.Object);
            var changedDeserializer = CreateJsonFileDeserializer(projectRazorJson);
            var removedArgs = new ProjectConfigurationFileChangeEventArgs(projectRazorJson.SerializedFilePath, RazorFileChangeKind.Removed, changedDeserializer);
            var addedArgs = new ProjectConfigurationFileChangeEventArgs(projectRazorJson.SerializedFilePath, RazorFileChangeKind.Added, changedDeserializer);
            var changedArgs = new ProjectConfigurationFileChangeEventArgs(projectRazorJson.SerializedFilePath, RazorFileChangeKind.Changed, changedDeserializer);

            // Act
            synchronizer.ProjectConfigurationFileChanged(addedArgs);
            synchronizer.ProjectConfigurationFileChanged(changedArgs);
            WaitForEnqueueAsync(synchronizer).Wait();

            // Assert
            projectService.Verify(p => p.UpdateProject(
                filePath,
                It.IsAny<RazorConfiguration>(),
                It.IsAny<string>(),
                It.IsAny<ProjectWorkspaceState>(),
                It.IsAny<IReadOnlyList<DocumentSnapshotHandle>>()), Times.Once);

            projectService.VerifyAll();
        }

        private async Task WaitForEnqueueAsync(ProjectConfigurationStateSynchronizer synchronizer, bool hasTask = true)
        {
            if (hasTask)
            {
                var kvp = Assert.Single(synchronizer.ProjectInfoMap);
                await LegacyDispatcher.RunOnDispatcherThreadAsync(
                    () => kvp.Value.ProjectUpdateTask.Wait(), CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                Assert.Empty(synchronizer.ProjectInfoMap);
            }
        }

        private ProjectConfigurationStateSynchronizer GetSynchronizer(RazorProjectService razorProjectService)
        {
            var synchronizer = new ProjectConfigurationStateSynchronizer(LegacyDispatcher, razorProjectService, FilePathNormalizer, LoggerFactory);
            synchronizer.EnqueueDelay = 5;

            return synchronizer;
        }

        private static JsonFileDeserializer CreateJsonFileDeserializer(ProjectRazorJson deserializedHandle)
        {
            var deserializer = new Mock<JsonFileDeserializer>(MockBehavior.Strict);
            deserializer.Setup(deserializer => deserializer.Deserialize<ProjectRazorJson>(It.IsAny<string>()))
                .Returns(deserializedHandle);

            return deserializer.Object;
        }
    }
}
