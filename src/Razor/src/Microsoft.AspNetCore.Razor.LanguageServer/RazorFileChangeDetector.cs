﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal partial class RazorFileChangeDetector : IFileChangeDetector, IDisposable
{
    private static readonly TimeSpan s_delay = TimeSpan.FromSeconds(1);
    private static readonly ImmutableArray<string> s_razorFileExtensions = [".razor", ".cshtml"];
    private static readonly string[] s_ignoredDirectories = ["node_modules"];

    private readonly ImmutableArray<IRazorFileChangeListener> _listeners;

    private readonly CancellationTokenSource _disposeTokenSource;
    private readonly AsyncBatchingWorkQueue<(string, RazorFileChangeKind)> _workQueue;
    private readonly Dictionary<string, (RazorFileChangeKind kind, int index)> _filePathToChangeMap;
    private readonly HashSet<int> _indicesToSkip;
    private readonly List<FileSystemWatcher> _watchers;

    public RazorFileChangeDetector(IEnumerable<IRazorFileChangeListener> listeners)
        : this(listeners, s_delay)
    {
    }

    protected RazorFileChangeDetector(IEnumerable<IRazorFileChangeListener> listeners, TimeSpan delay)
    {
        _listeners = listeners.ToImmutableArray();

        _disposeTokenSource = new();
        _workQueue = new AsyncBatchingWorkQueue<(string, RazorFileChangeKind)>(delay, ProcessBatchAsync, _disposeTokenSource.Token);
        _filePathToChangeMap = new(FilePathComparer.Instance);
        _indicesToSkip = [];
        _watchers = new List<FileSystemWatcher>(s_razorFileExtensions.Length);
    }

    public void Dispose()
    {
        if (_disposeTokenSource.IsCancellationRequested)
        {
            return;
        }

        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
    }

    private async ValueTask ProcessBatchAsync(ImmutableArray<(string, RazorFileChangeKind)> items, CancellationToken token)
    {
        // Clear out our helper collections.
        _filePathToChangeMap.Clear();
        _indicesToSkip.Clear();

        // First, collect all of the file paths and note the indices add/remove change pairs for the same file path.
        using var potentialItems = new PooledArrayBuilder<string>(capacity: items.Length);

        var index = 0;

        foreach (var (filePath, kind) in items)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (_filePathToChangeMap.TryGetValue(filePath, out var value))
            {
                // We've already seen this file path, so we should skip it later.
                _indicesToSkip.Add(index);

                var (existingKind, existingIndex) = value;

                // We only ever get added or removed. So, if we've already received an add and are getting
                // a remove, there's no need to send the notification. Likewise, if we've received a remove
                // and are getting an add, we can just elide this notification altogether.
                if (kind != existingKind)
                {
                    _filePathToChangeMap.Remove(filePath);
                    _indicesToSkip.Add(existingIndex);
                }
                else
                {
                    Debug.Fail($"Unexpected {kind} event because our prior tracked state was the same.");
                }
            }
            else
            {
                _filePathToChangeMap.Add(filePath, (kind, index));
            }

            potentialItems.Add(filePath);
            index++;
        }

        // Now, loop through all of the file paths we collected and notify listeners of changes,
        // taking care of to skip any indices that we noted earlier.
        for (var i = 0; i < potentialItems.Count; i++)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (_indicesToSkip.Contains(i))
            {
                continue;
            }

            var filePath = potentialItems[i];

            if (!_filePathToChangeMap.TryGetValue(filePath, out var value))
            {
                continue;
            }

            // We only send notifications for the changes that we kept.
            foreach (var listener in _listeners)
            {
                await listener.RazorFileChangedAsync(filePath, value.kind, token).ConfigureAwait(false);
            }
        }
    }

    public async Task StartAsync(string workspaceDirectory, CancellationToken cancellationToken)
    {
        // Dive through existing Razor files and fabricate "added" events so listeners can accurately listen to state changes for them.

        workspaceDirectory = FilePathNormalizer.Normalize(workspaceDirectory);

        var existingRazorFiles = GetExistingRazorFiles(workspaceDirectory);

        foreach (var razorFilePath in existingRazorFiles)
        {
            foreach (var listener in _listeners)
            {
                await listener.RazorFileChangedAsync(razorFilePath, RazorFileChangeKind.Added, cancellationToken).ConfigureAwait(false);
            }
        }

        if (!InitializeFileWatchers)
        {
            return;
        }

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

            watcher.Created += (sender, args) => _workQueue.AddWork((args.FullPath, RazorFileChangeKind.Added));
            watcher.Deleted += (sender, args) => _workQueue.AddWork((args.FullPath, RazorFileChangeKind.Removed));
            watcher.Renamed += (sender, args) =>
            {
                // Translate file renames into remove->add

                if (args.OldFullPath.EndsWith(extension, FilePathComparison.Instance))
                {
                    // Renaming from Razor file to something else.
                    _workQueue.AddWork((args.OldFullPath, RazorFileChangeKind.Removed));
                }

                if (args.FullPath.EndsWith(extension, FilePathComparison.Instance))
                {
                    // Renaming to a Razor file.
                    _workQueue.AddWork((args.FullPath, RazorFileChangeKind.Added));
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
    protected virtual bool InitializeFileWatchers => true;

    // Protected virtual for testing
    protected virtual ImmutableArray<string> GetExistingRazorFiles(string workspaceDirectory)
    {
        using var result = new PooledArrayBuilder<string>();

        foreach (var extension in s_razorFileExtensions)
        {
            var existingFiles = DirectoryHelper.GetFilteredFiles(workspaceDirectory, "*" + extension, s_ignoredDirectories);
            result.AddRange(existingFiles);
        }

        return result.DrainToImmutable();
    }
}
