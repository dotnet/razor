﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.LanguageServices.Razor.Test;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    public class VsSolutionUpdatesProjectSnapshotChangeTriggerTest
    {
        private static JoinableTaskFactory JoinableTaskFactory
        {
            get
            {
                var joinableTaskContext = new JoinableTaskContextNode(new JoinableTaskContext());
                return new JoinableTaskFactory(joinableTaskContext.Context);
            }
        }

        private static readonly ProjectSnapshotManagerDispatcher Dispatcher = new TestProjectSnapshotManagerDispatcher();

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
                Dispatcher,
                JoinableTaskFactory.Context);

            // Act
            trigger.Initialize(Mock.Of<ProjectSnapshotManagerBase>(MockBehavior.Strict));

            // Assert
            buildManager.Verify();
        }

        [Fact]
        public async Task Initialize_SwitchesToMainThread()
        {
            // Arrange
            uint cookie;
            var buildManager = new Mock<IVsSolutionBuildManager>(MockBehavior.Strict);
            buildManager
                .Setup(b => b.AdviseUpdateSolutionEvents(It.IsAny<VsSolutionUpdatesProjectSnapshotChangeTrigger>(), out cookie))
                .Returns(VSConstants.S_OK);

            var context = JoinableTaskFactory.Context;

            var services = new Mock<IServiceProvider>(MockBehavior.Strict);
            services.Setup(s => s.GetService(It.Is<Type>(f => f == typeof(SVsSolutionBuildManager)))).Callback(() => Assert.True(context.IsOnMainThread)).Returns(buildManager.Object);

            var trigger = new VsSolutionUpdatesProjectSnapshotChangeTrigger(
                services.Object,
                Mock.Of<TextBufferProjectService>(MockBehavior.Strict),
                Mock.Of<ProjectWorkspaceStateGenerator>(MockBehavior.Strict),
                Dispatcher,
                context);

            await Task.Run(() =>
            {
                Assert.False(context.IsOnMainThread);
                trigger.Initialize(Mock.Of<ProjectSnapshotManagerBase>(MockBehavior.Strict));
                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task SolutionClosing_CancelsActiveWork()
        {
            // Arrange
            var projectManager = new TestProjectSnapshotManager(Workspace)
            {
                AllowNotifyListeners = true,
            };
            var expectedProjectPath = SomeProject.FilePath;
            var expectedProjectSnapshot = await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(SomeProject);
                projectManager.ProjectAdded(SomeOtherProject);

                return projectManager.GetLoadedProject(SomeProject.FilePath);
            }, CancellationToken.None);

            var projectService = new Mock<TextBufferProjectService>(MockBehavior.Strict);
            projectService.Setup(p => p.GetProjectPath(It.IsAny<IVsHierarchy>())).Returns(expectedProjectPath);
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();

            var trigger = new VsSolutionUpdatesProjectSnapshotChangeTrigger(TestServiceProvider.Instance, projectService.Object, workspaceStateGenerator, Dispatcher, JoinableTaskFactory.Context);
            trigger.Initialize(projectManager);
            trigger.UpdateProjectCfg_Done(Mock.Of<IVsHierarchy>(MockBehavior.Strict), pCfgProj: default, pCfgSln: default, dwAction: default, fSuccess: default, fCancel: default);
            await trigger.CurrentUpdateTaskForTests;

            // Act
            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                projectManager.SolutionClosed();
                projectManager.ProjectRemoved(SomeProject);
            }, CancellationToken.None);

            // Assert
            var update = Assert.Single(workspaceStateGenerator.UpdateQueue);
            Assert.Equal(update.WorkspaceProject.Id, SomeWorkspaceProject.Id);
            Assert.Same(expectedProjectSnapshot, update.ProjectSnapshot);
            Assert.True(update.CancellationToken.IsCancellationRequested);
        }

        [Fact]
        public async Task OnProjectBuiltAsync_KnownProject_EnqueuesProjectStateUpdate()
        {
            // Arrange
            var projectManager = new TestProjectSnapshotManager(Workspace);
            var expectedProjectPath = SomeProject.FilePath;
            var expectedProjectSnapshot = await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(SomeProject);
                projectManager.ProjectAdded(SomeOtherProject);

                return projectManager.GetLoadedProject(SomeProject.FilePath);
            }, CancellationToken.None);

            var projectService = new Mock<TextBufferProjectService>(MockBehavior.Strict);
            projectService.Setup(p => p.GetProjectPath(It.IsAny<IVsHierarchy>())).Returns(expectedProjectPath);
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();

            var trigger = new VsSolutionUpdatesProjectSnapshotChangeTrigger(TestServiceProvider.Instance, projectService.Object, workspaceStateGenerator, Dispatcher, JoinableTaskFactory.Context);
            trigger.Initialize(projectManager);

            // Act
            await trigger.OnProjectBuiltAsync(Mock.Of<IVsHierarchy>(MockBehavior.Strict), CancellationToken.None);

            // Assert
            var update = Assert.Single(workspaceStateGenerator.UpdateQueue);
            Assert.Equal(update.WorkspaceProject.Id, SomeWorkspaceProject.Id);
            Assert.Same(expectedProjectSnapshot, update.ProjectSnapshot);
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

            var trigger = new VsSolutionUpdatesProjectSnapshotChangeTrigger(services.Object, projectService.Object, workspaceStateGenerator, Dispatcher, JoinableTaskFactory.Context);
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

            var trigger = new VsSolutionUpdatesProjectSnapshotChangeTrigger(services.Object, projectService.Object, workspaceStateGenerator, Dispatcher, JoinableTaskFactory.Context);
            trigger.Initialize(projectManager.Object);

            // Act
            await trigger.OnProjectBuiltAsync(Mock.Of<IVsHierarchy>(MockBehavior.Strict), CancellationToken.None);

            // Assert
            Assert.Empty(workspaceStateGenerator.UpdateQueue);
        }

        private class TestServiceProvider : IServiceProvider
        {
            public static readonly TestServiceProvider Instance = new TestServiceProvider();

            private TestServiceProvider()
            {
            }

            public object GetService(Type serviceType)
            {
                return null;
            }
        }
    }
}
