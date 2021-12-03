﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Editor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Editor.Razor.Documents;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.Editor.Razor
{
    public class DefaultVisualStudioDocumentTrackerTest : ProjectSnapshotManagerDispatcherWorkspaceTestBase
    {
        public DefaultVisualStudioDocumentTrackerTest()
        {
            RazorCoreContentType = Mock.Of<IContentType>(c => c.IsOfType(RazorLanguage.ContentType) && c.IsOfType(RazorConstants.LegacyContentType), MockBehavior.Strict);
            TextBuffer = Mock.Of<ITextBuffer>(b => b.ContentType == RazorCoreContentType, MockBehavior.Strict);

            FilePath = TestProjectData.SomeProjectFile1.FilePath;
            ProjectPath = TestProjectData.SomeProject.FilePath;
            RootNamespace = TestProjectData.SomeProject.RootNamespace;

            ImportDocumentManager = new Mock<ImportDocumentManager>(MockBehavior.Strict).Object;
            Mock.Get(ImportDocumentManager).Setup(m => m.OnSubscribed(It.IsAny<VisualStudioDocumentTracker>())).Verifiable();
            Mock.Get(ImportDocumentManager).Setup(m => m.OnUnsubscribed(It.IsAny<VisualStudioDocumentTracker>())).Verifiable();

            var projectSnapshotManagerDispatcher = new Mock<ProjectSnapshotManagerDispatcher>(MockBehavior.Strict);
            projectSnapshotManagerDispatcher.Setup(d => d.AssertDispatcherThread(It.IsAny<string>())).Verifiable();
            WorkspaceEditorSettings = new DefaultWorkspaceEditorSettings(projectSnapshotManagerDispatcher.Object, Mock.Of<EditorSettingsManager>(MockBehavior.Strict));

            SomeTagHelpers = new List<TagHelperDescriptor>()
            {
                TagHelperDescriptorBuilder.Create("test", "test").Build(),
            };

            ProjectManager = new TestProjectSnapshotManager(Dispatcher, Workspace) { AllowNotifyListeners = true };

            HostProject = new HostProject(ProjectPath, FallbackRazorConfiguration.MVC_2_1, RootNamespace);
            UpdatedHostProject = new HostProject(ProjectPath, FallbackRazorConfiguration.MVC_2_0, RootNamespace);
            OtherHostProject = new HostProject(TestProjectData.AnotherProject.FilePath, FallbackRazorConfiguration.MVC_2_0, TestProjectData.AnotherProject.RootNamespace);

            DocumentTracker = new DefaultVisualStudioDocumentTracker(
                Dispatcher,
                JoinableTaskFactory.Context,
                FilePath,
                ProjectPath,
                ProjectManager,
                WorkspaceEditorSettings,
                Workspace,
                TextBuffer,
                ImportDocumentManager);
        }

        private IContentType RazorCoreContentType { get; }

        private ITextBuffer TextBuffer { get; }

        private string FilePath { get; }

        private string ProjectPath { get; }

        private string RootNamespace { get; }

        private HostProject HostProject { get; }

        private HostProject UpdatedHostProject { get; }

        private HostProject OtherHostProject { get; }

        private Project WorkspaceProject { get; set; }

        private ImportDocumentManager ImportDocumentManager { get; }

        private WorkspaceEditorSettings WorkspaceEditorSettings { get; }

        private List<TagHelperDescriptor> SomeTagHelpers { get; }

        private TestTagHelperResolver TagHelperResolver { get; set; }

        private ProjectSnapshotManagerBase ProjectManager { get; }

        private DefaultVisualStudioDocumentTracker DocumentTracker { get; }

        protected override void ConfigureWorkspaceServices(List<IWorkspaceService> services)
        {
            TagHelperResolver = new TestTagHelperResolver();
            services.Add(TagHelperResolver);
        }

        protected override void ConfigureWorkspace(AdhocWorkspace workspace)
        {
            WorkspaceProject = workspace.AddProject(ProjectInfo.Create(
                ProjectId.CreateNewId(),
                new VersionStamp(),
                "Test1",
                "TestAssembly",
                LanguageNames.CSharp,
                filePath: TestProjectData.SomeProject.FilePath));
        }

        [UIFact]
        public void Subscribe_NoopsIfAlreadySubscribed()
        {
            // Arrange
            var callCount = 0;
            DocumentTracker.ContextChanged += (sender, args) => callCount++;
            DocumentTracker.Subscribe();

            // Act
            DocumentTracker.Subscribe();

            // Assert
            Assert.Equal(1, callCount);
        }

        [UIFact]
        public void Unsubscribe_NoopsIfAlreadyUnsubscribed()
        {
            // Arrange
            var callCount = 0;
            DocumentTracker.Subscribe();
            DocumentTracker.ContextChanged += (sender, args) => callCount++;
            DocumentTracker.Unsubscribe();

            // Act
            DocumentTracker.Unsubscribe();

            // Assert
            Assert.Equal(1, callCount);
        }

        [UIFact]
        public void Unsubscribe_NoopsIfSubscribeHasBeenCalledMultipleTimes()
        {
            // Arrange
            var callCount = 0;
            DocumentTracker.Subscribe();
            DocumentTracker.Subscribe();
            DocumentTracker.ContextChanged += (sender, args) => callCount++;

            // Act - 1
            DocumentTracker.Unsubscribe();

            // Assert - 1
            Assert.Equal(0, callCount);

            // Act - 2
            DocumentTracker.Unsubscribe();

            // Assert - 2
            Assert.Equal(1, callCount);
        }

        [UIFact]
        public void EditorSettingsManager_Changed_TriggersContextChanged()
        {
            // Arrange
            var called = false;
            DocumentTracker.ContextChanged += (sender, args) =>
            {
                Assert.Equal(ContextChangeKind.EditorSettingsChanged, args.Kind);
                called = true;
                Assert.Equal(ContextChangeKind.EditorSettingsChanged, args.Kind);
            };

            // Act
            DocumentTracker.EditorSettingsManager_Changed(null, null);

            // Assert
            Assert.True(called);
        }

        [UIFact]
        public void ProjectManager_Changed_ProjectAdded_TriggersContextChanged()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);

            var e = new ProjectChangeEventArgs(null, ProjectManager.GetLoadedProject(HostProject.FilePath), ProjectChangeKind.ProjectAdded);

            var called = false;
            DocumentTracker.ContextChanged += (sender, args) =>
            {
                called = true;

                Assert.Same(ProjectManager.GetLoadedProject(DocumentTracker.ProjectPath), DocumentTracker.ProjectSnapshot);
            };

            // Act
            DocumentTracker.ProjectManager_Changed(ProjectManager, e);

            // Assert
            Assert.True(called);
        }

        [UIFact]
        public void ProjectManager_Changed_ProjectChanged_TriggersContextChanged()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);

            var e = new ProjectChangeEventArgs(null, ProjectManager.GetLoadedProject(HostProject.FilePath), ProjectChangeKind.ProjectChanged);

            var called = false;
            DocumentTracker.ContextChanged += (sender, args) =>
            {
                called = true;

                Assert.Same(ProjectManager.GetLoadedProject(DocumentTracker.ProjectPath), DocumentTracker.ProjectSnapshot);
            };

            // Act
            DocumentTracker.ProjectManager_Changed(ProjectManager, e);

            // Assert
            Assert.True(called);
        }

        [UIFact]
        public void ProjectManager_Changed_ProjectRemoved_TriggersContextChanged_WithEphemeralProject()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);

            var project = ProjectManager.GetLoadedProject(HostProject.FilePath);
            ProjectManager.ProjectRemoved(HostProject);

            var e = new ProjectChangeEventArgs(project, null, ProjectChangeKind.ProjectRemoved);

            var called = false;
            DocumentTracker.ContextChanged += (sender, args) =>
            {
                // This can be called both with tag helper and project changes.
                called = true;

                Assert.IsType<EphemeralProjectSnapshot>(DocumentTracker.ProjectSnapshot);
            };

            // Act
            DocumentTracker.ProjectManager_Changed(ProjectManager, e);

            // Assert
            Assert.True(called);
        }

        [UIFact]
        public void ProjectManager_Changed_IgnoresUnknownProject()
        {
            // Arrange
            ProjectManager.ProjectAdded(OtherHostProject);

            var e = new ProjectChangeEventArgs(null, ProjectManager.GetLoadedProject(OtherHostProject.FilePath), ProjectChangeKind.ProjectChanged);

            var called = false;
            DocumentTracker.ContextChanged += (sender, args) => called = true;

            // Act
            DocumentTracker.ProjectManager_Changed(ProjectManager, e);

            // Assert
            Assert.False(called);
        }

        [UIFact]
        public void Import_Changed_ImportAssociatedWithDocument_TriggersContextChanged()
        {
            // Arrange
            var called = false;
            DocumentTracker.ContextChanged += (sender, args) =>
            {
                Assert.Equal(ContextChangeKind.ImportsChanged, args.Kind);
                called = true;
            };

            var importChangedArgs = new ImportChangedEventArgs("path/to/import", FileChangeKind.Changed, new[] { FilePath });

            // Act
            DocumentTracker.Import_Changed(null, importChangedArgs);

            // Assert
            Assert.True(called);
        }

        [UIFact]
        public void Import_Changed_UnrelatedImport_DoesNothing()
        {
            // Arrange
            DocumentTracker.ContextChanged += (sender, args) => throw new InvalidOperationException();

            var importChangedArgs = new ImportChangedEventArgs("path/to/import", FileChangeKind.Changed, new[] { "path/to/differentfile" });

            // Act & Assert (Does not throw)
            DocumentTracker.Import_Changed(null, importChangedArgs);
        }

        [UIFact]
        public void Subscribe_SetsSupportedProjectAndTriggersContextChanged()
        {
            // Arrange
            var called = false;
            DocumentTracker.ContextChanged += (sender, args) => called = true;

            // Act
            DocumentTracker.Subscribe();

            // Assert
            Assert.True(called);
            Assert.True(DocumentTracker.IsSupportedProject);
        }

        [UIFact]
        public void Unsubscribe_ResetsSupportedProjectAndTriggersContextChanged()
        {
            // Arrange

            // Subscribe once to set supported project
            DocumentTracker.Subscribe();

            var called = false;
            DocumentTracker.ContextChanged += (sender, args) =>
            {
                called = true;
                Assert.Equal(ContextChangeKind.ProjectChanged, args.Kind);
            };

            // Act
            DocumentTracker.Unsubscribe();

            // Assert
            Assert.False(DocumentTracker.IsSupportedProject);
            Assert.True(called);
        }

        [UIFact]
        public void AddTextView_AddsToTextViewCollection()
        {
            // Arrange
            var textView = Mock.Of<ITextView>(MockBehavior.Strict);

            // Act
            DocumentTracker.AddTextView(textView);

            // Assert
            Assert.Collection(DocumentTracker.TextViews, v => Assert.Same(v, textView));
        }

        [UIFact]
        public void AddTextView_DoesNotAddDuplicateTextViews()
        {
            // Arrange
            var textView = Mock.Of<ITextView>(MockBehavior.Strict);

            // Act
            DocumentTracker.AddTextView(textView);
            DocumentTracker.AddTextView(textView);

            // Assert
            Assert.Collection(DocumentTracker.TextViews, v => Assert.Same(v, textView));
        }

        [UIFact]
        public void AddTextView_AddsMultipleTextViewsToCollection()
        {
            // Arrange
            var textView1 = Mock.Of<ITextView>(MockBehavior.Strict);
            var textView2 = Mock.Of<ITextView>(MockBehavior.Strict);

            // Act
            DocumentTracker.AddTextView(textView1);
            DocumentTracker.AddTextView(textView2);

            // Assert
            Assert.Collection(
                DocumentTracker.TextViews,
                v => Assert.Same(v, textView1),
                v => Assert.Same(v, textView2));
        }

        [UIFact]
        public void RemoveTextView_RemovesTextViewFromCollection_SingleItem()
        {
            // Arrange
            var textView = Mock.Of<ITextView>(MockBehavior.Strict);
            DocumentTracker.AddTextView(textView);

            // Act
            DocumentTracker.RemoveTextView(textView);

            // Assert
            Assert.Empty(DocumentTracker.TextViews);
        }

        [UIFact]
        public void RemoveTextView_RemovesTextViewFromCollection_MultipleItems()
        {
            // Arrange
            var textView1 = Mock.Of<ITextView>(MockBehavior.Strict);
            var textView2 = Mock.Of<ITextView>(MockBehavior.Strict);
            var textView3 = Mock.Of<ITextView>(MockBehavior.Strict);
            DocumentTracker.AddTextView(textView1);
            DocumentTracker.AddTextView(textView2);
            DocumentTracker.AddTextView(textView3);

            // Act
            DocumentTracker.RemoveTextView(textView2);

            // Assert
            Assert.Collection(
                DocumentTracker.TextViews,
                v => Assert.Same(v, textView1),
                v => Assert.Same(v, textView3));
        }

        [UIFact]
        public void RemoveTextView_NoopsWhenRemovingTextViewNotInCollection()
        {
            // Arrange
            var textView1 = Mock.Of<ITextView>(MockBehavior.Strict);
            DocumentTracker.AddTextView(textView1);
            var textView2 = Mock.Of<ITextView>(MockBehavior.Strict);

            // Act
            DocumentTracker.RemoveTextView(textView2);

            // Assert
            Assert.Collection(DocumentTracker.TextViews, v => Assert.Same(v, textView1));
        }

        [UIFact]
        public void Subscribed_InitializesEphemeralProjectSnapshot()
        {
            // Arrange

            // Act
            DocumentTracker.Subscribe();

            // Assert
            Assert.IsType<EphemeralProjectSnapshot>(DocumentTracker.ProjectSnapshot);
        }

        [UIFact]
        public void Subscribed_InitializesRealProjectSnapshot()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);

            // Act
            DocumentTracker.Subscribe();

            // Assert
            Assert.IsType<DefaultProjectSnapshot>(DocumentTracker.ProjectSnapshot);
        }

        [UIFact]
        public void Subscribed_ListensToProjectChanges()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);

            DocumentTracker.Subscribe();

            var args = new List<ContextChangeEventArgs>();
            DocumentTracker.ContextChanged += (sender, e) => args.Add(e);

            // Act
            ProjectManager.ProjectConfigurationChanged(UpdatedHostProject);

            // Assert
            var snapshot = Assert.IsType<DefaultProjectSnapshot>(DocumentTracker.ProjectSnapshot);

            Assert.Same(UpdatedHostProject, snapshot.HostProject);

            Assert.Collection(
                args,
                e => Assert.Equal(ContextChangeKind.ProjectChanged, e.Kind));
        }

        [UIFact]
        public void Subscribed_ListensToProjectRemoval()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject);

            DocumentTracker.Subscribe();

            var args = new List<ContextChangeEventArgs>();
            DocumentTracker.ContextChanged += (sender, e) => args.Add(e);

            // Act
            ProjectManager.ProjectRemoved(HostProject);

            // Assert
            Assert.IsType<EphemeralProjectSnapshot>(DocumentTracker.ProjectSnapshot);

            Assert.Collection(
                args,
                e => Assert.Equal(ContextChangeKind.ProjectChanged, e.Kind));
        }
    }
}
