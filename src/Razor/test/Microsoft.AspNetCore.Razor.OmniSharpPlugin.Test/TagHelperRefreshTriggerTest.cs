// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.OmniSharpPlugin.StrongNamed;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using Moq;
using OmniSharp.MSBuild.Logging;
using OmniSharp.MSBuild.Notification;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.AspNetCore.Razor.OmniSharpPlugin
{
    public class TagHelperRefreshTriggerTest : OmniSharpTestBase
    {
        public TagHelperRefreshTriggerTest()
        {
            Workspace = TestWorkspace.Create();
            var projectRoot1 = ProjectRootElement.Create("/path/to/project.csproj");
            Project1Instance = new ProjectInstance(projectRoot1);
            ProjectManager = CreateProjectSnapshotManager();
            Project1 = new OmniSharpHostProject(projectRoot1.ProjectFileLocation.File, RazorConfiguration.Default, "TestRootNamespace");

            var solution = Workspace.CurrentSolution.AddProject(
                ProjectInfo.Create(
                    ProjectId.CreateNewId(),
                    VersionStamp.Default,
                    "Project1",
                    "Project1",
                    LanguageNames.CSharp,
                    filePath: Project1.FilePath));
            Workspace.TryApplyChanges(solution);
        }

        public static TimeSpan WaitDelay
        {
            get
            {
                if (Debugger.IsAttached)
                {
                    return TimeSpan.MaxValue;
                }

                return TimeSpan.FromMilliseconds(3000);
            }
        }

        public Workspace Workspace { get; }

        internal OmniSharpProjectSnapshotManagerBase ProjectManager { get; }

        public OmniSharpHostProject Project1 { get; }

        public object Project1Instance { get; }

        [Fact]
        public async Task IsComponentFile_UnknownProject_ReturnsFalse()
        {
            // Arrange
            var projectManager = CreateProjectSnapshotManager();
            var refreshTrigger = CreateRefreshTrigger();
            refreshTrigger.Initialize(projectManager);

            // Act
            var result = await RunOnDispatcherThreadAsync(() => refreshTrigger.IsComponentFile("file.razor", "/path/to/project.csproj")).ConfigureAwait(false);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsComponentFile_UnknownDocument_ReturnsFalse()
        {
            // Arrange
            var projectManager = CreateProjectSnapshotManager();
            var refreshTrigger = CreateRefreshTrigger();
            refreshTrigger.Initialize(projectManager);
            var hostProject = new OmniSharpHostProject("/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
            await RunOnDispatcherThreadAsync(() => projectManager.ProjectAdded(hostProject)).ConfigureAwait(false);

            // Act
            var result = await RunOnDispatcherThreadAsync(() => refreshTrigger.IsComponentFile("file.razor", hostProject.FilePath)).ConfigureAwait(false);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsComponentFile_NonComponent_ReturnsFalse()
        {
            // Arrange
            var projectManager = CreateProjectSnapshotManager();
            var refreshTrigger = CreateRefreshTrigger();
            refreshTrigger.Initialize(projectManager);
            var hostProject = new OmniSharpHostProject("/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
            var hostDocument = new OmniSharpHostDocument("file.cshtml", "file.cshtml", FileKinds.Legacy);
            await RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(hostProject);
                projectManager.DocumentAdded(hostProject, hostDocument);
            }).ConfigureAwait(false);

            // Act
            var result = await RunOnDispatcherThreadAsync(() => refreshTrigger.IsComponentFile(hostDocument.FilePath, hostProject.FilePath)).ConfigureAwait(false);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsComponentFile_Component_ReturnsTrue()
        {
            // Arrange
            var projectManager = CreateProjectSnapshotManager();
            var refreshTrigger = CreateRefreshTrigger();
            refreshTrigger.Initialize(projectManager);
            var hostProject = new OmniSharpHostProject("/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
            var hostDocument = new OmniSharpHostDocument("file.cshtml", "file.cshtml", FileKinds.Component);
            await RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(hostProject);
                projectManager.DocumentAdded(hostProject, hostDocument);
            }).ConfigureAwait(false);

            // Act
            var result = await RunOnDispatcherThreadAsync(() => refreshTrigger.IsComponentFile(hostDocument.FilePath, hostProject.FilePath)).ConfigureAwait(false);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ProjectLoaded_TriggersUpdate()
        {
            // Arrange
            await RunOnDispatcherThreadAsync(() => ProjectManager.ProjectAdded(Project1)).ConfigureAwait(false);
            using var mre = new ManualResetEventSlim(initialState: false);
            var workspaceStateGenerator = new Mock<OmniSharpProjectWorkspaceStateGenerator>(MockBehavior.Strict);
            workspaceStateGenerator.Setup(generator => generator.Update(It.IsAny<Project>(), It.IsAny<OmniSharpProjectSnapshot>()))
                .Callback<Project, OmniSharpProjectSnapshot>((_1, _2) => mre.Set());
            var refreshTrigger = CreateRefreshTrigger(workspaceStateGenerator.Object);
            var args = new ProjectLoadedEventArgs(
                id: null,
                project: null,
                sessionId: Guid.NewGuid(),
                (ProjectInstance)Project1Instance,
                diagnostics: Enumerable.Empty<MSBuildDiagnostic>().ToImmutableArray(),
                isReload: false,
                projectIdIsDefinedInSolution: false,
                sourceFiles: Enumerable.Empty<string>().ToImmutableArray(),
                sdkVersion: default);

            // Act
            refreshTrigger.ProjectLoaded(args);

            // Assert
            var result = mre.Wait(WaitDelay);
            Assert.True(result);
        }

        [Fact]
        public async Task ProjectLoaded_BatchesUpdates()
        {
            // Arrange
            await RunOnDispatcherThreadAsync(() => ProjectManager.ProjectAdded(Project1)).ConfigureAwait(false);
            using var mre = new ManualResetEventSlim(initialState: false);
            var workspaceStateGenerator = new Mock<OmniSharpProjectWorkspaceStateGenerator>(MockBehavior.Strict);
            workspaceStateGenerator.Setup(generator => generator.Update(It.IsAny<Project>(), It.IsAny<OmniSharpProjectSnapshot>()))
                .Callback<Project, OmniSharpProjectSnapshot>((_1, _2) =>
                {
                    if (mre.IsSet)
                    {
                        throw new XunitException("Should not have been called twice.");
                    }

                    mre.Set();
                });
            var refreshTrigger = CreateRefreshTrigger(workspaceStateGenerator.Object, enqueueDelay: 10);
            var args = new ProjectLoadedEventArgs(
                id: null,
                project: null,
                sessionId: Guid.NewGuid(),
                (ProjectInstance)Project1Instance,
                diagnostics: Enumerable.Empty<MSBuildDiagnostic>().ToImmutableArray(),
                isReload: false,
                projectIdIsDefinedInSolution: false,
                sourceFiles: Enumerable.Empty<string>().ToImmutableArray(),
                sdkVersion: default);

            // Act
            refreshTrigger.ProjectLoaded(args);
            refreshTrigger.ProjectLoaded(args);
            refreshTrigger.ProjectLoaded(args);
            refreshTrigger.ProjectLoaded(args);

            // Assert
            var result = mre.Wait(WaitDelay);
            Assert.True(result);
        }

        [Fact]
        public async Task RazorDocumentOutputChanged_TriggersUpdate()
        {
            // Arrange
            await RunOnDispatcherThreadAsync(() => ProjectManager.ProjectAdded(Project1)).ConfigureAwait(false);
            using var mre = new ManualResetEventSlim(initialState: false);
            var workspaceStateGenerator = new Mock<OmniSharpProjectWorkspaceStateGenerator>(MockBehavior.Strict);
            workspaceStateGenerator.Setup(generator => generator.Update(It.IsAny<Project>(), It.IsAny<OmniSharpProjectSnapshot>()))
                .Callback<Project, OmniSharpProjectSnapshot>((_1, _2) => mre.Set());
            var refreshTrigger = CreateRefreshTrigger(workspaceStateGenerator.Object);
            var args = new RazorFileChangeEventArgs("/path/to/obj/file.cshtml.g.cs", (ProjectInstance)Project1Instance, RazorFileChangeKind.Added);

            // Act
            refreshTrigger.RazorDocumentOutputChanged(args);

            // Assert
            var result = mre.Wait(WaitDelay);
            Assert.True(result);
        }

        [Fact]
        public async Task RazorDocumentOutputChanged_BatchesUpdates()
        {
            // Arrange
            await RunOnDispatcherThreadAsync(() => ProjectManager.ProjectAdded(Project1)).ConfigureAwait(false);
            using var mre = new ManualResetEventSlim(initialState: false);
            var workspaceStateGenerator = new Mock<OmniSharpProjectWorkspaceStateGenerator>(MockBehavior.Strict);
            workspaceStateGenerator.Setup(generator => generator.Update(It.IsAny<Project>(), It.IsAny<OmniSharpProjectSnapshot>()))
                .Callback<Project, OmniSharpProjectSnapshot>((_1, _2) =>
                {
                    if (mre.IsSet)
                    {
                        throw new XunitException("Should not have been called twice.");
                    }

                    mre.Set();
                });
            var refreshTrigger = CreateRefreshTrigger(workspaceStateGenerator.Object, enqueueDelay: 10);
            var args = new RazorFileChangeEventArgs("/path/to/obj/file.cshtml.g.cs", (ProjectInstance)Project1Instance, RazorFileChangeKind.Added);

            // Act
            refreshTrigger.RazorDocumentOutputChanged(args);
            refreshTrigger.RazorDocumentOutputChanged(args);
            refreshTrigger.RazorDocumentOutputChanged(args);
            refreshTrigger.RazorDocumentOutputChanged(args);

            // Assert
            var result = mre.Wait(WaitDelay);
            Assert.True(result);
        }

        [Fact]
        public async Task UpdateAfterDelayAsync_NoWorkspaceProject_Noops()
        {
            // Arrange
            using var workspace = TestWorkspace.Create();
            var projectManager = CreateProjectSnapshotManager();
            await RunOnDispatcherThreadAsync(() => ProjectManager.ProjectAdded(Project1)).ConfigureAwait(false);
            var workspaceStateGenerator = new Mock<OmniSharpProjectWorkspaceStateGenerator>(MockBehavior.Strict);
            workspaceStateGenerator.Setup(generator => generator.Update(It.IsAny<Project>(), It.IsAny<OmniSharpProjectSnapshot>()))
                .Throws<XunitException>();
            var refreshTrigger = CreateRefreshTrigger(workspaceStateGenerator.Object, workspace);

            // Act & Assert
            await RunOnDispatcherThreadAsync(() => refreshTrigger.UpdateAfterDelayAsync(Project1.FilePath)).ConfigureAwait(false);
        }

        [Fact]
        public async Task UpdateAfterDelayAsync_NoProjectSnapshot_Noops()
        {
            // Arrange
            var projectManager = CreateProjectSnapshotManager();
            var workspaceStateGenerator = new Mock<OmniSharpProjectWorkspaceStateGenerator>(MockBehavior.Strict);
            workspaceStateGenerator.Setup(generator => generator.Update(It.IsAny<Project>(), It.IsAny<OmniSharpProjectSnapshot>()))
                .Throws<XunitException>();
            var refreshTrigger = CreateRefreshTrigger(workspaceStateGenerator.Object);

            // Act & Assert
            await RunOnDispatcherThreadAsync(() => refreshTrigger.UpdateAfterDelayAsync(((ProjectInstance)Project1Instance).ProjectFileLocation.File)).ConfigureAwait(false);
        }

        private TagHelperRefreshTrigger CreateRefreshTrigger(OmniSharpProjectWorkspaceStateGenerator workspaceStateGenerator = null, Workspace workspace = null, int enqueueDelay = 1)
        {
            workspaceStateGenerator ??= Mock.Of<OmniSharpProjectWorkspaceStateGenerator>(MockBehavior.Strict);
            workspace ??= Workspace;
            var refreshTrigger = new TagHelperRefreshTrigger(Dispatcher, workspace, workspaceStateGenerator)
            {
                EnqueueDelay = enqueueDelay,
            };

            refreshTrigger.Initialize(ProjectManager);

            return refreshTrigger;
        }
    }
}
