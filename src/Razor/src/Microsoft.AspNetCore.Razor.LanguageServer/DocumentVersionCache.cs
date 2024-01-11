// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

// TODO: This has to be Shared for MEF to work, but this service was written assuming its lifetime was that of the language
//       server, so it doesn't clean up after itself well. In the long run, this hopefully won't matter, as we can remove it
//       but leaving this note here because you never know.
[Export(typeof(IDocumentVersionCache)), Shared]
[method: ImportingConstructor]
internal sealed partial class DocumentVersionCache() : IDocumentVersionCache, IProjectSnapshotChangeTrigger
{
    internal const int MaxDocumentTrackingCount = 20;

    private readonly Dictionary<string, List<DocumentEntry>> _documentLookup_NeedsLock = new(FilePathComparer.Instance);
    private readonly ReadWriterLocker _lock = new();
    private ProjectSnapshotManagerBase? _projectSnapshotManager;

    private ProjectSnapshotManagerBase ProjectSnapshotManager
        => _projectSnapshotManager ?? throw new InvalidOperationException("ProjectSnapshotManager accessed before Initialized was called.");

    public void Initialize(ProjectSnapshotManagerBase projectManager)
    {
        _projectSnapshotManager = projectManager;
        ProjectSnapshotManager.Changed += ProjectSnapshotManager_Changed;
    }

    public void TrackDocumentVersion(IDocumentSnapshot documentSnapshot, int version)
    {
        if (documentSnapshot is null)
        {
            throw new ArgumentNullException(nameof(documentSnapshot));
        }

        using var upgradeableReadLock = _lock.EnterUpgradeAbleReadLock();
        TrackDocumentVersion(documentSnapshot, version, upgradeableReadLock);
    }

    private void TrackDocumentVersion(IDocumentSnapshot documentSnapshot, int version, ReadWriterLocker.UpgradeableReadLock upgradeableReadLock)
    {
        // Need to ensure the write lock covers all uses of documentEntries, not just DocumentLookup
        using (upgradeableReadLock.EnterWriteLock())
        {
            var key = documentSnapshot.FilePath.AssumeNotNull();
            if (!_documentLookup_NeedsLock.TryGetValue(key, out var documentEntries))
            {
                documentEntries = new List<DocumentEntry>();
                _documentLookup_NeedsLock.Add(key, documentEntries);
            }

            if (documentEntries.Count == MaxDocumentTrackingCount)
            {
                // Clear the oldest document entry

                // With this approach we'll slowly leak memory as new documents are added to the system. We don't clear up
                // document file paths where where all of the corresponding entries are expired.
                documentEntries.RemoveAt(0);
            }

            var entry = new DocumentEntry(documentSnapshot, version);
            documentEntries.Add(entry);
        }
    }

    public int GetLatestDocumentVersion(string filePath)
    {
        using var _ = _lock.EnterReadLock();

        if (!_documentLookup_NeedsLock.TryGetValue(filePath, out var documentEntries))
        {
            return -1;
        }

        return documentEntries[^1].Version;
    }

    public bool TryGetDocumentVersion(IDocumentSnapshot documentSnapshot, [NotNullWhen(true)] out int? version)
    {
        if (documentSnapshot is null)
        {
            throw new ArgumentNullException(nameof(documentSnapshot));
        }

        using var _ = _lock.EnterReadLock();

        var filePath = documentSnapshot.FilePath.AssumeNotNull();
        if (!_documentLookup_NeedsLock.TryGetValue(filePath, out var documentEntries))
        {
            version = null;
            return false;
        }

        // We iterate backwards over the entries to prioritize newer entries.
        for (var i = documentEntries.Count - 1; i >= 0; i--)
        {
            if (documentEntries[i].Document.TryGetTarget(out var document) &&
                document == documentSnapshot)
            {
                version = documentEntries[i].Version;
                return true;
            }
        }

        version = null;
        return false;
    }

    private void ProjectSnapshotManager_Changed(object? sender, ProjectChangeEventArgs args)
    {
        // Don't do any work if the solution is closing
        if (args.SolutionIsClosing)
        {
            return;
        }

        using var upgradeableLock = _lock.EnterUpgradeAbleReadLock();

        switch (args.Kind)
        {
            case ProjectChangeKind.DocumentChanged:
                var documentFilePath = args.DocumentFilePath!;
                if (_documentLookup_NeedsLock.ContainsKey(documentFilePath) &&
                    !ProjectSnapshotManager.IsDocumentOpen(documentFilePath))
                {
                    using (upgradeableLock.EnterWriteLock())
                    {
                        // Document closed, evict entry.
                        _documentLookup_NeedsLock.Remove(documentFilePath);
                    }
                }

                break;
        }

        // Any event that has a project may have changed the state of the documents
        // and therefore requires us to mark all existing documents as latest.
        var project = ProjectSnapshotManager.GetLoadedProject(args.ProjectKey);
        if (project is null)
        {
            // Project no longer loaded, wait for document removed event.
            return;
        }

        CaptureProjectDocumentsAsLatest(project, upgradeableLock);
    }

    private void CaptureProjectDocumentsAsLatest(IProjectSnapshot projectSnapshot, ReadWriterLocker.UpgradeableReadLock upgradeableReadLock)
    {
        foreach (var documentPath in projectSnapshot.DocumentFilePaths)
        {
            if (_documentLookup_NeedsLock.ContainsKey(documentPath) &&
                projectSnapshot.GetDocument(documentPath) is { } document)
            {
                MarkAsLatestVersion(document, upgradeableReadLock);
            }
        }
    }

    private void MarkAsLatestVersion(IDocumentSnapshot document, ReadWriterLocker.UpgradeableReadLock upgradeableReadLock)
    {
        if (!_documentLookup_NeedsLock.TryGetValue(document.FilePath.AssumeNotNull(), out var documentEntries))
        {
            return;
        }

        var latestEntry = documentEntries[^1];

        // Update our internal tracking state to track the changed document as the latest document.
        TrackDocumentVersion(document, latestEntry.Version, upgradeableReadLock);
    }
}
