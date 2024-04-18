// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.CodeAnalysis.Razor;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

public class RazorFileChangeDetectorTest(ITestOutputHelper testOutput) : LanguageServerTestBase(testOutput)
{
    [Fact]
    public async Task StartAsync_NotifiesListenersOfExistingRazorFiles()
    {
        // Arrange
        var args1 = new List<(string FilePath, RazorFileChangeKind Kind)>();
        var listenerMock1 = new StrictMock<IRazorFileChangeListener>();
        listenerMock1
            .Setup(l => l.RazorFileChangedAsync(It.IsAny<string>(), It.IsAny<RazorFileChangeKind>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback((string filePath, RazorFileChangeKind kind, CancellationToken _) => args1.Add((filePath, kind)));

        var args2 = new List<(string FilePath, RazorFileChangeKind Kind)>();
        var listenerMock2 = new StrictMock<IRazorFileChangeListener>();
        listenerMock2
            .Setup(l => l.RazorFileChangedAsync(It.IsAny<string>(), It.IsAny<RazorFileChangeKind>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback((string filePath, RazorFileChangeKind kind, CancellationToken _) => args2.Add((filePath, kind)));

        string[] existingRazorFiles = ["c:/path/to/index.razor", "c:/other/path/_Host.cshtml"];
        var cts = new CancellationTokenSource();
        using var detector = new TestRazorFileChangeDetector(
            cts,
            Dispatcher,
            [listenerMock1.Object, listenerMock2.Object],
            existingRazorFiles);

        // Act
        await detector.StartAsync("/some/workspacedirectory", cts.Token);

        // Assert
        Assert.Collection(args1,
            args =>
            {
                Assert.Equal(RazorFileChangeKind.Added, args.Kind);
                Assert.Equal(existingRazorFiles[0], args.FilePath);
            },
            args =>
            {
                Assert.Equal(RazorFileChangeKind.Added, args.Kind);
                Assert.Equal(existingRazorFiles[1], args.FilePath);
            });
        Assert.Collection(args2,
            args =>
            {
                Assert.Equal(RazorFileChangeKind.Added, args.Kind);
                Assert.Equal(existingRazorFiles[0], args.FilePath);
            },
            args =>
            {
                Assert.Equal(RazorFileChangeKind.Added, args.Kind);
                Assert.Equal(existingRazorFiles[1], args.FilePath);
            });
    }

    [Fact]
    public async Task FileSystemWatcher_RazorFileEvent_Background_NotifiesChange()
    {
        // Arrange
        var filePath = "C:/path/to/file.razor";
        var changeKind = RazorFileChangeKind.Added;
        var listenerMock = new StrictMock<IRazorFileChangeListener>();
        listenerMock
            .Setup(l => l.RazorFileChangedAsync(filePath, changeKind, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        using var fileChangeDetector = new SimpleTestRazorFileChangeDetector(Dispatcher, [listenerMock.Object], TimeSpan.FromMilliseconds(50))
        {
            BlockNotificationWorkStart = new ManualResetEventSlim(initialState: false),
        };

        // Act
        fileChangeDetector.FileSystemWatcher_RazorFileEvent_Background(filePath, changeKind);

        // Assert

        // We acquire the notification prior to unblocking notification work because once we allow that work to proceed the notification will be removed.
        var notification = Assert.Single(fileChangeDetector.PendingNotifications);

        fileChangeDetector.BlockNotificationWorkStart.Set();

        await notification.Value.NotifyTask;

        listenerMock.VerifyAll();
    }

    [Fact]
    public void FileSystemWatcher_RazorFileEvent_Background_AddRemoveDoesNotNotify()
    {
        // Arrange
        var filePath = "C:/path/to/file.razor";
        var listenerCalled = false;
        var listenerMock = new StrictMock<IRazorFileChangeListener>();
        listenerMock
            .Setup(l => l.RazorFileChangedAsync(filePath, It.IsAny<RazorFileChangeKind>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => listenerCalled = true);

        using var fileChangeDetector = new SimpleTestRazorFileChangeDetector(Dispatcher, [listenerMock.Object], TimeSpan.FromMilliseconds(10))
        {
            NotifyNotificationNoop = new ManualResetEventSlim(initialState: false),
            BlockNotificationWorkStart = new ManualResetEventSlim(initialState: false)
        };

        // Act
        fileChangeDetector.FileSystemWatcher_RazorFileEvent_Background(filePath, RazorFileChangeKind.Added);
        fileChangeDetector.FileSystemWatcher_RazorFileEvent_Background(filePath, RazorFileChangeKind.Removed);

        // Assert
        fileChangeDetector.BlockNotificationWorkStart.Set();
        Assert.True(fileChangeDetector.NotifyNotificationNoop.Wait(TimeSpan.FromSeconds(10)));
        Assert.False(listenerCalled);
    }

    [Fact]
    public async Task FileSystemWatcher_RazorFileEvent_Background_NotificationNoopToAdd_NotifiesAddedOnce()
    {
        // Arrange
        var filePath = "C:/path/to/file.razor";
        var listenerMock = new StrictMock<IRazorFileChangeListener>();
        var callCount = 0;
        listenerMock
            .Setup(l => l.RazorFileChangedAsync(filePath, RazorFileChangeKind.Added, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => callCount++);

        using var fileChangeDetector = new SimpleTestRazorFileChangeDetector(Dispatcher, [listenerMock.Object], TimeSpan.FromMilliseconds(50))
        {
            BlockNotificationWorkStart = new ManualResetEventSlim(initialState: false),
        };

        // Act
        fileChangeDetector.FileSystemWatcher_RazorFileEvent_Background(filePath, RazorFileChangeKind.Added);
        fileChangeDetector.FileSystemWatcher_RazorFileEvent_Background(filePath, RazorFileChangeKind.Removed);
        fileChangeDetector.FileSystemWatcher_RazorFileEvent_Background(filePath, RazorFileChangeKind.Added);

        // Assert

        // We acquire the notification prior to unblocking notification work because once we allow that work to proceed the notification will be removed.
        var notification = Assert.Single(fileChangeDetector.PendingNotifications);

        fileChangeDetector.BlockNotificationWorkStart.Set();

        await notification.Value.NotifyTask;

        Assert.Equal(1, callCount);
    }

    private class SimpleTestRazorFileChangeDetector(
        ProjectSnapshotManagerDispatcher dispatcher,
        IEnumerable<IRazorFileChangeListener> listeners,
        TimeSpan delay)
        : RazorFileChangeDetector(dispatcher, listeners, delay)
    {
    }

    private class TestRazorFileChangeDetector : RazorFileChangeDetector
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly IReadOnlyList<string> _existingProjectFiles;

        public TestRazorFileChangeDetector(
            CancellationTokenSource cancellationTokenSource,
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            IEnumerable<IRazorFileChangeListener> listeners,
            IReadOnlyList<string> existingProjectFiles)
            : base(projectSnapshotManagerDispatcher, listeners)
        {
            _cancellationTokenSource = cancellationTokenSource;
            _existingProjectFiles = existingProjectFiles;
        }

        protected override void OnInitializationFinished()
        {
            // Once initialization has finished we want to ensure that no file watchers are created so cancel!
            _cancellationTokenSource.Cancel();
        }

        protected override IReadOnlyList<string> GetExistingRazorFiles(string workspaceDirectory)
        {
            return _existingProjectFiles;
        }
    }
}
