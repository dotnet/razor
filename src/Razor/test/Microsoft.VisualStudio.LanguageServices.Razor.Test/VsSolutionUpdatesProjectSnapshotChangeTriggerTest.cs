﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.LanguageServices.Razor.Test;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    public class VsSolutionUpdatesProjectSnapshotChangeTriggerTest
    {
        private static readonly ProjectSnapshotManagerDispatcher Dispatcher = new DefaultProjectSnapshotManagerDispatcher();

        public VsSolutionUpdatesProjectSnapshotChangeTriggerTest()
        {
            SomeProject = new HostProject(TestProjectData.SomeProject.FilePath, FallbackRazorConfiguration.MVC_1_0, TestProjectData.SomeProject.RootNamespace);
            SomeOtherProject = new HostProject(TestProjectData.AnotherProject.FilePath, FallbackRazorConfiguration.MVC_2_0, TestProjectData.AnotherProject.RootNamespace);

            Workspace = TestWorkspace.Create(w => SomeWorkspaceProject = w.AddProject(ProjectInfo.Create(
                    ProjectId.CreateNewId(),
                    VersionStamp.Create(),
                    "SomeProject",
                    "SomeProject",
                    LanguageNames.CSharp,
                    filePath: SomeProject.FilePath)));
        }

        private HostProject SomeProject { get; }

        private HostProject SomeOtherProject { get; }

        private Project SomeWorkspaceProject { get; set; }

        private Workspace Workspace { get; }

        [Fact]
        public void Initialize_AttachesEventSink()
        {
            // Arrange
            uint cookie;
            var buildManager = new Mock<IVsSolutionBuildManager>(MockBehavior.Strict);
            buildManager
                .Setup(b => b.AdviseUpdateSolutionEvents(It.IsAny<VsSolutionUpdatesProjectSnapshotChangeTrigger>(), out cookie))
                .Returns(VSConstants.S_OK)
                .Verifiable();

            var services = new Mock<IServiceProvider>(MockBehavior.Strict);
            services.Setup(s => s.GetService(It.Is<Type>(f => f == typeof(SVsSolutionBuildManager)))).Returns(buildManager.Object);

            var trigger = new VsSolutionUpdatesProjectSnapshotChangeTrigger(
                services.Object,
                Mock.Of<TextBufferProjectService>(MockBehavior.Strict),
                Mock.Of<ProjectWorkspaceStateGenerator>(MockBehavior.Strict),
                Dispatcher);

            // Act
            trigger.Initialize(Mock.Of<ProjectSnapshotManagerBase>(MockBehavior.Strict));

            // Assert
            buildManager.Verify();
        }

        [Fact]
        public async Task OnProjectBuiltAsync_KnownProject_EnqueuesProjectStateUpdate()
        {
            // Arrange
            var expectedProjectPath = SomeProject.FilePath;

            uint cookie;
            var buildManager = new Mock<IVsSolutionBuildManager>(MockBehavior.Strict);
            buildManager
                .Setup(b => b.AdviseUpdateSolutionEvents(It.IsAny<VsSolutionUpdatesProjectSnapshotChangeTrigger>(), out cookie))
                .Returns(VSConstants.S_OK);

            var services = new Mock<IServiceProvider>(MockBehavior.Strict);
            services.Setup(s => s.GetService(It.Is<Type>(f => f == typeof(SVsSolutionBuildManager)))).Returns(buildManager.Object);

            var projectService = new Mock<TextBufferProjectService>(MockBehavior.Strict);
            projectService.Setup(p => p.GetProjectPath(It.IsAny<IVsHierarchy>())).Returns(expectedProjectPath);

            var projectSnapshots = new[]
            {
                new DefaultProjectSnapshot(ProjectState.Create(Workspace.Services, SomeProject)),
                new DefaultProjectSnapshot(ProjectState.Create(Workspace.Services, SomeOtherProject)),
            };

            var projectManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectManager.SetupGet(p => p.Workspace).Returns(Workspace);
            projectManager
                .Setup(p => p.GetLoadedProject(expectedProjectPath))
                .Returns(projectSnapshots[0]);
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();

            var trigger = new VsSolutionUpdatesProjectSnapshotChangeTrigger(services.Object, projectService.Object, workspaceStateGenerator, Dispatcher);
            trigger.Initialize(projectManager.Object);

            // Act
            await trigger.OnProjectBuiltAsync(Mock.Of<IVsHierarchy>(MockBehavior.Strict), CancellationToken.None);

            // Assert
            var (workspaceProject, projectSnapshot) = Assert.Single(workspaceStateGenerator.UpdateQueue);
            Assert.Equal(workspaceProject.Id, SomeWorkspaceProject.Id);
            Assert.Same(projectSnapshot, projectSnapshots[0]);
        }

        [Fact]
        public async Task OnProjectBuiltAsync_WithoutWorkspaceProject_DoesNotEnqueueUpdate()
        {
            // Arrange
            uint cookie;
            var buildManager = new Mock<IVsSolutionBuildManager>(MockBehavior.Strict);
            buildManager
                .Setup(b => b.AdviseUpdateSolutionEvents(It.IsAny<VsSolutionUpdatesProjectSnapshotChangeTrigger>(), out cookie))
                .Returns(VSConstants.S_OK);

            var services = new Mock<IServiceProvider>(MockBehavior.Strict);
            services.Setup(s => s.GetService(It.Is<Type>(f => f == typeof(SVsSolutionBuildManager)))).Returns(buildManager.Object);
            var projectSnapshot = new DefaultProjectSnapshot(
                ProjectState.Create(
                    Workspace.Services,
                    new HostProject("/Some/Unknown/Path.csproj", RazorConfiguration.Default, "Path")));
            var expectedProjectPath = projectSnapshot.FilePath;

            var projectService = new Mock<TextBufferProjectService>(MockBehavior.Strict);
            projectService.Setup(p => p.GetProjectPath(It.IsAny<IVsHierarchy>())).Returns(expectedProjectPath);

            var projectManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectManager.SetupGet(p => p.Workspace).Returns(Workspace);
            projectManager
                .Setup(p => p.GetLoadedProject(expectedProjectPath))
                .Returns(projectSnapshot);
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();

            var trigger = new VsSolutionUpdatesProjectSnapshotChangeTrigger(services.Object, projectService.Object, workspaceStateGenerator, Dispatcher);
            trigger.Initialize(projectManager.Object);

            // Act
            await trigger.OnProjectBuiltAsync(Mock.Of<IVsHierarchy>(MockBehavior.Strict), CancellationToken.None);

            // Assert
            Assert.Empty(workspaceStateGenerator.UpdateQueue);
        }

        [Fact]
        public async Task OnProjectBuiltAsync_UnknownProject_DoesNotEnqueueUpdate()
        {
            // Arrange
            var expectedProjectPath = "Path/To/Project";

            uint cookie;
            var buildManager = new Mock<IVsSolutionBuildManager>(MockBehavior.Strict);
            buildManager
                .Setup(b => b.AdviseUpdateSolutionEvents(It.IsAny<VsSolutionUpdatesProjectSnapshotChangeTrigger>(), out cookie))
                .Returns(VSConstants.S_OK);

            var services = new Mock<IServiceProvider>(MockBehavior.Strict);
            services.Setup(s => s.GetService(It.Is<Type>(f => f == typeof(SVsSolutionBuildManager)))).Returns(buildManager.Object);

            var projectService = new Mock<TextBufferProjectService>(MockBehavior.Strict);
            projectService.Setup(p => p.GetProjectPath(It.IsAny<IVsHierarchy>())).Returns(expectedProjectPath);

            var projectManager = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
            projectManager.SetupGet(p => p.Workspace).Returns(Workspace);
            projectManager
                .Setup(p => p.GetLoadedProject(expectedProjectPath))
                .Returns((ProjectSnapshot)null);
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();

            var trigger = new VsSolutionUpdatesProjectSnapshotChangeTrigger(services.Object, projectService.Object, workspaceStateGenerator, Dispatcher);
            trigger.Initialize(projectManager.Object);

            // Act
            await trigger.OnProjectBuiltAsync(Mock.Of<IVsHierarchy>(MockBehavior.Strict), CancellationToken.None);

            // Assert
            Assert.Empty(workspaceStateGenerator.UpdateQueue);
        }
    }
}
