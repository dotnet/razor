// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common.VisualStudio;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor;

public class ProjectWorkspaceStateGeneratorTest(ITestOutputHelper testOutput) : VisualStudioWorkspaceTestBase(testOutput)
{
    private static readonly HostProject s_hostProject = TestProjectData.SomeProject;

#nullable disable
    private TestProjectSnapshotManager _projectManager;
    private TestTagHelperResolver _tagHelperResolver;
    private ProjectId _projectId;
    private ProjectWorkspaceState _projectWorkspaceState;
#nullable enable

    protected override Task InitializeAsync()
    {
        var projectInfo = s_hostProject.ToProjectInfo();
        _projectId = projectInfo.Id;

        var solution = Workspace.CurrentSolution.AddProject(projectInfo);
        Workspace.TryApplyChanges(solution);

        ImmutableArray<TagHelperDescriptor> tagHelpers = [TagHelperDescriptorBuilder.Create("ResolvableTagHelper", "TestAssembly").Build()];
        _projectWorkspaceState = ProjectWorkspaceState.Create(tagHelpers);
        _tagHelperResolver = new TestTagHelperResolver(tagHelpers);

        _projectManager = CreateProjectSnapshotManager();

        return Task.CompletedTask;
    }

    private ProjectWorkspaceStateGenerator CreateGenerator()
    {
        var generator = new ProjectWorkspaceStateGenerator(_projectManager, _tagHelperResolver, WorkspaceProvider, LoggerFactory, NoOpTelemetryReporter.Instance);
        AddDisposable(generator);

        return generator;
    }

    [UIFact]
    public void Dispose_MakesUpdateIgnored()
    {
        // Arrange
        var generator = CreateGenerator();

        var generatorAccessor = generator.GetTestAccessor();
        generatorAccessor.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);

        // Act
        generator.Dispose();

        generator.EnqueueUpdate(s_hostProject.Key, _projectId);

        // Assert
        Assert.Empty(generatorAccessor.GetUpdates());
    }

    [UIFact]
    public async Task Update_StartsUpdateTask()
    {
        // Arrange
        var generator = CreateGenerator();

        await _projectManager.UpdateAsync(updater =>
        {
            updater.AddProject(s_hostProject);
        });

        var generatorAccessor = generator.GetTestAccessor();
        generatorAccessor.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);

        // Act
        generator.EnqueueUpdate(s_hostProject.Key, _projectId);

        // Assert
        var update = Assert.Single(generatorAccessor.GetUpdates());
        Assert.False(update.IsCompleted);
    }

    [UIFact]
    public void Update_SoftCancelsIncompleteTaskForSameProject()
    {
        // Arrange
        var generator = CreateGenerator();

        var generatorAccessor = generator.GetTestAccessor();
        generatorAccessor.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);

        generator.EnqueueUpdate(s_hostProject.Key, _projectId);

        var initialUpdate = Assert.Single(generatorAccessor.GetUpdates());

        // Act
        generator.EnqueueUpdate(s_hostProject.Key, _projectId);

        // Assert
        Assert.True(initialUpdate.IsCancellationRequested);
    }

    [UIFact]
    public async Task Update_NullWorkspaceProject_ClearsProjectWorkspaceState()
    {
        // Arrange
        var generator = CreateGenerator();

        var generatorAccessor = generator.GetTestAccessor();
        generatorAccessor.NotifyBackgroundWorkCompleted = new ManualResetEventSlim(initialState: false);

        await _projectManager.UpdateAsync(updater =>
        {
            updater.AddProject(s_hostProject);
            updater.UpdateProjectWorkspaceState(s_hostProject.Key, _projectWorkspaceState);
        });

        // Act
        generator.EnqueueUpdate(s_hostProject.Key, id: null);

        // Jump off the UI thread so the background work can complete.
        await Task.Run(() => generatorAccessor.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3)));

        // Assert
        var newProjectSnapshot = _projectManager.GetRequiredProject(s_hostProject.Key);

        Assert.Empty(await newProjectSnapshot.GetTagHelpersAsync(DisposalToken));
    }

    [UIFact]
    public async Task Update_ResolvesTagHelpersAndUpdatesWorkspaceState()
    {
        // Arrange
        var generator = CreateGenerator();

        var generatorAccessor = generator.GetTestAccessor();
        generatorAccessor.NotifyBackgroundWorkCompleted = new ManualResetEventSlim(initialState: false);

        await _projectManager.UpdateAsync(updater =>
        {
            updater.AddProject(s_hostProject);
        });

        // Act
        generator.EnqueueUpdate(s_hostProject.Key, _projectId);

        // Jump off the UI thread so the background work can complete.
        await Task.Run(() => generatorAccessor.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3)));

        // Assert
        var newProjectSnapshot = _projectManager.GetRequiredProject(s_hostProject.Key);

        Assert.Equal<TagHelperDescriptor>(_tagHelperResolver.TagHelpers, await newProjectSnapshot.GetTagHelpersAsync(DisposalToken));
    }
}
