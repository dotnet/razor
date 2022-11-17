﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.Build.Execution;
using OmniSharp.MSBuild.Notification;

namespace Microsoft.AspNetCore.Razor.OmniSharpPlugin;

[Shared]
[Export(typeof(IMSBuildEventSink))]
internal class MSBuildProjectDocumentChangeDetector : IMSBuildEventSink
{
    private const string MSBuildProjectFullPathPropertyName = "MSBuildProjectFullPath";
    private const string MSBuildProjectDirectoryPropertyName = "MSBuildProjectDirectory";
    private static readonly IReadOnlyList<string> s_razorFileExtensions = new[] { ".razor", ".cshtml" };

    private readonly Dictionary<string, IReadOnlyList<FileSystemWatcher>> _watcherMap;
    private readonly IReadOnlyList<IRazorDocumentChangeListener> _documentChangeListeners;
    private readonly List<IRazorDocumentOutputChangeListener> _documentOutputChangeListeners;

    [ImportingConstructor]
    public MSBuildProjectDocumentChangeDetector(
        [ImportMany] IEnumerable<IRazorDocumentChangeListener> documentChangeListeners,
        [ImportMany] IEnumerable<IRazorDocumentOutputChangeListener> documentOutputChangeListeners)
    {
        if (documentChangeListeners is null)
        {
            throw new ArgumentNullException(nameof(documentChangeListeners));
        }

        if (documentOutputChangeListeners is null)
        {
            throw new ArgumentNullException(nameof(documentOutputChangeListeners));
        }

        _watcherMap = new Dictionary<string, IReadOnlyList<FileSystemWatcher>>(FilePathComparer.Instance);
        _documentChangeListeners = documentChangeListeners.ToList();
        _documentOutputChangeListeners = documentOutputChangeListeners.ToList();
    }

    public void ProjectLoaded(ProjectLoadedEventArgs loadedArgs)
    {
        if (loadedArgs is null)
        {
            throw new ArgumentNullException(nameof(loadedArgs));
        }

        var projectInstance = loadedArgs.ProjectInstance;
        var projectFilePath = projectInstance.GetPropertyValue(MSBuildProjectFullPathPropertyName);
        if (string.IsNullOrEmpty(projectFilePath))
        {
            // This should never be true but we're being extra careful.
            return;
        }

        var projectDirectory = projectInstance.GetPropertyValue(MSBuildProjectDirectoryPropertyName);
        if (string.IsNullOrEmpty(projectDirectory))
        {
            // This should never be true but we're beign extra careful.
            return;
        }

        if (_watcherMap.TryGetValue(projectDirectory, out var existingWatchers))
        {
            for (var i = 0; i < existingWatchers.Count; i++)
            {
                existingWatchers[i].Dispose();
            }
        }

        var watchers = new List<FileSystemWatcher>(s_razorFileExtensions.Count);
        for (var i = 0; i < s_razorFileExtensions.Count; i++)
        {
            var documentWatcher = new FileSystemWatcher(projectDirectory, "*" + s_razorFileExtensions[i])
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                IncludeSubdirectories = true,
            };

            documentWatcher.Created += (sender, args) => FileSystemWatcher_RazorDocumentEvent(args.FullPath, projectInstance, RazorFileChangeKind.Added);
            documentWatcher.Deleted += (sender, args) => FileSystemWatcher_RazorDocumentEvent(args.FullPath, projectInstance, RazorFileChangeKind.Removed);
            documentWatcher.Changed += (sender, args) => FileSystemWatcher_RazorDocumentEvent(args.FullPath, projectInstance, RazorFileChangeKind.Changed);
            documentWatcher.Renamed += (sender, args) =>
            {
                // Translate file renames into remove / add

                if (s_razorFileExtensions.Any(extension => args.OldFullPath.EndsWith(extension, StringComparison.Ordinal)))
                {
                    // Renaming from Razor file to something else.
                    FileSystemWatcher_RazorDocumentEvent(args.OldFullPath, projectInstance, RazorFileChangeKind.Removed);
                }

                if (s_razorFileExtensions.Any(extension => args.FullPath.EndsWith(extension, StringComparison.Ordinal)))
                {
                    // Renaming into a Razor file. This typically occurs when users go from .cshtml => .razor
                    FileSystemWatcher_RazorDocumentEvent(args.FullPath, projectInstance, RazorFileChangeKind.Added);
                }
            };
            watchers.Add(documentWatcher);

            var documentOutputWatcher = new FileSystemWatcher(projectDirectory, "*" + s_razorFileExtensions[i] + ".g.cs")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                IncludeSubdirectories = true,
            };

            documentOutputWatcher.Created += (sender, args) => FileSystemWatcher_RazorDocumentOutputEvent(args.FullPath, projectInstance, RazorFileChangeKind.Added);
            documentOutputWatcher.Deleted += (sender, args) => FileSystemWatcher_RazorDocumentOutputEvent(args.FullPath, projectInstance, RazorFileChangeKind.Removed);
            documentOutputWatcher.Changed += (sender, args) => FileSystemWatcher_RazorDocumentOutputEvent(args.FullPath, projectInstance, RazorFileChangeKind.Changed);
            documentOutputWatcher.Renamed += (sender, args) =>
            {
                // Translate file renames into remove / add

                if (s_razorFileExtensions.Any(extension => args.OldFullPath.EndsWith(extension + ".g.cs", StringComparison.Ordinal)))
                {
                    // Renaming from Razor background file to something else.
                    FileSystemWatcher_RazorDocumentOutputEvent(args.OldFullPath, projectInstance, RazorFileChangeKind.Removed);
                }

                if (s_razorFileExtensions.Any(extension => args.FullPath.EndsWith(extension + ".g.cs", StringComparison.Ordinal)))
                {
                    // Renaming into a Razor generated file.
                    FileSystemWatcher_RazorDocumentOutputEvent(args.FullPath, projectInstance, RazorFileChangeKind.Added);
                }
            };
            watchers.Add(documentOutputWatcher);

            documentWatcher.EnableRaisingEvents = true;
            documentOutputWatcher.EnableRaisingEvents = true;
        }

        _watcherMap[projectDirectory] = watchers;
    }

    // Internal for testing
    internal void FileSystemWatcher_RazorDocumentEvent(string filePath, ProjectInstance projectInstance, RazorFileChangeKind changeKind)
    {
        var args = new RazorFileChangeEventArgs(filePath, projectInstance, changeKind);
        for (var i = 0; i < _documentChangeListeners.Count; i++)
        {
            _documentChangeListeners[i].RazorDocumentChanged(args);
        }
    }

    // Internal for testing
    internal void FileSystemWatcher_RazorDocumentOutputEvent(string filePath, ProjectInstance projectInstance, RazorFileChangeKind changeKind)
    {
        var args = new RazorFileChangeEventArgs(filePath, projectInstance, changeKind);
        for (var i = 0; i < _documentOutputChangeListeners.Count; i++)
        {
            _documentOutputChangeListeners[i].RazorDocumentOutputChanged(args);
        }
    }
}
