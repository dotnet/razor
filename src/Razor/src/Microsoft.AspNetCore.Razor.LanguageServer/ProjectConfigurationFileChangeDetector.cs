﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class ProjectConfigurationFileChangeDetector : IFileChangeDetector
{
    private readonly ProjectSnapshotManagerDispatcher _dispatcher;
    private readonly IEnumerable<IProjectConfigurationFileChangeListener> _listeners;
    private readonly LanguageServerFeatureOptions _options;
    private readonly ILogger _logger;
    private FileSystemWatcher? _watcher;

    private static readonly IReadOnlyCollection<string> s_ignoredDirectories = new string[]
    {
        "node_modules",
        "bin",
        ".vs",
    };

    public ProjectConfigurationFileChangeDetector(
        ProjectSnapshotManagerDispatcher dispatcher,
        IEnumerable<IProjectConfigurationFileChangeListener> listeners,
        LanguageServerFeatureOptions options,
        ILoggerFactory loggerFactory)
    {
        if (loggerFactory is null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _listeners = listeners ?? throw new ArgumentNullException(nameof(listeners));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = loggerFactory.CreateLogger<ProjectConfigurationFileChangeDetector>();
    }

    public async Task StartAsync(string workspaceDirectory, CancellationToken cancellationToken)
    {
        if (workspaceDirectory is null)
        {
            throw new ArgumentNullException(nameof(workspaceDirectory));
        }

        // Dive through existing project configuration files and fabricate "added" events so listeners can accurately listen to state changes for them.

        workspaceDirectory = FilePathNormalizer.Normalize(workspaceDirectory);
        var existingConfigurationFiles = GetExistingConfigurationFiles(workspaceDirectory);

        _logger.LogDebug("Triggering events for existing project configuration files");
        await _dispatcher.RunOnDispatcherThreadAsync(() =>
        {
            foreach (var configurationFilePath in existingConfigurationFiles)
            {
                FileSystemWatcher_ProjectConfigurationFileEvent(configurationFilePath, RazorFileChangeKind.Added);
            }
        }, cancellationToken).ConfigureAwait(false);

        // This is an entry point for testing
        OnInitializationFinished();

        if (cancellationToken.IsCancellationRequested)
        {
            // Client cancelled connection, no need to setup any file watchers. Server is about to tear down.
            return;
        }

        _watcher = new RazorFileSystemWatcher(workspaceDirectory, _options.ProjectConfigurationFileName)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            IncludeSubdirectories = true,
        };

        _watcher.Created += (sender, args) => FileSystemWatcher_ProjectConfigurationFileEvent_Background(args.FullPath, RazorFileChangeKind.Added);
        _watcher.Deleted += (sender, args) => FileSystemWatcher_ProjectConfigurationFileEvent_Background(args.FullPath, RazorFileChangeKind.Removed);
        _watcher.Changed += (sender, args) => FileSystemWatcher_ProjectConfigurationFileEvent_Background(args.FullPath, RazorFileChangeKind.Changed);
        _watcher.Renamed += (sender, args) =>
        {
            // Translate file renames into remove / add

            if (args.OldFullPath.EndsWith(_options.ProjectConfigurationFileName, FilePathComparison.Instance))
            {
                // Renaming from project configuration file to something else. Just remove the configuration file.
                FileSystemWatcher_ProjectConfigurationFileEvent_Background(args.OldFullPath, RazorFileChangeKind.Removed);
            }
            else if (args.FullPath.EndsWith(_options.ProjectConfigurationFileName, FilePathComparison.Instance))
            {
                // Renaming from a non-project configuration file file to a real one. Just add the configuration file.
                FileSystemWatcher_ProjectConfigurationFileEvent_Background(args.FullPath, RazorFileChangeKind.Added);
            }
        };

        _watcher.EnableRaisingEvents = true;
    }

    public void Stop()
    {
        // We're relying on callers to synchronize start/stops so we don't need to ensure one happens before the other.

        _watcher?.Dispose();
        _watcher = null;
    }

    // Protected virtual for testing
    protected virtual void OnInitializationFinished()
    {
    }

    // Protected virtual for testing
    protected virtual IEnumerable<string> GetExistingConfigurationFiles(string workspaceDirectory)
    {
        using var _ = _logger.BeginScope("Searching for existing project configuration files");

        return DirectoryHelper.GetFilteredFiles(
            workspaceDirectory,
            _options.ProjectConfigurationFileName,
            s_ignoredDirectories,
            logger: _logger);
    }

    private void FileSystemWatcher_ProjectConfigurationFileEvent_Background(string physicalFilePath, RazorFileChangeKind kind)
    {
        _ = _dispatcher.RunOnDispatcherThreadAsync(
            () => FileSystemWatcher_ProjectConfigurationFileEvent(physicalFilePath, kind),
            CancellationToken.None);
    }

    private void FileSystemWatcher_ProjectConfigurationFileEvent(string physicalFilePath, RazorFileChangeKind kind)
    {
        var args = new ProjectConfigurationFileChangeEventArgs(physicalFilePath, kind);
        foreach (var listener in _listeners)
        {
            _logger.LogDebug("Notifying listener '{Listener}' of config file path '{PhysicalFilePath}' change with kind '{Kind}'", listener, physicalFilePath, kind);
            listener.ProjectConfigurationFileChanged(args);
        }
    }
}
