// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class OpenDocumentGeneratorTest : LanguageServerTestBase
    {
        public OpenDocumentGeneratorTest()
        {
            Documents = new HostDocument[]
            {
                new HostDocument("c:/Test1/Index.cshtml", "Index.cshtml"),
                new HostDocument("c:/Test1/Components/Counter.cshtml", "Components/Counter.cshtml"),
            };

            HostProject1 = new HostProject("c:/Test1/Test1.csproj", RazorConfiguration.Default, "TestRootNamespace");
            HostProject2 = new HostProject("c:/Test2/Test2.csproj", RazorConfiguration.Default, "TestRootNamespace");
        }

        private HostDocument[] Documents { get; }

        private HostProject HostProject1 { get; }

        private HostProject HostProject2 { get; }

        [Fact]
        public async Task DocumentAdded_IgnoresClosedDocument()
        {
            // Arrange
            var projectManager = TestProjectSnapshotManager.Create(Dispatcher);
            var listener = new TestDocumentProcessedListener();
            var queue = new TestOpenDocumentGenerator(Dispatcher, listener);

            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(HostProject1);
                projectManager.ProjectAdded(HostProject2);
                projectManager.AllowNotifyListeners = true;

                queue.Initialize(projectManager);

                // Act
                projectManager.DocumentAdded(HostProject1, Documents[0], null);
            }, CancellationToken.None);

            // Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => listener.GetProcessedDocumentAsync(cancelAfter: TimeSpan.FromMilliseconds(50)));
        }

        [Fact]
        public async Task DocumentChanged_IgnoresClosedDocument()
        {
            // Arrange
            var projectManager = TestProjectSnapshotManager.Create(Dispatcher);
            var listener = new TestDocumentProcessedListener();
            var queue = new TestOpenDocumentGenerator(Dispatcher, listener);

            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(HostProject1);
                projectManager.ProjectAdded(HostProject2);
                projectManager.AllowNotifyListeners = true;
                projectManager.DocumentAdded(HostProject1, Documents[0], null);

                queue.Initialize(projectManager);

                // Act
                projectManager.DocumentChanged(HostProject1.FilePath, Documents[0].FilePath, SourceText.From("new"));
            }, CancellationToken.None);

            // Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => listener.GetProcessedDocumentAsync(cancelAfter: TimeSpan.FromMilliseconds(50)));
        }

        [Fact]
        public async Task DocumentChanged_ProcessesOpenDocument()
        {
            // Arrange
            var projectManager = TestProjectSnapshotManager.Create(Dispatcher);
            var listener = new TestDocumentProcessedListener();
            var queue = new TestOpenDocumentGenerator(Dispatcher, listener);

            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(HostProject1);
                projectManager.ProjectAdded(HostProject2);
                projectManager.AllowNotifyListeners = true;
                projectManager.DocumentAdded(HostProject1, Documents[0], null);
                projectManager.DocumentOpened(HostProject1.FilePath, Documents[0].FilePath, SourceText.From(string.Empty));

                queue.Initialize(projectManager);

                // Act
                projectManager.DocumentChanged(HostProject1.FilePath, Documents[0].FilePath, SourceText.From("new"));
            }, CancellationToken.None);

            // Assert

            var document = await listener.GetProcessedDocumentAsync(cancelAfter: TimeSpan.FromSeconds(10));
            Assert.Equal(document.FilePath, Documents[0].FilePath);
        }

        [Fact]
        public async Task ProjectChanged_IgnoresClosedDocument()
        {
            // Arrange
            var projectManager = TestProjectSnapshotManager.Create(Dispatcher);
            var listener = new TestDocumentProcessedListener();
            var queue = new TestOpenDocumentGenerator(Dispatcher, listener);

            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(HostProject1);
                projectManager.ProjectAdded(HostProject2);
                projectManager.AllowNotifyListeners = true;
                projectManager.DocumentAdded(HostProject1, Documents[0], null);

                queue.Initialize(projectManager);

                // Act
                projectManager.ProjectWorkspaceStateChanged(HostProject1.FilePath, new ProjectWorkspaceState(Array.Empty<TagHelperDescriptor>(), LanguageVersion.CSharp8));
            }, CancellationToken.None);

            // Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => listener.GetProcessedDocumentAsync(cancelAfter: TimeSpan.FromMilliseconds(50)));
        }

        [Fact]
        public async Task ProjectChanged_ProcessesOpenDocument()
        {
            // Arrange
            var projectManager = TestProjectSnapshotManager.Create(Dispatcher);
            var listener = new TestDocumentProcessedListener();
            var queue = new TestOpenDocumentGenerator(Dispatcher, listener);

            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(HostProject1);
                projectManager.ProjectAdded(HostProject2);
                projectManager.AllowNotifyListeners = true;
                projectManager.DocumentAdded(HostProject1, Documents[0], null);
                projectManager.DocumentOpened(HostProject1.FilePath, Documents[0].FilePath, SourceText.From(string.Empty));

                queue.Initialize(projectManager);

                // Act
                projectManager.ProjectWorkspaceStateChanged(HostProject1.FilePath, new ProjectWorkspaceState(Array.Empty<TagHelperDescriptor>(), LanguageVersion.CSharp8));
            }, CancellationToken.None);

            // Assert

            var document = await listener.GetProcessedDocumentAsync(cancelAfter: TimeSpan.FromSeconds(10));
            Assert.Equal(document.FilePath, Documents[0].FilePath);
        }

        private class TestOpenDocumentGenerator : OpenDocumentGenerator
        {
            public TestOpenDocumentGenerator(
                ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
                params DocumentProcessedListener[] listeners)
                : base(listeners, projectSnapshotManagerDispatcher, new DefaultErrorReporter())
            {
            }
        }

        private class TestDocumentProcessedListener : DocumentProcessedListener
        {
            private readonly TaskCompletionSource<DocumentSnapshot> _tcs;

            public TestDocumentProcessedListener()
            {
                _tcs = new TaskCompletionSource<DocumentSnapshot>();
            }

            public Task<DocumentSnapshot> GetProcessedDocumentAsync(TimeSpan cancelAfter)
            {
                var cts = new CancellationTokenSource(cancelAfter);
                var registration = cts.Token.Register(() => _tcs.SetCanceled(cts.Token));
                _ = _tcs.Task.ContinueWith(
                    (t) =>
                    {
                        registration.Dispose();
                        cts.Dispose();
                    },
                    TaskScheduler.Current);

                return _tcs.Task;
            }

            public override void DocumentProcessed(DocumentSnapshot document)
            {
                _tcs.SetResult(document);
            }

            public override void Initialize(ProjectSnapshotManager projectManager)
            {
            }
        }
    }
}
