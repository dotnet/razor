// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Editor.Razor.Documents;

namespace Microsoft.VisualStudio.Editor.Razor;

internal class ImportDocumentManager(ProjectSnapshotManagerDispatcher dispatcher, FileChangeTrackerFactory fileChangeTrackerFactory) : IImportDocumentManager
{
    private readonly FileChangeTrackerFactory _fileChangeTrackerFactory = fileChangeTrackerFactory;
    private readonly ProjectSnapshotManagerDispatcher _dispatcher = dispatcher;
    private readonly Dictionary<string, ImportTracker> _importTrackerCache = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<ImportChangedEventArgs>? Changed;

    public async ValueTask OnSubscribedAsync(VisualStudioDocumentTracker tracker, CancellationToken cancellationToken)
    {
        if (tracker is null)
        {
            throw new ArgumentNullException(nameof(tracker));
        }

        await _dispatcher.SwitchToAsync(cancellationToken);

        foreach (var import in GetImportItems(tracker))
        {
            var importFilePath = import.PhysicalPath;

            if (importFilePath is null)
            {
                Debug.Fail("import.FilePath is null!");
                continue;
            }

            if (!_importTrackerCache.TryGetValue(importFilePath, out var importTracker))
            {
                // First time seeing this import. Start tracking it.
                var fileChangeTracker = _fileChangeTrackerFactory.Create(importFilePath);
                importTracker = new ImportTracker(fileChangeTracker);
                _importTrackerCache[importFilePath] = importTracker;

                fileChangeTracker.Changed += FileChangeTracker_Changed;
                await fileChangeTracker.StartListeningAsync(cancellationToken);
            }

            importTracker.AssociatedDocuments.Add(tracker.FilePath);
        }
    }

    public async ValueTask OnUnsubscribedAsync(VisualStudioDocumentTracker tracker, CancellationToken cancellationToken)
    {
        if (tracker is null)
        {
            throw new ArgumentNullException(nameof(tracker));
        }

        await _dispatcher.SwitchToAsync(cancellationToken);

        foreach (var import in GetImportItems(tracker))
        {
            var importFilePath = import.PhysicalPath;
            Assumes.NotNull(importFilePath);

            if (_importTrackerCache.TryGetValue(importFilePath, out var importTracker))
            {
                importTracker.AssociatedDocuments.Remove(tracker.FilePath);

                if (importTracker.AssociatedDocuments.Count == 0)
                {
                    // There are no open documents that care about this import. We no longer need to track it.
                    await importTracker.FileChangeTracker.StopListeningAsync(cancellationToken);
                    _importTrackerCache.Remove(importFilePath);
                }
            }
        }
    }

    private static ImmutableArray<RazorProjectItem> GetImportItems(VisualStudioDocumentTracker tracker)
    {
        using var result = new PooledArrayBuilder<RazorProjectItem>();

        var projectSnapshot = tracker.ProjectSnapshot.AssumeNotNull();
        var projectEngine = projectSnapshot.GetProjectEngine();
        var documentSnapshot = projectSnapshot.GetDocument(tracker.FilePath);
        var fileKind = documentSnapshot?.FileKind;
        var trackerItem = projectEngine.FileSystem.GetItem(tracker.FilePath, fileKind);

        foreach (var feature in projectEngine.ProjectFeatures)
        {
            if (feature is not IImportProjectFeature importFeature)
            {
                continue;
            }

            foreach (var importItem in importFeature.GetImports(trackerItem))
            {
                if (importItem.FilePath is not null)
                {
                    result.Add(importItem);
                }
            }
        }

        return result.DrainToImmutable();
    }

    private void OnChanged(ImportTracker importTracker, FileChangeKind changeKind)
    {
        _dispatcher.AssertDispatcherThread();

        if (Changed is null)
        {
            return;
        }

        var args = new ImportChangedEventArgs(importTracker.FilePath, changeKind, importTracker.AssociatedDocuments);
        Changed.Invoke(this, args);
}

    private void FileChangeTracker_Changed(object sender, FileChangeEventArgs args)
    {
        _dispatcher.AssertDispatcherThread();

        if (_importTrackerCache.TryGetValue(args.FilePath, out var importTracker))
        {
            OnChanged(importTracker, args.Kind);
        }
    }

    private sealed class ImportTracker(FileChangeTracker fileChangeTracker)
    {
        public FileChangeTracker FileChangeTracker { get; } = fileChangeTracker;
        public HashSet<string> AssociatedDocuments { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public string FilePath => FileChangeTracker.FilePath;
    }
}
