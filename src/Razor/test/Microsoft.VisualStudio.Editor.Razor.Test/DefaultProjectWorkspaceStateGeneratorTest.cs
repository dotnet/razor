// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.Workspaces
{
    public class DefaultProjectWorkspaceStateGeneratorTest : ProjectSnapshotManagerDispatcherTestBase
    {
        public DefaultProjectWorkspaceStateGeneratorTest()
        {
            var tagHelperResolver = new TestTagHelperResolver();
            tagHelperResolver.TagHelpers.Add(TagHelperDescriptorBuilder.Create("ResolvableTagHelper", "TestAssembly").Build());
            ResolvableTagHelpers = tagHelperResolver.TagHelpers;
            var workspaceServices = new List<IWorkspaceService>() { tagHelperResolver };
            var testServices = TestServices.Create(workspaceServices, Enumerable.Empty<ILanguageService>());
            Workspace = TestWorkspace.Create(testServices);
            var projectId = ProjectId.CreateNewId("Test");
            var solution = Workspace.CurrentSolution.AddProject(ProjectInfo.Create(
                projectId,
                VersionStamp.Default,
                "Test",
                "Test",
                LanguageNames.CSharp,
                TestProjectData.SomeProject.FilePath));
            WorkspaceProject = solution.GetProject(projectId);
            ProjectSnapshot = new DefaultProjectSnapshot(ProjectState.Create(Workspace.Services, TestProjectData.SomeProject));
            ProjectWorkspaceStateWithTagHelpers = new ProjectWorkspaceState(new[]
            {
                TagHelperDescriptorBuilder.Create("TestTagHelper", "TestAssembly").Build(),
            }, default);
        }

        private IReadOnlyList<TagHelperDescriptor> ResolvableTagHelpers { get; }

        private Workspace Workspace { get; }

        private Project WorkspaceProject { get; }

        private DefaultProjectSnapshot ProjectSnapshot { get; }

        private ProjectWorkspaceState ProjectWorkspaceStateWithTagHelpers { get; }

        [UIFact]
        public void Dispose_MakesUpdateNoop()
        {
            // Arrange
            using (var stateGenerator = new DefaultProjectWorkspaceStateGenerator(Dispatcher))
            {
                stateGenerator.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);

                // Act
                stateGenerator.Dispose();
                stateGenerator.Update(WorkspaceProject, ProjectSnapshot, CancellationToken.None);

                // Assert
                Assert.Empty(stateGenerator.Updates);
            }
        }

        [UIFact]
        public void Update_StartsUpdateTask()
        {
            // Arrange
            using (var stateGenerator = new DefaultProjectWorkspaceStateGenerator(Dispatcher))
            {
                stateGenerator.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);

                // Act
                stateGenerator.Update(WorkspaceProject, ProjectSnapshot, CancellationToken.None);

                // Assert
                var update = Assert.Single(stateGenerator.Updates);
                Assert.False(update.Value.Task.IsCompleted);
            }
        }

        [UIFact]
        public void Update_SoftCancelsIncompleteTaskForSameProject()
        {
            // Arrange
            using (var stateGenerator = new DefaultProjectWorkspaceStateGenerator(Dispatcher))
            {
                stateGenerator.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);
                stateGenerator.Update(WorkspaceProject, ProjectSnapshot, CancellationToken.None);
                var initialUpdate = stateGenerator.Updates.Single().Value;

                // Act
                stateGenerator.Update(WorkspaceProject, ProjectSnapshot, CancellationToken.None);

                // Assert
                Assert.True(initialUpdate.Cts.IsCancellationRequested);
            }
        }

        [UIFact]
        public async Task Update_NullWorkspaceProject_ClearsProjectWorkspaceState()
        {
            // Arrange
            using (var stateGenerator = new DefaultProjectWorkspaceStateGenerator(Dispatcher))
            {
                stateGenerator.NotifyBackgroundWorkCompleted = new ManualResetEventSlim(initialState: false);
                var projectManager = new TestProjectSnapshotManager(Dispatcher, Workspace);
                stateGenerator.Initialize(projectManager);
                projectManager.ProjectAdded(ProjectSnapshot.HostProject);
                projectManager.ProjectWorkspaceStateChanged(ProjectSnapshot.FilePath, ProjectWorkspaceStateWithTagHelpers);

                // Act
                stateGenerator.Update(workspaceProject: null, ProjectSnapshot, CancellationToken.None);

                // Jump off the UI thread so the background work can complete.
                await Task.Run(() => stateGenerator.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3)));

                // Assert
                var newProjectSnapshot = projectManager.GetLoadedProject(ProjectSnapshot.FilePath);
                Assert.Empty(newProjectSnapshot.TagHelpers);
            }
        }

        [UIFact]
        public async Task Update_ResolvesTagHelpersAndUpdatesWorkspaceState()
        {
            // Arrange
            using (var stateGenerator = new DefaultProjectWorkspaceStateGenerator(Dispatcher))
            {
                stateGenerator.NotifyBackgroundWorkCompleted = new ManualResetEventSlim(initialState: false);
                var projectManager = new TestProjectSnapshotManager(Dispatcher, Workspace);
                stateGenerator.Initialize(projectManager);
                projectManager.ProjectAdded(ProjectSnapshot.HostProject);

                // Act
                stateGenerator.Update(WorkspaceProject, ProjectSnapshot, CancellationToken.None);

                // Jump off the UI thread so the background work can complete.
                await Task.Run(() => stateGenerator.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3)));

                // Assert
                var newProjectSnapshot = projectManager.GetLoadedProject(ProjectSnapshot.FilePath);
                Assert.Equal(ResolvableTagHelpers, newProjectSnapshot.TagHelpers);
            }
        }
    }
}
