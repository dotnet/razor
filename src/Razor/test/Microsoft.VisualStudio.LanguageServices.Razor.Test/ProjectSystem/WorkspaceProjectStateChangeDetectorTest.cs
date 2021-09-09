// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServices.Razor;
using Microsoft.VisualStudio.LanguageServices.Razor.Test;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Moq;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    public class WorkspaceProjectStateChangeDetectorTest : WorkspaceTestBase, IDisposable
    {
        private static readonly ProjectSnapshotManagerDispatcher Dispatcher = new VisualStudioProjectSnapshotManagerDispatcher(
            new VisualStudioErrorReporter(new TestSVsServiceProvider()));

        public WorkspaceProjectStateChangeDetectorTest()
        {
            EmptySolution = Workspace.CurrentSolution;

            var projectId1 = ProjectId.CreateNewId("One");
            var projectId2 = ProjectId.CreateNewId("Two");
            var projectId3 = ProjectId.CreateNewId("Three");

            CshtmlDocumentId = DocumentId.CreateNewId(projectId1);
            var cshtmlDocumentInfo = DocumentInfo.Create(CshtmlDocumentId, "Test", filePath: "file.cshtml.g.cs");
            RazorDocumentId = DocumentId.CreateNewId(projectId1);
            var razorDocumentInfo = DocumentInfo.Create(RazorDocumentId, "Test", filePath: "file.razor.g.cs");
            BackgroundVirtualCSharpDocumentId = DocumentId.CreateNewId(projectId1);
            var backgroundDocumentInfo = DocumentInfo.Create(BackgroundVirtualCSharpDocumentId, "Test", filePath: "file.razor__bg__virtual.cs");
            PartialComponentClassDocumentId = DocumentId.CreateNewId(projectId1);
            var partialComponentClassDocumentInfo = DocumentInfo.Create(PartialComponentClassDocumentId, "Test", filePath: "file.razor.cs");

            SolutionWithTwoProjects = Workspace.CurrentSolution
                .AddProject(ProjectInfo.Create(
                    projectId1,
                    VersionStamp.Default,
                    "One",
                    "One",
                    LanguageNames.CSharp,
                    filePath: "One.csproj",
                    documents: new[] { cshtmlDocumentInfo, razorDocumentInfo, partialComponentClassDocumentInfo, backgroundDocumentInfo }))
                .AddProject(ProjectInfo.Create(
                    projectId2,
                    VersionStamp.Default,
                    "Two",
                    "Two",
                    LanguageNames.CSharp,
                    filePath: "Two.csproj"));

            SolutionWithOneProject = EmptySolution
                .AddProject(ProjectInfo.Create(
                    projectId3,
                    VersionStamp.Default,
                    "Three",
                    "Three",
                    LanguageNames.CSharp,
                    filePath: "Three.csproj"));

            var project2Reference = new ProjectReference(projectId2);
            var project3Reference = new ProjectReference(projectId3);
            SolutionWithDependentProject = Workspace.CurrentSolution
                .AddProject(ProjectInfo.Create(
                    projectId1,
                    VersionStamp.Default,
                    "One",
                    "One",
                    LanguageNames.CSharp,
                    filePath: "One.csproj",
                    documents: new[] { cshtmlDocumentInfo, razorDocumentInfo, partialComponentClassDocumentInfo, backgroundDocumentInfo },
                    projectReferences: new[] { project2Reference }))
                .AddProject(ProjectInfo.Create(
                    projectId2,
                    VersionStamp.Default,
                    "Two",
                    "Two",
                    LanguageNames.CSharp,
                    filePath: "Two.csproj",
                    projectReferences: new[] { project3Reference }))
                .AddProject(ProjectInfo.Create(
                    projectId3,
                    VersionStamp.Default,
                    "Three",
                    "Three",
                    LanguageNames.CSharp,
                    filePath: "Three.csproj",
                    documents: new[] { razorDocumentInfo }));

            ProjectNumberOne = SolutionWithTwoProjects.GetProject(projectId1);
            ProjectNumberTwo = SolutionWithTwoProjects.GetProject(projectId2);
            ProjectNumberThree = SolutionWithOneProject.GetProject(projectId3);

            HostProjectOne = new HostProject("One.csproj", FallbackRazorConfiguration.MVC_1_1, "One");
            HostProjectTwo = new HostProject("Two.csproj", FallbackRazorConfiguration.MVC_1_1, "Two");
            HostProjectThree = new HostProject("Three.csproj", FallbackRazorConfiguration.MVC_1_1, "Three");
            WorkQueue = new BatchingWorkQueue(TimeSpan.FromMilliseconds(1), StringComparer.Ordinal, new DefaultErrorReporter());
            WorkQueueTestAccessor = WorkQueue.GetTestAccessor();
            WorkQueue.GetTestAccessor().NotifyBackgroundWorkCompleted = null;
            WorkQueueTestAccessor.NotifyBackgroundWorkCompleted = new ManualResetEventSlim(initialState: false);
        }

        private BatchingWorkQueue WorkQueue { get; }

        private BatchingWorkQueue.TestAccessor WorkQueueTestAccessor { get; }

        private HostProject HostProjectOne { get; }

        private HostProject HostProjectTwo { get; }

        private HostProject HostProjectThree { get; }

        private Solution EmptySolution { get; }

        private Solution SolutionWithOneProject { get; }

        private Solution SolutionWithTwoProjects { get; }

        private Solution SolutionWithDependentProject { get; }

        private Project ProjectNumberOne { get; }

        private Project ProjectNumberTwo { get; }

        private Project ProjectNumberThree { get; }

        public DocumentId CshtmlDocumentId { get; }

        public DocumentId RazorDocumentId { get; }

        public DocumentId BackgroundVirtualCSharpDocumentId { get; }

        public DocumentId PartialComponentClassDocumentId { get; }

        [UITheory]
        [InlineData(WorkspaceChangeKind.DocumentAdded)]
        [InlineData(WorkspaceChangeKind.DocumentChanged)]
        [InlineData(WorkspaceChangeKind.DocumentRemoved)]
        public async Task WorkspaceChanged_DocumentEvents_EnqueuesUpdatesForDependentProjects(WorkspaceChangeKind kind)
        {
            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue)
            {
                NotifyWorkspaceChangedEventComplete = new ManualResetEventSlim(initialState: false),
            };
            WorkQueueTestAccessor.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);

            var projectManager = new TestProjectSnapshotManager(Dispatcher, new[] { detector }, Workspace);

            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(HostProjectOne);
                projectManager.ProjectAdded(HostProjectTwo);
                projectManager.ProjectAdded(HostProjectThree);
            }, CancellationToken.None).ConfigureAwait(false);

            // Initialize with a project. This will get removed.
            var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.SolutionAdded, oldSolution: EmptySolution, newSolution: SolutionWithOneProject);
            detector.Workspace_WorkspaceChanged(Workspace, e);
            detector.NotifyWorkspaceChangedEventComplete.Wait();
            detector.NotifyWorkspaceChangedEventComplete.Reset();

            e = new WorkspaceChangeEventArgs(kind, oldSolution: SolutionWithOneProject, newSolution: SolutionWithDependentProject);

            var solution = SolutionWithDependentProject.WithProjectAssemblyName(ProjectNumberThree.Id, "Changed");

            e = new WorkspaceChangeEventArgs(kind, oldSolution: SolutionWithDependentProject, newSolution: solution, projectId: ProjectNumberThree.Id, documentId: RazorDocumentId);

            // Act
            detector.Workspace_WorkspaceChanged(Workspace, e);
            detector.NotifyWorkspaceChangedEventComplete.Wait();

            // Assert
            Assert.Equal(3, WorkQueueTestAccessor.Work.Count);
            Assert.Contains(WorkQueueTestAccessor.Work, u => u.Key == ProjectNumberOne.FilePath);
            Assert.Contains(WorkQueueTestAccessor.Work, u => u.Key == ProjectNumberTwo.FilePath);
            Assert.Contains(WorkQueueTestAccessor.Work, u => u.Key == ProjectNumberThree.FilePath);

            WorkQueueTestAccessor.BlockBackgroundWorkStart.Set();
            WorkQueueTestAccessor.NotifyBackgroundWorkCompleted.Wait();
            Assert.Empty(WorkQueueTestAccessor.Work);
        }

        [UITheory]
        [InlineData(WorkspaceChangeKind.ProjectChanged)]
        [InlineData(WorkspaceChangeKind.ProjectAdded)]
        [InlineData(WorkspaceChangeKind.ProjectRemoved)]

        public async Task WorkspaceChanged_ProjectEvents_EnqueuesUpdatesForDependentProjects(WorkspaceChangeKind kind)
        {
            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue)
            {
                NotifyWorkspaceChangedEventComplete = new ManualResetEventSlim(initialState: false),
            };
            WorkQueueTestAccessor.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);

            var projectManager = new TestProjectSnapshotManager(Dispatcher, new[] { detector }, Workspace);

            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(HostProjectOne);
                projectManager.ProjectAdded(HostProjectTwo);
                projectManager.ProjectAdded(HostProjectThree);
            }, CancellationToken.None).ConfigureAwait(false);

            // Initialize with a project. This will get removed.
            var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.SolutionAdded, oldSolution: EmptySolution, newSolution: SolutionWithOneProject);
            detector.Workspace_WorkspaceChanged(Workspace, e);
            detector.NotifyWorkspaceChangedEventComplete.Wait();
            detector.NotifyWorkspaceChangedEventComplete.Reset();

            e = new WorkspaceChangeEventArgs(kind, oldSolution: SolutionWithOneProject, newSolution: SolutionWithDependentProject);

            var solution = SolutionWithDependentProject.WithProjectAssemblyName(ProjectNumberThree.Id, "Changed");

            e = new WorkspaceChangeEventArgs(kind, oldSolution: SolutionWithDependentProject, newSolution: solution, projectId: ProjectNumberThree.Id);

            // Act
            detector.Workspace_WorkspaceChanged(Workspace, e);
            detector.NotifyWorkspaceChangedEventComplete.Wait();

            // Assert
            Assert.Equal(3, WorkQueueTestAccessor.Work.Count);
            Assert.Contains(WorkQueueTestAccessor.Work, u => u.Key == ProjectNumberOne.FilePath);
            Assert.Contains(WorkQueueTestAccessor.Work, u => u.Key == ProjectNumberTwo.FilePath);
            Assert.Contains(WorkQueueTestAccessor.Work, u => u.Key == ProjectNumberThree.FilePath);

            WorkQueueTestAccessor.BlockBackgroundWorkStart.Set();
            WorkQueueTestAccessor.NotifyBackgroundWorkCompleted.Wait();
            Assert.Empty(WorkQueueTestAccessor.Work);
        }

        [UITheory]
        [InlineData(WorkspaceChangeKind.SolutionAdded)]
        [InlineData(WorkspaceChangeKind.SolutionChanged)]
        [InlineData(WorkspaceChangeKind.SolutionCleared)]
        [InlineData(WorkspaceChangeKind.SolutionReloaded)]
        [InlineData(WorkspaceChangeKind.SolutionRemoved)]
        public async Task WorkspaceChanged_SolutionEvents_EnqueuesUpdatesForProjectsInSolution(WorkspaceChangeKind kind)
        {
            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue)
            {
                NotifyWorkspaceChangedEventComplete = new ManualResetEventSlim(initialState: false),
            };
            var projectManager = new TestProjectSnapshotManager(Dispatcher, new[] { detector }, Workspace);
            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(HostProjectOne);
                projectManager.ProjectAdded(HostProjectTwo);
            }, CancellationToken.None).ConfigureAwait(false);

            var e = new WorkspaceChangeEventArgs(kind, oldSolution: EmptySolution, newSolution: SolutionWithTwoProjects);

            // Act
            detector.Workspace_WorkspaceChanged(Workspace, e);
            detector.NotifyWorkspaceChangedEventComplete.Wait();
            WorkQueueTestAccessor.NotifyBackgroundWorkCompleted.Wait();

            // Assert
            Assert.Collection(
                workspaceStateGenerator.UpdateQueue,
                p => Assert.Equal(ProjectNumberOne.Id, p.workspaceProject.Id),
                p => Assert.Equal(ProjectNumberTwo.Id, p.workspaceProject.Id));
        }

        [UITheory]
        [InlineData(WorkspaceChangeKind.SolutionAdded)]
        [InlineData(WorkspaceChangeKind.SolutionChanged)]
        [InlineData(WorkspaceChangeKind.SolutionCleared)]
        [InlineData(WorkspaceChangeKind.SolutionReloaded)]
        [InlineData(WorkspaceChangeKind.SolutionRemoved)]
        public async Task WorkspaceChanged_SolutionEvents_EnqueuesStateClear_EnqueuesSolutionProjectUpdates(WorkspaceChangeKind kind)
        {
            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue)
            {
                NotifyWorkspaceChangedEventComplete = new ManualResetEventSlim(initialState: false),
            };

            var projectManager = new TestProjectSnapshotManager(Dispatcher, new[] { detector }, Workspace);

            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(HostProjectOne);
                projectManager.ProjectAdded(HostProjectTwo);
                projectManager.ProjectAdded(HostProjectThree);
            }, CancellationToken.None).ConfigureAwait(false);

            // Initialize with a project. This will get removed.
            var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.SolutionAdded, oldSolution: EmptySolution, newSolution: SolutionWithOneProject);
            detector.Workspace_WorkspaceChanged(Workspace, e);
            detector.NotifyWorkspaceChangedEventComplete.Wait();
            detector.NotifyWorkspaceChangedEventComplete.Reset();
            WorkQueueTestAccessor.NotifyBackgroundWorkCompleted.Wait();
            WorkQueueTestAccessor.NotifyBackgroundWorkCompleted.Reset();

            e = new WorkspaceChangeEventArgs(kind, oldSolution: SolutionWithOneProject, newSolution: SolutionWithTwoProjects);

            // Act
            detector.Workspace_WorkspaceChanged(Workspace, e);
            detector.NotifyWorkspaceChangedEventComplete.Wait();
            WorkQueueTestAccessor.NotifyBackgroundWorkCompleted.Wait();

            // Assert
            Assert.Collection(
                workspaceStateGenerator.UpdateQueue,
                p => Assert.Equal(ProjectNumberThree.Id, p.workspaceProject.Id),
                p => Assert.Null(p.workspaceProject),
                p => Assert.Equal(ProjectNumberOne.Id, p.workspaceProject.Id),
                p => Assert.Equal(ProjectNumberTwo.Id, p.workspaceProject.Id));
        }

        [UITheory]
        [InlineData(WorkspaceChangeKind.ProjectChanged)]
        [InlineData(WorkspaceChangeKind.ProjectReloaded)]
        public async Task WorkspaceChanged_ProjectChangeEvents_UpdatesProjectState_AfterDelay(WorkspaceChangeKind kind)
        {
            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue);
            WorkQueueTestAccessor.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);

            var projectManager = new TestProjectSnapshotManager(Dispatcher, new[] { detector }, Workspace);
            await Dispatcher.RunOnDispatcherThreadAsync(() => projectManager.ProjectAdded(HostProjectOne), CancellationToken.None).ConfigureAwait(false);

            var solution = SolutionWithTwoProjects.WithProjectAssemblyName(ProjectNumberOne.Id, "Changed");
            var e = new WorkspaceChangeEventArgs(kind, oldSolution: SolutionWithTwoProjects, newSolution: solution, projectId: ProjectNumberOne.Id);

            // Act
            detector.Workspace_WorkspaceChanged(Workspace, e);

            // Assert
            //
            // The change hasn't come through yet.
            Assert.Empty(workspaceStateGenerator.UpdateQueue);

            WorkQueueTestAccessor.BlockBackgroundWorkStart.Set();
            WorkQueueTestAccessor.NotifyBackgroundWorkCompleted.Wait();

            var (workspaceProject, projectSnapshot) = Assert.Single(workspaceStateGenerator.UpdateQueue);
            Assert.Equal(workspaceProject.Id, ProjectNumberOne.Id);
            Assert.Equal(projectSnapshot.FilePath, HostProjectOne.FilePath);
        }

        [UIFact]
        public async Task WorkspaceChanged_DocumentChanged_BackgroundVirtualCS_UpdatesProjectState_AfterDelay()
        {
            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue);
            WorkQueueTestAccessor.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);

            Workspace.TryApplyChanges(SolutionWithTwoProjects);
            var projectManager = new TestProjectSnapshotManager(Dispatcher, new[] { detector }, Workspace);
            await Dispatcher.RunOnDispatcherThreadAsync(() => projectManager.ProjectAdded(HostProjectOne), CancellationToken.None).ConfigureAwait(false);
            workspaceStateGenerator.ClearQueue();

            var solution = SolutionWithTwoProjects.WithDocumentText(BackgroundVirtualCSharpDocumentId, SourceText.From("public class Foo{}"));
            var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.DocumentChanged, oldSolution: SolutionWithTwoProjects, newSolution: solution, projectId: ProjectNumberOne.Id, BackgroundVirtualCSharpDocumentId);

            // Act
            detector.Workspace_WorkspaceChanged(Workspace, e);

            // Assert
            //
            // The change hasn't come through yet.
            Assert.Empty(workspaceStateGenerator.UpdateQueue);

            WorkQueueTestAccessor.BlockBackgroundWorkStart.Set();
            WorkQueueTestAccessor.NotifyBackgroundWorkCompleted.Wait();

            var (workspaceProject, projectSnapshot) = Assert.Single(workspaceStateGenerator.UpdateQueue);
            Assert.Equal(workspaceProject.Id, ProjectNumberOne.Id);
            Assert.Equal(projectSnapshot.FilePath, HostProjectOne.FilePath);
        }

        [UIFact]
        public async Task WorkspaceChanged_DocumentChanged_CSHTML_UpdatesProjectState_AfterDelay()
        {
            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue);
            WorkQueueTestAccessor.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);

            Workspace.TryApplyChanges(SolutionWithTwoProjects);
            var projectManager = new TestProjectSnapshotManager(Dispatcher, new[] { detector }, Workspace);
            await Dispatcher.RunOnDispatcherThreadAsync(() => projectManager.ProjectAdded(HostProjectOne), CancellationToken.None).ConfigureAwait(false);
            workspaceStateGenerator.ClearQueue();

            var solution = SolutionWithTwoProjects.WithDocumentText(CshtmlDocumentId, SourceText.From("Hello World"));
            var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.DocumentChanged, oldSolution: SolutionWithTwoProjects, newSolution: solution, projectId: ProjectNumberOne.Id, CshtmlDocumentId);

            // Act
            detector.Workspace_WorkspaceChanged(Workspace, e);

            // Assert
            //
            // The change hasn't come through yet.
            Assert.Empty(workspaceStateGenerator.UpdateQueue);

            WorkQueueTestAccessor.BlockBackgroundWorkStart.Set();
            WorkQueueTestAccessor.NotifyBackgroundWorkCompleted.Wait();

            var (workspaceProject, projectSnapshot) = Assert.Single(workspaceStateGenerator.UpdateQueue);
            Assert.Equal(workspaceProject.Id, ProjectNumberOne.Id);
            Assert.Equal(projectSnapshot.FilePath, HostProjectOne.FilePath);
        }

        [UIFact]
        public async Task WorkspaceChanged_DocumentChanged_Razor_UpdatesProjectState_AfterDelay()
        {
            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue);
            WorkQueueTestAccessor.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);

            Workspace.TryApplyChanges(SolutionWithTwoProjects);
            var projectManager = new TestProjectSnapshotManager(Dispatcher, new[] { detector }, Workspace);
            await Dispatcher.RunOnDispatcherThreadAsync(() => projectManager.ProjectAdded(HostProjectOne), CancellationToken.None).ConfigureAwait(false);
            workspaceStateGenerator.ClearQueue();

            var solution = SolutionWithTwoProjects.WithDocumentText(RazorDocumentId, SourceText.From("Hello World"));
            var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.DocumentChanged, oldSolution: SolutionWithTwoProjects, newSolution: solution, projectId: ProjectNumberOne.Id, RazorDocumentId);

            // Act
            detector.Workspace_WorkspaceChanged(Workspace, e);

            // Assert
            //
            // The change hasn't come through yet.
            Assert.Empty(workspaceStateGenerator.UpdateQueue);

            WorkQueueTestAccessor.BlockBackgroundWorkStart.Set();
            WorkQueueTestAccessor.NotifyBackgroundWorkCompleted.Wait();

            var (workspaceProject, projectSnapshot) = Assert.Single(workspaceStateGenerator.UpdateQueue);
            Assert.Equal(workspaceProject.Id, ProjectNumberOne.Id);
            Assert.Equal(projectSnapshot.FilePath, HostProjectOne.FilePath);
        }

        [UIFact]
        public async Task WorkspaceChanged_DocumentChanged_PartialComponent_UpdatesProjectState_AfterDelay()
        {
            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue);
            WorkQueueTestAccessor.BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false);

            Workspace.TryApplyChanges(SolutionWithTwoProjects);
            var projectManager = new TestProjectSnapshotManager(Dispatcher, new[] { detector }, Workspace);
            await Dispatcher.RunOnDispatcherThreadAsync(() => projectManager.ProjectAdded(HostProjectOne), CancellationToken.None).ConfigureAwait(false);
            workspaceStateGenerator.ClearQueue();

            var sourceText = SourceText.From(
$@"
public partial class TestComponent : {ComponentsApi.IComponent.MetadataName} {{}}
namespace Microsoft.AspNetCore.Components
{{
    public interface IComponent {{}}
}}
");
            var syntaxTreeRoot = await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync();
            var solution = SolutionWithTwoProjects
                .WithDocumentText(PartialComponentClassDocumentId, sourceText)
                .WithDocumentSyntaxRoot(PartialComponentClassDocumentId, syntaxTreeRoot, PreservationMode.PreserveIdentity);
            var document = solution.GetDocument(PartialComponentClassDocumentId);

            // The change detector only operates when a semantic model / syntax tree is available.
            await document.GetSyntaxRootAsync();
            await document.GetSemanticModelAsync();

            var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.DocumentChanged, oldSolution: solution, newSolution: solution, projectId: ProjectNumberOne.Id, PartialComponentClassDocumentId);

            // Act
            detector.Workspace_WorkspaceChanged(Workspace, e);

            // Assert
            //
            // The change hasn't come through yet.
            Assert.Empty(workspaceStateGenerator.UpdateQueue);

            WorkQueueTestAccessor.BlockBackgroundWorkStart.Set();

            WorkQueueTestAccessor.NotifyBackgroundWorkCompleted.Wait();

            var (workspaceProject, projectSnapshot) = Assert.Single(workspaceStateGenerator.UpdateQueue);
            Assert.Equal(workspaceProject.Id, ProjectNumberOne.Id);
            Assert.Equal(projectSnapshot.FilePath, HostProjectOne.FilePath);
        }

        [UIFact]
        public async Task WorkspaceChanged_ProjectRemovedEvent_QueuesProjectStateRemoval()
        {
            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue)
            {
                NotifyWorkspaceChangedEventComplete = new ManualResetEventSlim(initialState: false),
            };
            var projectManager = new TestProjectSnapshotManager(Dispatcher, new[] { detector }, Workspace);
            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(HostProjectOne);
                projectManager.ProjectAdded(HostProjectTwo);
            }, CancellationToken.None).ConfigureAwait(false);

            var solution = SolutionWithTwoProjects.RemoveProject(ProjectNumberOne.Id);
            var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.ProjectRemoved, oldSolution: SolutionWithTwoProjects, newSolution: solution, projectId: ProjectNumberOne.Id);

            // Act
            detector.Workspace_WorkspaceChanged(Workspace, e);
            WorkQueueTestAccessor.NotifyBackgroundWorkCompleted.Wait();

            // Assert
            Assert.Collection(
                workspaceStateGenerator.UpdateQueue,
                p => Assert.Null(p.workspaceProject));
        }

        [UIFact]
        public async Task WorkspaceChanged_ProjectAddedEvent_AddsProject()
        {
            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue)
            {
                NotifyWorkspaceChangedEventComplete = new ManualResetEventSlim(initialState: false),
            };
            var projectManager = new TestProjectSnapshotManager(Dispatcher, new[] { detector }, Workspace);
            await Dispatcher.RunOnDispatcherThreadAsync(() => projectManager.ProjectAdded(HostProjectThree), CancellationToken.None).ConfigureAwait(false);

            var solution = SolutionWithOneProject;
            var e = new WorkspaceChangeEventArgs(WorkspaceChangeKind.ProjectAdded, oldSolution: EmptySolution, newSolution: solution, projectId: ProjectNumberThree.Id);

            // Act
            detector.Workspace_WorkspaceChanged(Workspace, e);
            detector.NotifyWorkspaceChangedEventComplete.Wait();
            WorkQueueTestAccessor.NotifyBackgroundWorkCompleted.Wait();

            // Assert
            Assert.Collection(
                workspaceStateGenerator.UpdateQueue,
                p => Assert.Equal(ProjectNumberThree.Id, p.workspaceProject.Id));
        }

        [Fact]
        public async Task IsPartialComponentClass_NoIComponent_ReturnsFalse()
        {
            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue);
            var sourceText = SourceText.From(
$@"
public partial class TestComponent{{}}
");
            var syntaxTreeRoot = await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync();
            var solution = SolutionWithTwoProjects
                .WithDocumentText(PartialComponentClassDocumentId, sourceText)
                .WithDocumentSyntaxRoot(PartialComponentClassDocumentId, syntaxTreeRoot, PreservationMode.PreserveIdentity);
            var document = solution.GetDocument(PartialComponentClassDocumentId);

            // Initialize document
            await document.GetSyntaxRootAsync();
            await document.GetSemanticModelAsync();

            // Act
            var result = detector.IsPartialComponentClass(document);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsPartialComponentClass_InitializedDocument_ReturnsTrue()
        {
            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue);
            var sourceText = SourceText.From(
$@"
public partial class TestComponent : {ComponentsApi.IComponent.MetadataName} {{}}
namespace Microsoft.AspNetCore.Components
{{
    public interface IComponent {{}}
}}
");
            var syntaxTreeRoot = await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync();
            var solution = SolutionWithTwoProjects
                .WithDocumentText(PartialComponentClassDocumentId, sourceText)
                .WithDocumentSyntaxRoot(PartialComponentClassDocumentId, syntaxTreeRoot, PreservationMode.PreserveIdentity);
            var document = solution.GetDocument(PartialComponentClassDocumentId);

            // Initialize document
            await document.GetSyntaxRootAsync();
            await document.GetSemanticModelAsync();

            // Act
            var result = detector.IsPartialComponentClass(document);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsPartialComponentClass_Uninitialized_ReturnsFalse()
        {
            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue);
            var sourceText = SourceText.From(
$@"
public partial class TestComponent : {ComponentsApi.IComponent.MetadataName} {{}}
namespace Microsoft.AspNetCore.Components
{{
    public interface IComponent {{}}
}}
");
            var syntaxTreeRoot = CSharpSyntaxTree.ParseText(sourceText).GetRoot();
            var solution = SolutionWithTwoProjects
                .WithDocumentText(PartialComponentClassDocumentId, sourceText)
                .WithDocumentSyntaxRoot(PartialComponentClassDocumentId, syntaxTreeRoot, PreservationMode.PreserveIdentity);
            var document = solution.GetDocument(PartialComponentClassDocumentId);

            // Act
            var result = detector.IsPartialComponentClass(document);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsPartialComponentClass_UninitializedSemanticModel_ReturnsFalse()
        {
            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue);
            var sourceText = SourceText.From(
$@"
public partial class TestComponent : {ComponentsApi.IComponent.MetadataName} {{}}
namespace Microsoft.AspNetCore.Components
{{
    public interface IComponent {{}}
}}
");
            var syntaxTreeRoot = await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync();
            var solution = SolutionWithTwoProjects
                .WithDocumentText(PartialComponentClassDocumentId, sourceText)
                .WithDocumentSyntaxRoot(PartialComponentClassDocumentId, syntaxTreeRoot, PreservationMode.PreserveIdentity);
            var document = solution.GetDocument(PartialComponentClassDocumentId);

            await document.GetSyntaxRootAsync();

            // Act
            var result = detector.IsPartialComponentClass(document);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsPartialComponentClass_NonClass_ReturnsFalse()
        {
            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue);
            var sourceText = SourceText.From(string.Empty);
            var syntaxTreeRoot = await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync();
            var solution = SolutionWithTwoProjects
                .WithDocumentText(PartialComponentClassDocumentId, sourceText)
                .WithDocumentSyntaxRoot(PartialComponentClassDocumentId, syntaxTreeRoot, PreservationMode.PreserveIdentity);
            var document = solution.GetDocument(PartialComponentClassDocumentId);

            // Initialize document
            await document.GetSyntaxRootAsync();
            await document.GetSemanticModelAsync();

            // Act
            var result = detector.IsPartialComponentClass(document);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsPartialComponentClass_MultipleClassesOneComponentPartial_ReturnsTrue()
        {

            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue);
            var sourceText = SourceText.From(
$@"
public partial class NonComponent1 {{}}
public class NonComponent2 {{}}
public partial class TestComponent : {ComponentsApi.IComponent.MetadataName} {{}}
public partial class NonComponent3 {{}}
public class NonComponent4 {{}}
namespace Microsoft.AspNetCore.Components
{{
    public interface IComponent {{}}
}}
");
            var syntaxTreeRoot = await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync();
            var solution = SolutionWithTwoProjects
                .WithDocumentText(PartialComponentClassDocumentId, sourceText)
                .WithDocumentSyntaxRoot(PartialComponentClassDocumentId, syntaxTreeRoot, PreservationMode.PreserveIdentity);
            var document = solution.GetDocument(PartialComponentClassDocumentId);

            // Initialize document
            await document.GetSyntaxRootAsync();
            await document.GetSemanticModelAsync();

            // Act
            var result = detector.IsPartialComponentClass(document);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsPartialComponentClass_NonComponents_ReturnsFalse()
        {

            // Arrange
            var workspaceStateGenerator = new TestProjectWorkspaceStateGenerator();
            var detector = new WorkspaceProjectStateChangeDetector(workspaceStateGenerator, Dispatcher, WorkQueue);
            var sourceText = SourceText.From(
$@"
public partial class NonComponent1 {{}}
public class NonComponent2 {{}}
public partial class NonComponent3 {{}}
public class NonComponent4 {{}}
namespace Microsoft.AspNetCore.Components
{{
    public interface IComponent {{}}
}}
");
            var syntaxTreeRoot = await CSharpSyntaxTree.ParseText(sourceText).GetRootAsync();
            var solution = SolutionWithTwoProjects
                .WithDocumentText(PartialComponentClassDocumentId, sourceText)
                .WithDocumentSyntaxRoot(PartialComponentClassDocumentId, syntaxTreeRoot, PreservationMode.PreserveIdentity);
            var document = solution.GetDocument(PartialComponentClassDocumentId);

            // Initialize document
            await document.GetSyntaxRootAsync();
            await document.GetSemanticModelAsync();

            // Act
            var result = detector.IsPartialComponentClass(document);

            // Assert
            Assert.False(result);
        }

        public void Dispose()
        {
            WorkQueue.Dispose();
        }

        private class TestProjectSnapshotManager : DefaultProjectSnapshotManager
        {
            public TestProjectSnapshotManager(
                ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
                IEnumerable<ProjectSnapshotChangeTrigger> triggers,
                Workspace workspace)
                : base(projectSnapshotManagerDispatcher, Mock.Of<ErrorReporter>(MockBehavior.Strict), triggers, workspace)
            {
            }
        }

        private class TestSVsServiceProvider : SVsServiceProvider
        {
            public object GetService(Type serviceType) => null;
        }
    }
}
