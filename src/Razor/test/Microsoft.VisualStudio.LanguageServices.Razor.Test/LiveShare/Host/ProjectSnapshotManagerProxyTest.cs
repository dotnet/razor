// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectEngineHost;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common.VisualStudio;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LiveShare.Razor.Test;
using Microsoft.VisualStudio.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.LiveShare.Razor.Host;

public class ProjectSnapshotManagerProxyTest : VisualStudioTestBase
{
    private const string ProjectName1 = "project1";
    private const string ProjectName2 = "project2";
    private const string ProjectFilePath1 = $"/host/path/to/{ProjectName1}.csproj";
    private const string ProjectFilePath2 = $"/host/path/to/{ProjectName2}.csproj";
    private const string IntermediateOutputPath = "/host/path/to/obj";
    private const string LspProjectFilePath1 = $"vsls:/path/to/{ProjectName1}.csproj";
    private const string LspProjectFilePath2 = $"vsls:/path/to/{ProjectName2}.csproj";

    private readonly IProjectSnapshot _projectSnapshot1;
    private readonly IProjectSnapshot _projectSnapshot2;

    public ProjectSnapshotManagerProxyTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        var projectEngineFactoryProvider = StrictMock.Of<IProjectEngineFactoryProvider>();

        _projectSnapshot1 = new ProjectSnapshot(
            ProjectState.Create(
                projectEngineFactoryProvider,
                new HostProject(ProjectFilePath1, IntermediateOutputPath, RazorConfiguration.Default, ProjectName1),
                ProjectWorkspaceState.Create([TagHelperDescriptorBuilder.Create("test1", "TestAssembly1").Build()])));

        _projectSnapshot2 = new ProjectSnapshot(
            ProjectState.Create(
                projectEngineFactoryProvider,
                new HostProject(ProjectFilePath2, IntermediateOutputPath, RazorConfiguration.Default, ProjectName2),
                ProjectWorkspaceState.Create([TagHelperDescriptorBuilder.Create("test2", "TestAssembly2").Build()])));
    }

    [UIFact]
    public async Task CalculateUpdatedStateAsync_ReturnsStateForAllProjects()
    {
        // Arrange
        var projectManagerMock = CreateProjectSnapshotManager(_projectSnapshot1, _projectSnapshot2);
        var projectManager = projectManagerMock.Object;
        using var proxy = new ProjectSnapshotManagerProxy(
            new TestCollaborationSession(true),
            projectManager,
            Dispatcher,
            JoinableTaskFactory);

        // Act
        var state = await JoinableTaskFactory.RunAsync(() => proxy.CalculateUpdatedStateAsync(projectManager.GetProjects()));

        // Assert
        var project1TagHelpers = await _projectSnapshot1.GetTagHelpersAsync(CancellationToken.None);
        var project2TagHelpers = await _projectSnapshot2.GetTagHelpersAsync(CancellationToken.None);

        Assert.Collection(
            state.ProjectHandles,
            AssertProjectSnapshotHandle(LspProjectFilePath1, project1TagHelpers),
            AssertProjectSnapshotHandle(LspProjectFilePath2, project2TagHelpers));
    }

    [UIFact]
    public async Task Changed_TriggersOnSnapshotManagerChanged()
    {
        // Arrange
        var projectManagerMock = CreateProjectSnapshotManager(_projectSnapshot1);
        var projectManager = projectManagerMock.Object;
        using var proxy = new ProjectSnapshotManagerProxy(
            new TestCollaborationSession(true),
            projectManager,
            Dispatcher,
            JoinableTaskFactory);

        var proxyAccessor = proxy.GetTestAccessor();

        var called = false;
        proxy.Changed += (sender, args) =>
        {
            called = true;
            Assert.Equal(LspProjectFilePath1, args.ProjectFilePath.ToString());
            Assert.Equal(ProjectProxyChangeKind.ProjectChanged, args.Kind);
            Assert.NotNull(args.Newer);
            Assert.Equal(LspProjectFilePath1, args.Newer.FilePath.ToString());
        };

        // Act
        await RunOnDispatcherAsync(() =>
        {
            projectManagerMock.RaiseChanged(
                new ProjectChangeEventArgs(_projectSnapshot1, _projectSnapshot1, ProjectChangeKind.ProjectChanged));
        });

        await proxyAccessor.ProcessingChangedEventTestTask.AssumeNotNull().JoinAsync();

        // Assert
        Assert.True(called);
    }

    [UIFact]
    public void Changed_DoesNotFireIfProxyIsDisposed()
    {
        // Arrange
        var projectManagerMock = CreateProjectSnapshotManager(_projectSnapshot1);
        var projectManager = projectManagerMock.Object;
        var proxy = new ProjectSnapshotManagerProxy(
            new TestCollaborationSession(true),
            projectManager,
            Dispatcher,
            JoinableTaskFactory);

        var proxyAccessor = proxy.GetTestAccessor();

        proxy.Changed += (sender, args) => throw new InvalidOperationException("Should not have been called.");
        proxy.Dispose();

        // Act
        projectManagerMock.RaiseChanged(
            new ProjectChangeEventArgs(_projectSnapshot1, _projectSnapshot1, ProjectChangeKind.ProjectChanged));

        // Assert
        Assert.Null(proxyAccessor.ProcessingChangedEventTestTask);
    }

    [UIFact]
    public async Task GetLatestProjectsAsync_ReturnsSnapshotManagerProjects()
    {
        // Arrange
        var projectManagerMock = CreateProjectSnapshotManager(_projectSnapshot1);
        var projectManager = projectManagerMock.Object;
        using var proxy = new ProjectSnapshotManagerProxy(
            new TestCollaborationSession(true),
            projectManager,
            Dispatcher,
            JoinableTaskFactory);

        // Act
        var projects = await proxy.GetLatestProjectsAsync();

        // Assert
        var project = Assert.Single(projects);
        Assert.Same(_projectSnapshot1, project);
    }

    [UIFact]
    public async Task GetStateAsync_ReturnsProjectState()
    {
        // Arrange
        var projectManagerMock = CreateProjectSnapshotManager(_projectSnapshot1, _projectSnapshot2);
        var projectManager = projectManagerMock.Object;
        using var proxy = new ProjectSnapshotManagerProxy(
            new TestCollaborationSession(true),
            projectManager,
            Dispatcher,
            JoinableTaskFactory);

        // Act
        var state = await JoinableTaskFactory.RunAsync(() => proxy.GetProjectManagerStateAsync(DisposalToken));

        // Assert
        var project1TagHelpers = await _projectSnapshot1.GetTagHelpersAsync(DisposalToken);
        var project2TagHelpers = await _projectSnapshot2.GetTagHelpersAsync(DisposalToken);

        Assert.Collection(
            state.ProjectHandles,
            AssertProjectSnapshotHandle(LspProjectFilePath1, project1TagHelpers),
            AssertProjectSnapshotHandle(LspProjectFilePath2, project2TagHelpers));
    }

    [UIFact]
    public async Task GetStateAsync_CachesState()
    {
        // Arrange
        var projectManagerMock = CreateProjectSnapshotManager(_projectSnapshot1);
        var projectManager = projectManagerMock.Object;
        using var proxy = new ProjectSnapshotManagerProxy(
            new TestCollaborationSession(true),
            projectManager,
            Dispatcher,
            JoinableTaskFactory);

        // Act
        var state1 = await JoinableTaskFactory.RunAsync(() => proxy.GetProjectManagerStateAsync(DisposalToken));
        var state2 = await JoinableTaskFactory.RunAsync(() => proxy.GetProjectManagerStateAsync(DisposalToken));

        // Assert
        Assert.Same(state1, state2);
    }

    private static StrictMock<IProjectSnapshotManager> CreateProjectSnapshotManager(params IProjectSnapshot[] projects)
    {
        var mock = new StrictMock<IProjectSnapshotManager>();

        mock.Setup(x => x.GetProjects())
            .Returns([.. projects]);

        return mock;
    }

    private static Action<ProjectSnapshotHandleProxy> AssertProjectSnapshotHandle(
        string expectedFilePath,
        ImmutableArray<TagHelperDescriptor> expectedTagHelpers)
        => handle =>
        {
            Assert.Equal(expectedFilePath, handle.FilePath.ToString());
            Assert.Equal<TagHelperDescriptor>(expectedTagHelpers, handle.ProjectWorkspaceState.TagHelpers);
        };
}
