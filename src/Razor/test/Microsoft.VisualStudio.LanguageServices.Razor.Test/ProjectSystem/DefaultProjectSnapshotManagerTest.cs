﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using Moq;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    public class DefaultProjectSnapshotManagerTest : ProjectSnapshotManagerDispatcherWorkspaceTestBase
    {
        public DefaultProjectSnapshotManagerTest()
        {
            var someTagHelpers = new List<TagHelperDescriptor>
            {
                TagHelperDescriptorBuilder.Create("Test1", "TestAssembly").Build()
            };
            TagHelperResolver = new TestTagHelperResolver()
            {
                TagHelpers = someTagHelpers,
            };

            Documents = new HostDocument[]
            {
                TestProjectData.SomeProjectFile1,
                TestProjectData.SomeProjectFile2,

                // linked file
                TestProjectData.AnotherProjectNestedFile3,

                TestProjectData.SomeProjectComponentFile1,
                TestProjectData.SomeProjectComponentFile2,
            };

            HostProject = new HostProject(TestProjectData.SomeProject.FilePath, FallbackRazorConfiguration.MVC_2_0, TestProjectData.SomeProject.RootNamespace);
            HostProjectWithConfigurationChange = new HostProject(TestProjectData.SomeProject.FilePath, FallbackRazorConfiguration.MVC_1_0, TestProjectData.SomeProject.RootNamespace);

            ProjectManager = new TestProjectSnapshotManager(Dispatcher, Enumerable.Empty<ProjectSnapshotChangeTrigger>(), Workspace);

            ProjectWorkspaceStateWithTagHelpers = new ProjectWorkspaceState(TagHelperResolver.TagHelpers, default);

            SourceText = SourceText.From("Hello world");
        }

        private HostDocument[] Documents { get; }

        private HostProject HostProject { get; }

        private HostProject HostProjectWithConfigurationChange { get; }

        private ProjectWorkspaceState ProjectWorkspaceStateWithTagHelpers { get; }

        private TestTagHelperResolver TagHelperResolver { get; }

        private TestProjectSnapshotManager ProjectManager { get; }

        private SourceText SourceText { get; }

        protected override void ConfigureWorkspaceServices(List<IWorkspaceService> services!!)
        {
            services.Add(TagHelperResolver);
        }

        [UIFact]
        public void Initialize_DoneInCorrectOrderBasedOnInitializePriorityPriority()
        {
            // Arrange
            var initializedOrder = new List<string>();
            var highPriorityTrigger = new InitializeInspectionTrigger(() => initializedOrder.Add("highPriority"), 100);
            var defaultPriorityTrigger = new InitializeInspectionTrigger(() => initializedOrder.Add("lowPriority"), 0);

            // Building this list in the wrong order so we can verify priority matters
            var triggers = new[] { defaultPriorityTrigger, highPriorityTrigger };

            // Act
            var projectManager = new TestProjectSnapshotManager(Dispatcher, triggers, Workspace);

            // Assert
            Assert.Equal(new[] { "highPriority", "lowPriority" }, initializedOrder);
        }

        [UIFact]
        public void DocumentAdded_AddsDocument()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            Assert.Collection(snapshot.DocumentFilePaths.OrderBy(f => f), d => Assert.Equal(Documents[0].FilePath, d));

            Assert.Equal(ProjectChangeKind.DocumentAdded, ProjectManager.ListenersNotifiedOf);
        }

        [UIFact]
        public void DocumentAdded_AddsDocument_Legacy()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            Assert.Collection(
                snapshot.DocumentFilePaths.OrderBy(f => f),
                d =>
                {
                    Assert.Equal(Documents[0].FilePath, d);
                    Assert.Equal(FileKinds.Legacy, snapshot.GetDocument(d).FileKind);
                });

            Assert.Equal(ProjectChangeKind.DocumentAdded, ProjectManager.ListenersNotifiedOf);
        }

        [UIFact]
        public void DocumentAdded_AddsDocument_Component()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.DocumentAdded(HostProject, Documents[3], null);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            Assert.Collection(
                snapshot.DocumentFilePaths.OrderBy(f => f),
                d =>
                {
                    Assert.Equal(Documents[3].FilePath, d);
                    Assert.Equal(FileKinds.Component, snapshot.GetDocument(d).FileKind);
                });

            Assert.Equal(ProjectChangeKind.DocumentAdded, ProjectManager.ListenersNotifiedOf);
        }

        [UIFact]
        public void DocumentAdded_IgnoresDuplicate()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);
            ProjectManager.Reset();

            // Act
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            Assert.Collection(snapshot.DocumentFilePaths.OrderBy(f => f), d => Assert.Equal(Documents[0].FilePath, d));

            Assert.Null(ProjectManager.ListenersNotifiedOf);
        }

        [UIFact]
        public void DocumentAdded_IgnoresUnknownProject()
        {
            // Arrange

            // Act
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            Assert.Null(snapshot);
        }

        [UIFact]
        public async Task DocumentAdded_NullLoader_HasEmptyText()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var document = snapshot.GetDocument(snapshot.DocumentFilePaths.Single());

            var text = await document.GetTextAsync();
            Assert.Equal(0, text.Length);
        }

        [UIFact]
        public async Task DocumentAdded_WithLoader_LoadesText()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.Reset();

            var expected = SourceText.From("Hello");

            // Act
            ProjectManager.DocumentAdded(HostProject, Documents[0], TextLoader.From(TextAndVersion.Create(expected, VersionStamp.Default)));

            // Assert
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var document = snapshot.GetDocument(snapshot.DocumentFilePaths.Single());

            var actual = await document.GetTextAsync();
            Assert.Same(expected, actual);
        }

        [UIFact]
        public void DocumentAdded_CachesTagHelpers()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.ProjectWorkspaceStateChanged(HostProject.FilePath, ProjectWorkspaceStateWithTagHelpers);
            ProjectManager.Reset();

            var originalTagHelpers = ProjectManager.GetSnapshot(HostProject).TagHelpers;

            // Act
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);

            // Assert
            var newTagHelpers = ProjectManager.GetSnapshot(HostProject).TagHelpers;
            Assert.Same(originalTagHelpers, newTagHelpers);
        }

        [UIFact]
        public void DocumentAdded_CachesProjectEngine()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.Reset();

            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var projectEngine = snapshot.GetProjectEngine();

            // Act
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);

            // Assert
            snapshot = ProjectManager.GetSnapshot(HostProject);
            Assert.Same(projectEngine, snapshot.GetProjectEngine());
        }

        [UIFact]
        public void DocumentRemoved_RemovesDocument()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);
            ProjectManager.DocumentAdded(HostProject, Documents[1], null);
            ProjectManager.DocumentAdded(HostProject, Documents[2], null);
            ProjectManager.Reset();

            // Act
            ProjectManager.DocumentRemoved(HostProject, Documents[1]);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            Assert.Collection(
                snapshot.DocumentFilePaths.OrderBy(f => f),
                d => Assert.Equal(Documents[2].FilePath, d),
                d => Assert.Equal(Documents[0].FilePath, d));

            Assert.Equal(ProjectChangeKind.DocumentRemoved, ProjectManager.ListenersNotifiedOf);
        }

        [UIFact]
        public void DocumentRemoved_IgnoresNotFoundDocument()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.DocumentRemoved(HostProject, Documents[0]);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            Assert.Empty(snapshot.DocumentFilePaths);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
        }

        [UIFact]
        public void DocumentRemoved_IgnoresUnknownProject()
        {
            // Arrange

            // Act
            ProjectManager.DocumentRemoved(HostProject, Documents[0]);

            // Assert
            var snapshot = ProjectManager.GetSnapshot(HostProject);
            Assert.Null(snapshot);
        }

        [UIFact]
        public void DocumentRemoved_CachesTagHelpers()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.ProjectWorkspaceStateChanged(HostProject.FilePath, ProjectWorkspaceStateWithTagHelpers);
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);
            ProjectManager.DocumentAdded(HostProject, Documents[1], null);
            ProjectManager.DocumentAdded(HostProject, Documents[2], null);
            ProjectManager.Reset();

            var originalTagHelpers = ProjectManager.GetSnapshot(HostProject).TagHelpers;

            // Act
            ProjectManager.DocumentRemoved(HostProject, Documents[1]);

            // Assert
            var newTagHelpers = ProjectManager.GetSnapshot(HostProject).TagHelpers;
            Assert.Same(originalTagHelpers, newTagHelpers);
        }

        [UIFact]
        public void DocumentRemoved_CachesProjectEngine()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);
            ProjectManager.DocumentAdded(HostProject, Documents[1], null);
            ProjectManager.DocumentAdded(HostProject, Documents[2], null);
            ProjectManager.Reset();

            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var projectEngine = snapshot.GetProjectEngine();

            // Act
            ProjectManager.DocumentRemoved(HostProject, Documents[1]);

            // Assert
            snapshot = ProjectManager.GetSnapshot(HostProject);
            Assert.Same(projectEngine, snapshot.GetProjectEngine());
        }
        [UIFact]
        public async Task DocumentOpened_UpdatesDocument()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);
            ProjectManager.Reset();

            // Act
            ProjectManager.DocumentOpened(HostProject.FilePath, Documents[0].FilePath, SourceText);

            // Assert
            Assert.Equal(ProjectChangeKind.DocumentChanged, ProjectManager.ListenersNotifiedOf);

            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var text = await snapshot.GetDocument(Documents[0].FilePath).GetTextAsync();
            Assert.Same(SourceText, text);

            Assert.True(ProjectManager.IsDocumentOpen(Documents[0].FilePath));
        }

        [UIFact]
        public async Task DocumentClosed_UpdatesDocument()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);
            ProjectManager.DocumentOpened(HostProject.FilePath, Documents[0].FilePath, SourceText);
            ProjectManager.Reset();

            var expected = SourceText.From("Hi");
            var textAndVersion = TextAndVersion.Create(expected, VersionStamp.Create());

            Assert.True(ProjectManager.IsDocumentOpen(Documents[0].FilePath));

            // Act
            ProjectManager.DocumentClosed(HostProject.FilePath, Documents[0].FilePath, TextLoader.From(textAndVersion));

            // Assert
            Assert.Equal(ProjectChangeKind.DocumentChanged, ProjectManager.ListenersNotifiedOf);

            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var text = await snapshot.GetDocument(Documents[0].FilePath).GetTextAsync();
            Assert.Same(expected, text);
            Assert.False(ProjectManager.IsDocumentOpen(Documents[0].FilePath));
        }

        [UIFact]
        public async Task DocumentClosed_AcceptsChange()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);
            ProjectManager.Reset();

            var expected = SourceText.From("Hi");
            var textAndVersion = TextAndVersion.Create(expected, VersionStamp.Create());

            // Act
            ProjectManager.DocumentClosed(HostProject.FilePath, Documents[0].FilePath, TextLoader.From(textAndVersion));

            // Assert
            Assert.Equal(ProjectChangeKind.DocumentChanged, ProjectManager.ListenersNotifiedOf);

            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var text = await snapshot.GetDocument(Documents[0].FilePath).GetTextAsync();
            Assert.Same(expected, text);
        }

        [UIFact]
        public async Task DocumentChanged_Snapshot_UpdatesDocument()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);
            ProjectManager.DocumentOpened(HostProject.FilePath, Documents[0].FilePath, SourceText);
            ProjectManager.Reset();

            var expected = SourceText.From("Hi");

            // Act
            ProjectManager.DocumentChanged(HostProject.FilePath, Documents[0].FilePath, expected);

            // Assert
            Assert.Equal(ProjectChangeKind.DocumentChanged, ProjectManager.ListenersNotifiedOf);

            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var text = await snapshot.GetDocument(Documents[0].FilePath).GetTextAsync();
            Assert.Same(expected, text);
        }

        [UIFact]
        public async Task DocumentChanged_Loader_UpdatesDocument()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);
            ProjectManager.DocumentOpened(HostProject.FilePath, Documents[0].FilePath, SourceText);
            ProjectManager.Reset();

            var expected = SourceText.From("Hi");
            var textAndVersion = TextAndVersion.Create(expected, VersionStamp.Create());

            // Act
            ProjectManager.DocumentChanged(HostProject.FilePath, Documents[0].FilePath, TextLoader.From(textAndVersion));

            // Assert
            Assert.Equal(ProjectChangeKind.DocumentChanged, ProjectManager.ListenersNotifiedOf);

            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var text = await snapshot.GetDocument(Documents[0].FilePath).GetTextAsync();
            Assert.Same(expected, text);
        }

        [UIFact]
        public void ProjectAdded_WithoutWorkspaceProject_NotifiesListeners()
        {
            // Arrange

            // Act
            ProjectManager.ProjectAdded(HostProject);

            // Assert
            Assert.Equal(ProjectChangeKind.ProjectAdded, ProjectManager.ListenersNotifiedOf);
        }

        [UIFact]
        public void ProjectConfigurationChanged_ConfigurationChange_ProjectWorkspaceState_NotifiesListeners()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.ProjectConfigurationChanged(HostProjectWithConfigurationChange);

            // Assert
            Assert.Equal(ProjectChangeKind.ProjectChanged, ProjectManager.ListenersNotifiedOf);
        }

        [UIFact]
        public void ProjectConfigurationChanged_ConfigurationChange_WithProjectWorkspaceState_NotifiesListeners()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.ProjectWorkspaceStateChanged(HostProject.FilePath, ProjectWorkspaceStateWithTagHelpers);
            ProjectManager.Reset();

            // Act
            ProjectManager.ProjectConfigurationChanged(HostProjectWithConfigurationChange);

            // Assert
            Assert.Equal(ProjectChangeKind.ProjectChanged, ProjectManager.ListenersNotifiedOf);
        }

        [UIFact]
        public void ProjectConfigurationChanged_ConfigurationChange_DoesNotCacheProjectEngine()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.Reset();

            var snapshot = ProjectManager.GetSnapshot(HostProject);
            var projectEngine = snapshot.GetProjectEngine();

            // Act
            ProjectManager.ProjectConfigurationChanged(HostProjectWithConfigurationChange);

            // Assert
            snapshot = ProjectManager.GetSnapshot(HostProjectWithConfigurationChange);
            Assert.NotSame(projectEngine, snapshot.GetProjectEngine());
        }

        [UIFact]
        public void ProjectConfigurationChanged_IgnoresUnknownProject()
        {
            // Arrange

            // Act
            ProjectManager.ProjectConfigurationChanged(HostProject);

            // Assert
            Assert.Empty(ProjectManager.Projects);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
        }

        [UIFact]
        public void ProjectRemoved_RemovesProject_NotifiesListeners()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.ProjectRemoved(HostProject);

            // Assert
            Assert.Empty(ProjectManager.Projects);

            Assert.Equal(ProjectChangeKind.ProjectRemoved, ProjectManager.ListenersNotifiedOf);
        }

        [UIFact]
        public void ProjectWorkspaceStateChanged_WithoutHostProject_IgnoresWorkspaceState()
        {
            // Arrange

            // Act
            ProjectManager.ProjectWorkspaceStateChanged(HostProject.FilePath, ProjectWorkspaceStateWithTagHelpers);

            // Assert
            Assert.Empty(ProjectManager.Projects);

            Assert.Null(ProjectManager.ListenersNotifiedOf);
        }

        [UIFact]
        public void ProjectWorkspaceStateChanged_WithHostProject_FirstTime_NotifiesListenters()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.Reset();

            // Act
            ProjectManager.ProjectWorkspaceStateChanged(HostProject.FilePath, ProjectWorkspaceStateWithTagHelpers);

            // Assert
            Assert.Equal(ProjectChangeKind.ProjectChanged, ProjectManager.ListenersNotifiedOf);
        }

        [UIFact]
        public void WorkspaceProjectChanged_WithHostProject_NotifiesListenters()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.ProjectWorkspaceStateChanged(HostProject.FilePath, ProjectWorkspaceState.Default);
            ProjectManager.Reset();

            // Act
            ProjectManager.ProjectWorkspaceStateChanged(HostProject.FilePath, ProjectWorkspaceStateWithTagHelpers);

            // Assert
            Assert.Equal(ProjectChangeKind.ProjectChanged, ProjectManager.ListenersNotifiedOf);
        }

        [UIFact]
        public void NestedNotifications_NotifiesListenersInCorrectOrder()
        {
            // Arrange
            var listenerNotifications = new List<ProjectChangeKind>();
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.Reset();
            ProjectManager.Changed += (sender, args) =>
            {
                // These conditions will result in a triply nested change notification of Add -> Change -> Remove all within the .Change chain.

                if (args.Kind == ProjectChangeKind.DocumentAdded)
                {
                    ProjectManager.DocumentOpened(HostProject.FilePath, Documents[0].FilePath, SourceText);
                }
                else if (args.Kind == ProjectChangeKind.DocumentChanged)
                {
                    ProjectManager.DocumentRemoved(HostProject, Documents[0]);
                }
            };
            ProjectManager.Changed += (sender, args) => listenerNotifications.Add(args.Kind);
            ProjectManager.NotifyChangedEvents = true;

            // Act
            ProjectManager.DocumentAdded(HostProject, Documents[0], null);

            // Assert
            Assert.Equal(new[] { ProjectChangeKind.DocumentAdded, ProjectChangeKind.DocumentChanged, ProjectChangeKind.DocumentRemoved }, listenerNotifications);
        }

        [UIFact]
        public void SolutionClosing_ProjectChangedEventsCorrect()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);
            ProjectManager.Reset();

            ProjectManager.Changed += (sender, args) => Assert.True(args.SolutionIsClosing);
            ProjectManager.NotifyChangedEvents = true;

            var textLoader = new Mock<TextLoader>(MockBehavior.Strict);

            // Act
            ProjectManager.SolutionClosed();
            ProjectManager.DocumentAdded(HostProject, Documents[0], textLoader.Object);

            // Assert
            Assert.Equal(ProjectChangeKind.DocumentAdded, ProjectManager.ListenersNotifiedOf);
            textLoader.Verify(d => d.LoadTextAndVersionAsync(It.IsAny<Workspace>(), It.IsAny<DocumentId>(), It.IsAny<CancellationToken>()), Times.Never());
        }

        private class TestProjectSnapshotManager : DefaultProjectSnapshotManager
        {
            public TestProjectSnapshotManager(ProjectSnapshotManagerDispatcher dispatcher, IEnumerable<ProjectSnapshotChangeTrigger> triggers, Workspace workspace)
                : base(dispatcher, Mock.Of<ErrorReporter>(MockBehavior.Strict), triggers, workspace)
            {
            }

            public ProjectChangeKind? ListenersNotifiedOf { get; private set; }

            public bool NotifyChangedEvents { get; set; }

            public DefaultProjectSnapshot GetSnapshot(HostProject hostProject)
            {
                return Projects.Cast<DefaultProjectSnapshot>().FirstOrDefault(s => s.FilePath == hostProject.FilePath);
            }

            public DefaultProjectSnapshot GetSnapshot(Project workspaceProject)
            {
                return Projects.Cast<DefaultProjectSnapshot>().FirstOrDefault(s => s.FilePath == workspaceProject.FilePath);
            }

            public void Reset()
            {
                ListenersNotifiedOf = null;
            }

            protected override void NotifyListeners(ProjectChangeEventArgs e)
            {
                ListenersNotifiedOf = e.Kind;

                if (NotifyChangedEvents)
                {
                    base.NotifyListeners(e);
                }
            }
        }

        private class InitializeInspectionTrigger : ProjectSnapshotChangeTrigger
        {
            private readonly Action _initializeNotification;

            public InitializeInspectionTrigger(Action initializeNotification, int initializePriority)
            {
                _initializeNotification = initializeNotification;
                InitializePriority = initializePriority;
            }

            public override int InitializePriority { get; }

            public override void Initialize(ProjectSnapshotManagerBase projectManager)
            {
                _initializeNotification();
            }
        }
    }
}
