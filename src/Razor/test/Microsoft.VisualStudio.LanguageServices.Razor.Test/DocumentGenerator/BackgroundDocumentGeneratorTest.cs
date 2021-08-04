﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Moq;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    // These tests are really integration tests. There isn't a good way to unit test this functionality since
    // the only thing in here is threading.
    public class BackgroundDocumentGeneratorTest : ProjectSnapshotManagerDispatcherWorkspaceTestBase
    {
        public BackgroundDocumentGeneratorTest()
        {
            Documents = new HostDocument[]
            {
                TestProjectData.SomeProjectFile1,
                TestProjectData.AnotherProjectFile1,
            };

            HostProject1 = new HostProject(TestProjectData.SomeProject.FilePath, FallbackRazorConfiguration.MVC_1_0, TestProjectData.SomeProject.RootNamespace);
            HostProject2 = new HostProject(TestProjectData.AnotherProject.FilePath, FallbackRazorConfiguration.MVC_1_0, TestProjectData.AnotherProject.RootNamespace);

            DynamicFileInfoProvider = new TestDynamicFileInfoProvider();
        }

        private HostDocument[] Documents { get; }

        private HostProject HostProject1 { get; }

        private HostProject HostProject2 { get; }

        private TestDynamicFileInfoProvider DynamicFileInfoProvider { get; }

        protected override void ConfigureProjectEngine(RazorProjectEngineBuilder builder)
        {
            builder.SetImportFeature(new TestImportProjectFeature());
        }

        [UIFact]
        public async Task ProcessDocument_LongDocumentParse_DoesNotUpdateAfterSuppress()
        {
            // Arrange
            var projectManager = new TestProjectSnapshotManager(Dispatcher, Workspace);
            projectManager.ProjectAdded(HostProject1);

            // We utilize a task completion source here so we can "fake" a document parse taking a significant amount of time
            var tcs = new TaskCompletionSource<TextAndVersion>();
            var textLoader = new Mock<TextLoader>(MockBehavior.Strict);
            textLoader.Setup(loader => loader.LoadTextAndVersionAsync(It.IsAny<Workspace>(), It.IsAny<DocumentId>(), It.IsAny<CancellationToken>()))
                .Returns(tcs.Task);
            var hostDocument = Documents[0];

            var project = projectManager.GetLoadedProject(HostProject1.FilePath);
            var queue = new BackgroundDocumentGenerator(Dispatcher, DynamicFileInfoProvider)
            {
                Delay = TimeSpan.FromMilliseconds(1),
                NotifyBackgroundWorkCompleted = new ManualResetEventSlim(initialState: false),
                NotifyBackgroundCapturedWorkload = new ManualResetEventSlim(initialState: false),
            };

            queue.Initialize(projectManager);

            // We trigger enqueued notifications via adding/opening to the project manager
            projectManager.AllowNotifyListeners = true;

            // Act & Assert
            projectManager.DocumentAdded(HostProject1, hostDocument, textLoader.Object);

            queue.NotifyBackgroundCapturedWorkload.Wait();

            projectManager.DocumentOpened(HostProject1.FilePath, hostDocument.FilePath, SourceText.From(string.Empty));

            // Verify document was suppressed because it was opened
            Assert.Null(DynamicFileInfoProvider.DynamicDocuments[hostDocument.FilePath]);

            // Unblock document processing
            tcs.SetResult(TextAndVersion.Create(SourceText.From(string.Empty), VersionStamp.Default));

            await Task.Run(() => queue.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3)));

            // Validate that even though document parsing took a significant amount of time that the dynamic document wasn't "unsuppressed"
            Assert.Null(DynamicFileInfoProvider.DynamicDocuments[hostDocument.FilePath]);
        }

        [UIFact]
        public async Task ProcessDocument_SwallowsIOExceptions()
        {
            // Arrange
            var projectManager = new TestProjectSnapshotManager(Dispatcher, Workspace);
            projectManager.ProjectAdded(HostProject1);

            var textLoader = new Mock<TextLoader>(MockBehavior.Strict);
            textLoader.Setup(loader => loader.LoadTextAndVersionAsync(It.IsAny<Workspace>(), It.IsAny<DocumentId>(), It.IsAny<CancellationToken>()))
                .Throws<FileNotFoundException>();
            projectManager.DocumentAdded(HostProject1, Documents[0], textLoader.Object);

            var project = projectManager.GetLoadedProject(HostProject1.FilePath);

            var queue = new BackgroundDocumentGenerator(Dispatcher, DynamicFileInfoProvider)
            {
                Delay = TimeSpan.FromMilliseconds(1),
                NotifyBackgroundWorkCompleted = new ManualResetEventSlim(initialState: false),
                NotifyErrorBeingReported = new ManualResetEventSlim(initialState: false),
            };

            queue.Initialize(projectManager);

            // Act & Assert
            queue.Enqueue(project, project.GetDocument(Documents[0].FilePath));

            await Task.Run(() => queue.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3)));

            Assert.False(queue.NotifyErrorBeingReported.IsSet);
        }

        [UIFact]
        public async Task ProcessDocument_SwallowsUnauthorizedAccessExceptions()
        {
            // Arrange
            var projectManager = new TestProjectSnapshotManager(Dispatcher, Workspace);
            projectManager.ProjectAdded(HostProject1);

            var textLoader = new Mock<TextLoader>(MockBehavior.Strict);
            textLoader.Setup(loader => loader.LoadTextAndVersionAsync(It.IsAny<Workspace>(), It.IsAny<DocumentId>(), It.IsAny<CancellationToken>()))
                .Throws<UnauthorizedAccessException>();
            projectManager.DocumentAdded(HostProject1, Documents[0], textLoader.Object);

            var project = projectManager.GetLoadedProject(HostProject1.FilePath);

            var queue = new BackgroundDocumentGenerator(Dispatcher, DynamicFileInfoProvider)
            {
                Delay = TimeSpan.FromMilliseconds(1),
                NotifyBackgroundWorkCompleted = new ManualResetEventSlim(initialState: false),
                NotifyErrorBeingReported = new ManualResetEventSlim(initialState: false),
            };

            queue.Initialize(projectManager);

            // Act & Assert
            queue.Enqueue(project, project.GetDocument(Documents[0].FilePath));

            await Task.Run(() => queue.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3)));

            Assert.False(queue.NotifyErrorBeingReported.IsSet);
        }

        [UIFact]
        public async Task Queue_ProcessesNotifications_AndGoesBackToSleep()
        {
            // Arrange
            var projectManager = new TestProjectSnapshotManager(Dispatcher, Workspace);
            projectManager.ProjectAdded(HostProject1);
            projectManager.ProjectAdded(HostProject2);
            projectManager.DocumentAdded(HostProject1, Documents[0], null);
            projectManager.DocumentAdded(HostProject1, Documents[1], null);

            var project = projectManager.GetLoadedProject(HostProject1.FilePath);

            var queue = new BackgroundDocumentGenerator(Dispatcher, DynamicFileInfoProvider)
            {
                Delay = TimeSpan.FromMilliseconds(1),
                BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false),
                NotifyBackgroundWorkStarting = new ManualResetEventSlim(initialState: false),
                BlockBackgroundWorkCompleting = new ManualResetEventSlim(initialState: false),
                NotifyBackgroundWorkCompleted = new ManualResetEventSlim(initialState: false),
            };

            queue.Initialize(projectManager);

            // Act & Assert
            queue.Enqueue(project, project.GetDocument(Documents[0].FilePath));

            Assert.True(queue.IsScheduledOrRunning, "Queue should be scheduled during Enqueue");
            Assert.True(queue.HasPendingNotifications, "Queue should have a notification created during Enqueue");

            // Allow the background work to proceed.
            queue.BlockBackgroundWorkStart.Set();
            queue.BlockBackgroundWorkCompleting.Set();

            await Task.Run(() => queue.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3)));

            Assert.False(queue.IsScheduledOrRunning, "Queue should not have restarted");
            Assert.False(queue.HasPendingNotifications, "Queue should have processed all notifications");
        }

        [UIFact]
        public async Task Queue_ProcessesNotifications_AndRestarts()
        {
            // Arrange
            var projectManager = new TestProjectSnapshotManager(Dispatcher, Workspace);
            projectManager.ProjectAdded(HostProject1);
            projectManager.ProjectAdded(HostProject2);
            projectManager.DocumentAdded(HostProject1, Documents[0], null);
            projectManager.DocumentAdded(HostProject1, Documents[1], null);

            var project = projectManager.GetLoadedProject(HostProject1.FilePath);

            var queue = new BackgroundDocumentGenerator(Dispatcher, DynamicFileInfoProvider)
            {
                Delay = TimeSpan.FromMilliseconds(1),
                BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false),
                NotifyBackgroundWorkStarting = new ManualResetEventSlim(initialState: false),
                NotifyBackgroundCapturedWorkload = new ManualResetEventSlim(initialState: false),
                BlockBackgroundWorkCompleting = new ManualResetEventSlim(initialState: false),
                NotifyBackgroundWorkCompleted = new ManualResetEventSlim(initialState: false),
            };

            queue.Initialize(projectManager);

            // Act & Assert
            queue.Enqueue(project, project.GetDocument(Documents[0].FilePath));

            Assert.True(queue.IsScheduledOrRunning, "Queue should be scheduled during Enqueue");
            Assert.True(queue.HasPendingNotifications, "Queue should have a notification created during Enqueue");

            // Allow the background work to start.
            queue.BlockBackgroundWorkStart.Set();

            await Task.Run(() => queue.NotifyBackgroundWorkStarting.Wait(TimeSpan.FromSeconds(1)));

            Assert.True(queue.IsScheduledOrRunning, "Worker should be processing now");

            await Task.Run(() => queue.NotifyBackgroundCapturedWorkload.Wait(TimeSpan.FromSeconds(1)));
            Assert.False(queue.HasPendingNotifications, "Worker should have taken all notifications");

            queue.Enqueue(project, project.GetDocument(Documents[1].FilePath));
            Assert.True(queue.HasPendingNotifications); // Now we should see the worker restart when it finishes.

            // Allow work to complete, which should restart the timer.
            queue.BlockBackgroundWorkCompleting.Set();

            await Task.Run(() => queue.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3)));
            queue.NotifyBackgroundWorkCompleted.Reset();

            // It should start running again right away.
            Assert.True(queue.IsScheduledOrRunning, "Queue should be scheduled during Enqueue");
            Assert.True(queue.HasPendingNotifications, "Queue should have a notification created during Enqueue");

            // Allow the background work to proceed.
            queue.BlockBackgroundWorkStart.Set();

            queue.BlockBackgroundWorkCompleting.Set();
            await Task.Run(() => queue.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3)));

            Assert.False(queue.IsScheduledOrRunning, "Queue should not have restarted");
            Assert.False(queue.HasPendingNotifications, "Queue should have processed all notifications");
        }

        [UIFact(Skip = "https://github.com/dotnet/aspnetcore/issues/14805")]
        public async Task DocumentChanged_ReparsesRelatedFiles()
        {
            // Arrange
            var projectManager = new TestProjectSnapshotManager(Dispatcher, Workspace)
            {
                AllowNotifyListeners = true,
            };
            var documents = new[]
            {
                TestProjectData.SomeProjectComponentFile1,
                TestProjectData.SomeProjectImportFile
            };
            projectManager.ProjectAdded(HostProject1);
            for (var i = 0; i < documents.Length; i++)
            {
                projectManager.DocumentAdded(HostProject1, documents[i], null);
            }

            var queue = new BackgroundDocumentGenerator(Dispatcher, DynamicFileInfoProvider)
            {
                Delay = TimeSpan.FromMilliseconds(1),
                BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false),
                NotifyBackgroundWorkStarting = new ManualResetEventSlim(initialState: false),
                NotifyBackgroundCapturedWorkload = new ManualResetEventSlim(initialState: false),
                BlockBackgroundWorkCompleting = new ManualResetEventSlim(initialState: false),
                NotifyBackgroundWorkCompleted = new ManualResetEventSlim(initialState: false),
            };

            var changedSourceText = SourceText.From("@inject DateTime Time");
            queue.Initialize(projectManager);

            // Act & Assert
            projectManager.DocumentChanged(HostProject1.FilePath, TestProjectData.SomeProjectImportFile.FilePath, changedSourceText);

            Assert.True(queue.IsScheduledOrRunning, "Queue should be scheduled during Enqueue");
            Assert.True(queue.HasPendingNotifications, "Queue should have a notification created during Enqueue");

            for (var i = 0; i < documents.Length; i++)
            {
                var key = new DocumentKey(HostProject1.FilePath, documents[i].FilePath);
                Assert.True(queue.Work.ContainsKey(key));
            }

            // Allow the background work to start.
            queue.BlockBackgroundWorkStart.Set();

            await Task.Run(() => queue.NotifyBackgroundWorkStarting.Wait(TimeSpan.FromSeconds(1)));

            Assert.True(queue.IsScheduledOrRunning, "Worker should be processing now");

            await Task.Run(() => queue.NotifyBackgroundCapturedWorkload.Wait(TimeSpan.FromSeconds(1)));
            Assert.False(queue.HasPendingNotifications, "Worker should have taken all notifications");

            // Allow work to complete
            queue.BlockBackgroundWorkCompleting.Set();

            await Task.Run(() => queue.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3)));

            Assert.False(queue.HasPendingNotifications, "Queue should have processed all notifications");
            Assert.False(queue.IsScheduledOrRunning, "Queue should not have restarted");
        }

        [UIFact]
        public async Task DocumentRemoved_ReparsesRelatedFiles()
        {
            // Arrange
            var projectManager = new TestProjectSnapshotManager(Dispatcher, Workspace)
            {
                AllowNotifyListeners = true,
            };
            projectManager.ProjectAdded(HostProject1);
            projectManager.DocumentAdded(HostProject1, TestProjectData.SomeProjectComponentFile1, null);
            projectManager.DocumentAdded(HostProject1, TestProjectData.SomeProjectImportFile, null);

            var queue = new BackgroundDocumentGenerator(Dispatcher, DynamicFileInfoProvider)
            {
                Delay = TimeSpan.FromMilliseconds(1),
                BlockBackgroundWorkStart = new ManualResetEventSlim(initialState: false),
                NotifyBackgroundWorkStarting = new ManualResetEventSlim(initialState: false),
                NotifyBackgroundCapturedWorkload = new ManualResetEventSlim(initialState: false),
                BlockBackgroundWorkCompleting = new ManualResetEventSlim(initialState: false),
                NotifyBackgroundWorkCompleted = new ManualResetEventSlim(initialState: false),
            };

            queue.Initialize(projectManager);

            // Act & Assert
            projectManager.DocumentRemoved(HostProject1, TestProjectData.SomeProjectImportFile);

            Assert.True(queue.IsScheduledOrRunning, "Queue should be scheduled during Enqueue");
            Assert.True(queue.HasPendingNotifications, "Queue should have a notification created during Enqueue");

            var kvp = Assert.Single(queue.Work);
            var expectedKey = new DocumentKey(HostProject1.FilePath, TestProjectData.SomeProjectComponentFile1.FilePath);
            Assert.Equal(expectedKey, kvp.Key);

            // Allow the background work to start.
            queue.BlockBackgroundWorkStart.Set();

            await Task.Run(() => queue.NotifyBackgroundWorkStarting.Wait(TimeSpan.FromSeconds(1)));

            Assert.True(queue.IsScheduledOrRunning, "Worker should be processing now");

            await Task.Run(() => queue.NotifyBackgroundCapturedWorkload.Wait(TimeSpan.FromSeconds(1)));
            Assert.False(queue.HasPendingNotifications, "Worker should have taken all notifications");

            // Allow work to complete
            queue.BlockBackgroundWorkCompleting.Set();

            await Task.Run(() => queue.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromSeconds(3)));

            Assert.False(queue.HasPendingNotifications, "Queue should have processed all notifications");
            Assert.False(queue.IsScheduledOrRunning, "Queue should not have restarted");
        }

        private class TestDynamicFileInfoProvider : RazorDynamicFileInfoProvider
        {
            private readonly Dictionary<string, DynamicDocumentContainer> _dynamicDocuments;

            public TestDynamicFileInfoProvider()
            {
                _dynamicDocuments = new Dictionary<string, DynamicDocumentContainer>();
            }

            public IReadOnlyDictionary<string, DynamicDocumentContainer> DynamicDocuments => _dynamicDocuments;

            public override void SuppressDocument(string projectFilePath, string documentFilePath)
            {
                _dynamicDocuments[documentFilePath] = null;
            }

            public override void UpdateFileInfo(string projectFilePath, DynamicDocumentContainer documentContainer)
            {
                _dynamicDocuments[documentContainer.FilePath] = documentContainer;
            }

            public override void UpdateLSPFileInfo(Uri documentUri, DynamicDocumentContainer documentContainer)
            {
                _dynamicDocuments[documentContainer.FilePath] = documentContainer;
            }
        }
    }
}
