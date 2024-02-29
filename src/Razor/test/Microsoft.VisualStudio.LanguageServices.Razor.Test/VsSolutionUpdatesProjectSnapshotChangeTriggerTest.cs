﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectEngineHost;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common.VisualStudio;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServices.Razor.Test;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.LanguageServices.Razor;

public class VsSolutionUpdatesProjectSnapshotChangeTriggerTest : ToolingTestBase
{
    private static readonly HostProject s_someProject = new(
        TestProjectData.SomeProject.FilePath,
        TestProjectData.SomeProject.IntermediateOutputPath,
        FallbackRazorConfiguration.MVC_1_0,
        TestProjectData.SomeProject.RootNamespace);
    private static readonly HostProject s_someOtherProject = new(
        TestProjectData.AnotherProject.FilePath,
        TestProjectData.AnotherProject.IntermediateOutputPath,
        FallbackRazorConfiguration.MVC_2_0,
        TestProjectData.AnotherProject.RootNamespace);

    private readonly Project _someWorkspaceProject;
    private readonly IWorkspaceProvider _workspaceProvider;

    public VsSolutionUpdatesProjectSnapshotChangeTriggerTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        Project? someWorkspaceProject = null;
        var workspace = TestWorkspace.Create(
            w =>
            {
                someWorkspaceProject = w.AddProject(
                    ProjectInfo.Create(
                        ProjectId.CreateNewId(),
                        VersionStamp.Create(),
                        "SomeProject",
                        "SomeProject",
                        LanguageNames.CSharp,
                        filePath: s_someProject.FilePath)
                    .WithCompilationOutputInfo(
                        new CompilationOutputInfo()
                            .WithAssemblyPath(Path.Combine(s_someProject.IntermediateOutputPath, "SomeProject.dll"))));
            });
        _someWorkspaceProject = someWorkspaceProject.AssumeNotNull();

        AddDisposable(workspace);

        var workspaceProviderMock = new Mock<IWorkspaceProvider>(MockBehavior.Strict);
        workspaceProviderMock
            .Setup(x => x.GetWorkspace())
            .Returns(workspace);

        _workspaceProvider = workspaceProviderMock.Object;
    }

    private protected override ProjectSnapshotManagerDispatcher CreateDispatcher()
        => new VisualStudioProjectSnapshotManagerDispatcher(ErrorReporter);

    [UIFact]
    public async Task Initialize_AttachesEventSink()
    {
        uint cookie = 42;
        var buildManagerMock = new StrictMock<IVsSolutionBuildManager>();
        buildManagerMock
            .Setup(b => b.AdviseUpdateSolutionEvents(It.IsAny<VsSolutionUpdatesProjectSnapshotChangeTrigger>(), out cookie))
            .Returns(VSConstants.S_OK)
            .Verifiable();
        buildManagerMock
            .Setup(b => b.UnadviseUpdateSolutionEvents(cookie))
            .Returns(VSConstants.S_OK)
            .Verifiable();

        var serviceProvider = VsMocks.CreateServiceProvider(b =>
            b.AddService<SVsSolutionBuildManager>(buildManagerMock.Object));

        // Note: We're careful to use a using statement with a block to allow
        // the call to UnadviseUpdateSolutionEvents() to be verified after disposal.
        using (var trigger = new VsSolutionUpdatesProjectSnapshotChangeTrigger(
            serviceProvider,
            StrictMock.Of<IProjectWorkspaceStateGenerator>(),
            _workspaceProvider,
            Dispatcher,
            JoinableTaskContext))
        {
            var testAccessor = trigger.GetTestAccessor();

            trigger.Initialize(StrictMock.Of<ProjectSnapshotManagerBase>());

            await testAccessor.InitializeTask.AssumeNotNull();
        }

        buildManagerMock.Verify();
    }

    [UIFact]
    public async Task Initialize_SwitchesToMainThread()
    {
        uint cookie = 42;
        var buildManagerMock = new StrictMock<IVsSolutionBuildManager>();
        buildManagerMock
            .Setup(b => b.AdviseUpdateSolutionEvents(It.IsAny<VsSolutionUpdatesProjectSnapshotChangeTrigger>(), out cookie))
            .Returns(VSConstants.S_OK)
            // When IVsSolutionBuildManager.AdviseUpdateSolutionEvents is called, we should have switched to the main thread.
            .Callback(() => Assert.True(JoinableTaskContext.IsOnMainThread));
        buildManagerMock
            .Setup(b => b.UnadviseUpdateSolutionEvents(cookie))
            .Returns(VSConstants.S_OK);

        var serviceProvider = VsMocks.CreateServiceProvider(b =>
            b.AddService<SVsSolutionBuildManager>(() =>
            {
                // When the IVsSolutionBuildManager service is retrieved, we should have switched to the main thread.
                Assert.True(JoinableTaskContext.IsOnMainThread);
                return buildManagerMock.Object;
            }));

        using var trigger = new VsSolutionUpdatesProjectSnapshotChangeTrigger(
            serviceProvider,
            StrictMock.Of<IProjectWorkspaceStateGenerator>(),
            _workspaceProvider,
            Dispatcher,
            JoinableTaskContext);

        var testAccessor = trigger.GetTestAccessor();

        await Task.Run(async () =>
        {
            Assert.False(JoinableTaskContext.IsOnMainThread);
            trigger.Initialize(StrictMock.Of<ProjectSnapshotManagerBase>());
            await testAccessor.InitializeTask.AssumeNotNull();
        });
    }

    [UIFact]
    public async Task SolutionClosing_CancelsActiveWork()
    {
        var projectManager = new TestProjectSnapshotManager(ProjectEngineFactories.DefaultProvider, Dispatcher)
        {
            AllowNotifyListeners = true,
        };

        var expectedProjectPath = s_someProject.FilePath;

        var expectedProjectSnapshot = await RunOnDispatcherAsync(() =>
        {
            projectManager.ProjectAdded(s_someProject);
            projectManager.ProjectAdded(s_someOtherProject);

            return projectManager.GetLoadedProject(s_someProject.Key);
        });

        var serviceProvider = VsMocks.CreateServiceProvider();
        var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();

        var vsHierarchyMock = new StrictMock<IVsHierarchy>();
        var vsProjectMock = vsHierarchyMock.As<IVsProject>();
        vsProjectMock
            .Setup(x => x.GetMkDocument((uint)VSConstants.VSITEMID.Root, out expectedProjectPath))
            .Returns(VSConstants.S_OK);

        using var trigger = new VsSolutionUpdatesProjectSnapshotChangeTrigger(
            serviceProvider,
            workspaceStateGenerator,
            _workspaceProvider,
            Dispatcher,
            JoinableTaskContext);

        var testAccessor = trigger.GetTestAccessor();

        trigger.Initialize(projectManager);
        trigger.UpdateProjectCfg_Done(vsHierarchyMock.Object, pCfgProj: null!, pCfgSln: null!, dwAction: 0, fSuccess: 0, fCancel: 0);

        // UpdateProjectCfg_Done will call OnProjectBuiltAsync(). The test accessor
        // provides a task we can wait on.
        await testAccessor.OnProjectBuiltTask.AssumeNotNull();

        await RunOnDispatcherAsync(() =>
        {
            projectManager.SolutionClosed();
            projectManager.ProjectRemoved(s_someProject.Key);
        });

        var update = Assert.Single(workspaceStateGenerator.UpdateQueue);
        Assert.Equal(update.WorkspaceProject.Id, _someWorkspaceProject.Id);
        Assert.Same(expectedProjectSnapshot, update.ProjectSnapshot);
        Assert.True(update.CancellationToken.IsCancellationRequested);
    }

    [UIFact]
    public async Task OnProjectBuiltAsync_KnownProject_EnqueuesProjectStateUpdate()
    {
        // Arrange
        var projectManager = new TestProjectSnapshotManager(ProjectEngineFactories.DefaultProvider, Dispatcher);
        var expectedProjectPath = s_someProject.FilePath;

        var expectedProjectSnapshot = await RunOnDispatcherAsync(() =>
        {
            projectManager.ProjectAdded(s_someProject);
            projectManager.ProjectAdded(s_someOtherProject);

            return projectManager.GetLoadedProject(s_someProject.Key);
        });

        var serviceProvider = VsMocks.CreateServiceProvider();
        var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();

        using var trigger = new VsSolutionUpdatesProjectSnapshotChangeTrigger(
            serviceProvider,
            workspaceStateGenerator,
            _workspaceProvider,
            Dispatcher,
            JoinableTaskContext);
        trigger.Initialize(projectManager);

        var vsHierarchyMock = new StrictMock<IVsHierarchy>();
        var vsProjectMock = vsHierarchyMock.As<IVsProject>();
        vsProjectMock
            .Setup(x => x.GetMkDocument((uint)VSConstants.VSITEMID.Root, out expectedProjectPath))
            .Returns(VSConstants.S_OK);

        var testAccessor = trigger.GetTestAccessor();

        // Act
        await testAccessor.OnProjectBuiltAsync(vsHierarchyMock.Object, DisposalToken);

        // Assert
        var update = Assert.Single(workspaceStateGenerator.UpdateQueue);
        Assert.Equal(update.WorkspaceProject.Id, _someWorkspaceProject.Id);
        Assert.Same(expectedProjectSnapshot, update.ProjectSnapshot);
    }

    [UIFact]
    public async Task OnProjectBuiltAsync_WithoutWorkspaceProject_DoesNotEnqueueUpdate()
    {
        // Arrange
        uint cookie = 42;
        var buildManagerMock = new StrictMock<IVsSolutionBuildManager>();
        buildManagerMock
            .Setup(b => b.AdviseUpdateSolutionEvents(It.IsAny<VsSolutionUpdatesProjectSnapshotChangeTrigger>(), out cookie))
            .Returns(VSConstants.S_OK);
        buildManagerMock
            .Setup(b => b.UnadviseUpdateSolutionEvents(cookie))
            .Returns(VSConstants.S_OK);

        var serviceProvider = VsMocks.CreateServiceProvider(b =>
            b.AddService<SVsSolutionBuildManager>(buildManagerMock.Object));

        var projectEngineFactoryProvider = StrictMock.Of<IProjectEngineFactoryProvider>();

        IProjectSnapshot? projectSnapshot = new ProjectSnapshot(
            ProjectState.Create(
                projectEngineFactoryProvider,
                new HostProject("/Some/Unknown/Path.csproj", "/Some/Unknown/obj", RazorConfiguration.Default, "Path"),
                ProjectWorkspaceState.Default));
        var expectedProjectPath = projectSnapshot.FilePath;

        var projectManager = new StrictMock<ProjectSnapshotManagerBase>();
        projectManager
            .Setup(p => p.GetAllProjectKeys(projectSnapshot.FilePath))
            .Returns(ImmutableArray.Create(projectSnapshot.Key));
        projectManager
            .Setup(p => p.GetLoadedProject(projectSnapshot.Key))
            .Returns(projectSnapshot);
        projectManager
            .Setup(p => p.TryGetLoadedProject(projectSnapshot.Key, out projectSnapshot))
            .Returns(true);

        var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();

        using var trigger = new VsSolutionUpdatesProjectSnapshotChangeTrigger(
            serviceProvider,
            workspaceStateGenerator,
            _workspaceProvider,
            Dispatcher,
            JoinableTaskContext);
        trigger.Initialize(projectManager.Object);

        var testAccessor = trigger.GetTestAccessor();

        // Act
        await testAccessor.OnProjectBuiltAsync(StrictMock.Of<IVsHierarchy>(), DisposalToken);

        // Assert
        Assert.Empty(workspaceStateGenerator.UpdateQueue);
    }

    [UIFact]
    public async Task OnProjectBuiltAsync_UnknownProject_DoesNotEnqueueUpdate()
    {
        // Arrange
        var expectedProjectPath = "Path/To/Project/proj.csproj";

        uint cookie = 42;
        var buildManagerMock = new StrictMock<IVsSolutionBuildManager>();
        buildManagerMock
            .Setup(b => b.AdviseUpdateSolutionEvents(It.IsAny<VsSolutionUpdatesProjectSnapshotChangeTrigger>(), out cookie))
            .Returns(VSConstants.S_OK);
        buildManagerMock
            .Setup(b => b.UnadviseUpdateSolutionEvents(cookie))
            .Returns(VSConstants.S_OK);

        var serviceProvider = VsMocks.CreateServiceProvider(b =>
            b.AddService<SVsSolutionBuildManager>(buildManagerMock.Object));

        var projectManager = new StrictMock<ProjectSnapshotManagerBase>();
        projectManager
            .Setup(p => p.GetAllProjectKeys(expectedProjectPath))
            .Returns(ImmutableArray<ProjectKey>.Empty);

        var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();

        using var trigger = new VsSolutionUpdatesProjectSnapshotChangeTrigger(
            serviceProvider,
            workspaceStateGenerator,
            _workspaceProvider,
            Dispatcher,
            JoinableTaskContext);
        trigger.Initialize(projectManager.Object);

        var testAccessor = trigger.GetTestAccessor();

        // Act
        await testAccessor.OnProjectBuiltAsync(StrictMock.Of<IVsHierarchy>(), DisposalToken);

        // Assert
        Assert.Empty(workspaceStateGenerator.UpdateQueue);
    }
}
