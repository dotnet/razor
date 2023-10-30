// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class RazorFileChangeDetector : IFileChangeDetector, IDisposable
{
    private static readonly ImmutableArray<string> s_razorFileExtensions = ImmutableArray.Create(".razor", ".cshtml");

    // Internal for testing
    internal readonly Dictionary<string, DelayedFileChangeNotification> PendingNotifications;

    private readonly ProjectSnapshotManagerDispatcher _dispatcher;
    private readonly ImmutableArray<IRazorFileChangeListener> _listeners;
    private readonly List<FileSystemWatcher> _watchers;
    private readonly SemaphoreSlim _pendingNotificationsLock = new(1, 1);

    private static readonly string[] s_ignoredDirectories =
    [
        "node_modules",
    ];

    public RazorFileChangeDetector(ProjectSnapshotManagerDispatcher dispatcher, IEnumerable<IRazorFileChangeListener> listeners)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        if (listeners is null)
        {
            throw new ArgumentNullException(nameof(listeners));
        }

        _listeners = listeners.ToImmutableArray();

        _watchers = new List<FileSystemWatcher>(s_razorFileExtensions.Length);
        PendingNotifications = new Dictionary<string, DelayedFileChangeNotification>(FilePathComparer.Instance);
    }

    public void Dispose()
    {
        _pendingNotificationsLock.Dispose();
    }

    // Internal for testing
    internal int EnqueueDelay { get; set; } = 1000;

    // Used in tests to ensure we can control when delayed notification work starts.
    internal ManualResetEventSlim? BlockNotificationWorkStart { get; set; }

    // Used in tests to ensure we can understand when notification work noops.
    internal ManualResetEventSlim? NotifyNotificationNoop { get; set; }

    public async Task StartAsync(string workspaceDirectory, CancellationToken cancellationToken)
    {
        if (workspaceDirectory is null)
        {
            throw new ArgumentNullException(nameof(workspaceDirectory));
        }

        // Dive through existing Razor files and fabricate "added" events so listeners can accurately listen to state changes for them.

        workspaceDirectory = FilePathNormalizer.Normalize(workspaceDirectory);

        var existingRazorFiles = GetExistingRazorFiles(workspaceDirectory);

        foreach (var razorFilePath in existingRazorFiles)
        {
            await FileSystemWatcher_RazorFileEventAsync(razorFilePath, RazorFileChangeKind.Added, cancellationToken).ConfigureAwait(false);
        }

        // This is an entry point for testing
        OnInitializationFinished();

        if (cancellationToken.IsCancellationRequested)
        {
            // Client cancelled connection, no need to setup any file watchers. Server is about to tear down.
            return;
        }

        // Start listening for project file changes (added/removed/renamed).

        foreach (var extension in s_razorFileExtensions)
        {
            var watcher = new RazorFileSystemWatcher(workspaceDirectory, "*" + extension)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                IncludeSubdirectories = true,
            };

            watcher.Created += (sender, args) => FileSystemWatcher_RazorFileEvent_Background(args.FullPath, RazorFileChangeKind.Added);
            watcher.Deleted += (sender, args) => FileSystemWatcher_RazorFileEvent_Background(args.FullPath, RazorFileChangeKind.Removed);
            watcher.Renamed += (sender, args) =>
            {
                // Translate file renames into remove->add

                if (args.OldFullPath.EndsWith(extension, FilePathComparison.Instance))
                {
                    // Renaming from Razor file to something else.
                    FileSystemWatcher_RazorFileEvent_Background(args.OldFullPath, RazorFileChangeKind.Removed);
                }

                if (args.FullPath.EndsWith(extension, FilePathComparison.Instance))
                {
                    // Renaming to a Razor file.
                    FileSystemWatcher_RazorFileEvent_Background(args.FullPath, RazorFileChangeKind.Added);
                }
            };

            watcher.EnableRaisingEvents = true;

            _watchers.Add(watcher);
        }
    }

    public void Stop()
    {
        // We're relying on callers to synchronize start/stops so we don't need to ensure one happens before the other.

        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }

        _watchers.Clear();
    }

    // Protected virtual for testing
    protected virtual void OnInitializationFinished()
    {
    }

    // Protected virtual for testing
    protected virtual ImmutableArray<string> GetExistingRazorFiles(string workspaceDirectory)
    {
        using var builder = new PooledArrayBuilder<string>();

        foreach (var extension in s_razorFileExtensions)
        {
            var existingFiles = DirectoryHelper.GetFilteredFiles(workspaceDirectory, "*" + extension, s_ignoredDirectories);
            builder.AddRange(existingFiles);
        }

        return builder.DrainToImmutable();
    }

    // Internal for testing
    internal void FileSystemWatcher_RazorFileEvent_Background(string physicalFilePath, RazorFileChangeKind kind)
    {
        _pendingNotificationsLock.Wait();
        try
        {
            if (!PendingNotifications.TryGetValue(physicalFilePath, out var currentNotification))
            {
                currentNotification = new DelayedFileChangeNotification();
                PendingNotifications[physicalFilePath] = currentNotification;
            }

            if (currentNotification.ChangeKind != null)
            {
                // We've already has a file change event for this file. Chances are we need to normalize the result.

                Debug.Assert(currentNotification.ChangeKind == RazorFileChangeKind.Added || currentNotification.ChangeKind == RazorFileChangeKind.Removed);

                if (currentNotification.ChangeKind != kind)
                {
                    // Previous was added and current is removed OR previous was removed and current is added. Either way there's no
                    // actual change to notify, null it out.
                    currentNotification.ChangeKind = null;
                }
                else
                {
                    Debug.Fail($"Unexpected {kind} event because our prior tracked state was the same.");
                }
            }
            else
            {
                currentNotification.ChangeKind = kind;
            }

            // The notify task is only ever null when it's the first time we're being notified about a change to the corresponding file.
            currentNotification.NotifyTask ??= NotifyAfterDelayAsync(physicalFilePath);
        }
        finally
        {
            _pendingNotificationsLock.Release();
        }
    }

    private async Task NotifyAfterDelayAsync(string physicalFilePath)
    {
        await Task.Delay(EnqueueDelay).ConfigureAwait(false);

        OnStartingDelayedNotificationWork();

        await NotifyAfterDelay_ProjectSnapshotManagerDispatcherAsync(physicalFilePath, CancellationToken.None).ConfigureAwait(false);
    }

    private async ValueTask NotifyAfterDelay_ProjectSnapshotManagerDispatcherAsync(string physicalFilePath, CancellationToken cancellationToken)
    {
        await _pendingNotificationsLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!PendingNotifications.TryGetValue(physicalFilePath, out var notification))
            {
                Debug.Fail("We should always have an associated notification after delaying an update.");
                return;
            }

            PendingNotifications.Remove(physicalFilePath);

            if (notification.ChangeKind is not RazorFileChangeKind changeKind)
            {
                // The file to be notified has been brought back to its original state.
                // Aka Add -> Remove is equivalent to the file never having been added.

                OnNoopingNotificationWork();
                return;
            }

            await FileSystemWatcher_RazorFileEventAsync(physicalFilePath, changeKind, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pendingNotificationsLock.Release();
        }
    }

    private async ValueTask FileSystemWatcher_RazorFileEventAsync(string physicalFilePath, RazorFileChangeKind kind, CancellationToken cancellationToken)
    {
        foreach (var listener in _listeners)
        {
            await listener.RazorFileChangedAsync(physicalFilePath, kind, cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnStartingDelayedNotificationWork()
    {
        if (BlockNotificationWorkStart != null)
        {
            BlockNotificationWorkStart.Wait();
            BlockNotificationWorkStart.Reset();
        }
    }

    private void OnNoopingNotificationWork()
    {
        if (NotifyNotificationNoop != null)
        {
            NotifyNotificationNoop.Set();
        }
    }

    // Internal for testing
    internal class DelayedFileChangeNotification
    {
        public Task? NotifyTask { get; set; }

        public RazorFileChangeKind? ChangeKind { get; set; }
    }
}
