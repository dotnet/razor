// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

// The implementation of project snapshot manager abstracts the host's underlying project system (HostProject),
// to provide a immutable view of the underlying project systems.
//
// The HostProject support all of the configuration that the Razor SDK exposes via the project system
// (language version, extensions, named configuration).
//
// The implementation will create a ProjectSnapshot for each HostProject.
internal class DefaultProjectSnapshotManager : ProjectSnapshotManagerBase
{
    public override event EventHandler<ProjectChangeEventArgs> Changed;

    private readonly ProjectSnapshotChangeTrigger[] _triggers;

    // Each entry holds a ProjectState and an optional ProjectSnapshot. ProjectSnapshots are
    // created lazily.
    private readonly LockFactory _locksFactory = new();
    private readonly Dictionary<string, Entry> _projects_needsLock;
    private readonly HashSet<string> _openDocuments_needsLock;
    private readonly LoadTextOptions LoadTextOptions = new LoadTextOptions(SourceHashAlgorithm.Sha256);

    // We have a queue for changes because if one change results in another change aka, add -> open we want to make sure the "add" finishes running first before "open" is notified.
    private readonly Queue<ProjectChangeEventArgs> _notificationWork;

    public DefaultProjectSnapshotManager(
        IErrorReporter errorReporter,
        IEnumerable<ProjectSnapshotChangeTrigger> triggers,
        Workspace workspace)
    {
        _triggers = triggers?.OrderByDescending(trigger => trigger.InitializePriority).ToArray() ?? throw new ArgumentNullException(nameof(triggers));
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        ErrorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));

        _projects_needsLock = new Dictionary<string, Entry>(FilePathComparer.Instance);
        _openDocuments_needsLock = new HashSet<string>(FilePathComparer.Instance);
        _notificationWork = new Queue<ProjectChangeEventArgs>();

        using (var _ = _locksFactory.EnterReadLock())
        {
            for (var i = 0; i < _triggers.Length; i++)
            {
                _triggers[i].Initialize(this);
            }
        }
    }

    // internal for testing
    internal bool IsSolutionClosing { get; private set; }

    public override ImmutableArray<IProjectSnapshot> Projects
    {
        get
        {
            using var _ = _locksFactory.EnterReadLock();
            return _projects_needsLock.Select(e => e.Value.GetSnapshot()).ToImmutableArray();
        }
    }

    internal override IReadOnlyCollection<string> OpenDocuments
    {
        get
        {
            using var _ = _locksFactory.EnterReadLock();
            return _openDocuments_needsLock;
        }
    }

    internal override Workspace Workspace { get; }

    internal override IErrorReporter ErrorReporter { get; }

    public override IProjectSnapshot GetLoadedProject(string filePath)
    {
        if (filePath is null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        using var _ = _locksFactory.EnterReadLock();
        if (_projects_needsLock.TryGetValue(filePath, out var entry))
        {
            return entry.GetSnapshot();
        }

        return null;
    }

    public override IProjectSnapshot GetOrCreateProject(string filePath)
    {
        if (filePath is null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        using var _ = _locksFactory.EnterReadLock();
        return GetLoadedProject(filePath) ?? new EphemeralProjectSnapshot(Workspace.Services, filePath);
    }

    public override bool IsDocumentOpen(string documentFilePath)
    {
        if (documentFilePath is null)
        {
            throw new ArgumentNullException(nameof(documentFilePath));
        }

        using var _ = _locksFactory.EnterReadLock();
        return _openDocuments_needsLock.Contains(documentFilePath);
    }

    internal override void DocumentAdded(HostProject hostProject, HostDocument document, TextLoader textLoader)
    {
        if (hostProject is null)
        {
            throw new ArgumentNullException(nameof(hostProject));
        }

        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        using var upgradeableLock = _locksFactory.EnterUpgradeAbleReadLock();

        if (_projects_needsLock.TryGetValue(hostProject.FilePath, out var entry))
        {
            // if the solution is closing we don't need to bother computing new state
            if (IsSolutionClosing)
            {
                var oldSnapshot = entry.GetSnapshot();
                NotifyListeners(oldSnapshot, oldSnapshot, document.FilePath, ProjectChangeKind.DocumentAdded);
            }
            else
            {
                var loader = CreateTextAndVersionFunc(textLoader);
                var state = entry.State.WithAddedHostDocument(document, loader);

                // Document updates can no-op.
                if (!object.ReferenceEquals(state, entry.State))
                {
                    var oldSnapshot = entry.GetSnapshot();
                    entry = new Entry(state);

                    using (var _ = upgradeableLock.GetWriteLock())
                    {
                        _projects_needsLock[hostProject.FilePath] = entry;
                    }

                    NotifyListeners(oldSnapshot, entry.GetSnapshot(), document.FilePath, ProjectChangeKind.DocumentAdded);
                }
            }
        }
    }

    internal override void DocumentRemoved(HostProject hostProject, HostDocument document)
    {
        if (hostProject is null)
        {
            throw new ArgumentNullException(nameof(hostProject));
        }

        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        using var upgradeableLock = _locksFactory.EnterUpgradeAbleReadLock();
        DocumentRemoved(hostProject, document, upgradeableLock);
    }

    private void DocumentRemoved(HostProject hostProject, HostDocument document, LockFactory.UpgradeAbleReadLock upgradeableLock)
    {
        if (_projects_needsLock.TryGetValue(hostProject.FilePath, out var entry))
        {
            // if the solution is closing we don't need to bother computing new state
            if (IsSolutionClosing)
            {
                var snapshot = entry.GetSnapshot();
                NotifyListeners(snapshot, snapshot, document.FilePath, ProjectChangeKind.DocumentRemoved);
            }
            else
            {
                var state = entry.State.WithRemovedHostDocument(document);

                // Document updates can no-op.
                if (!object.ReferenceEquals(state, entry.State))
                {
                    var oldSnapshot = entry.GetSnapshot();
                    entry = new Entry(state);

                    using (var _ = upgradeableLock.GetWriteLock())
                    {
                        _projects_needsLock[hostProject.FilePath] = entry;
                    }

                    NotifyListeners(oldSnapshot, entry.GetSnapshot(), document.FilePath, ProjectChangeKind.DocumentRemoved);
                }
            }
        }
    }

    internal override void DocumentOpened(string projectFilePath, string documentFilePath, SourceText sourceText)
    {
        if (projectFilePath is null)
        {
            throw new ArgumentNullException(nameof(projectFilePath));
        }

        if (documentFilePath is null)
        {
            throw new ArgumentNullException(nameof(documentFilePath));
        }

        if (sourceText is null)
        {
            throw new ArgumentNullException(nameof(sourceText));
        }

        using var upgradeableLock = _locksFactory.EnterUpgradeAbleReadLock();

        if (_projects_needsLock.TryGetValue(projectFilePath, out var entry) &&
                    entry.State.Documents.TryGetValue(documentFilePath, out var older))
        {
            // if the solution is closing we don't need to bother computing new state
            if (IsSolutionClosing)
            {
                var oldSnapshot = entry.GetSnapshot();
                NotifyListeners(oldSnapshot, oldSnapshot, documentFilePath, ProjectChangeKind.DocumentChanged);
            }
            else
            {
                ProjectState state;

                var currentText = sourceText;
                if (older.TryGetText(out var olderText) &&
                    older.TryGetTextVersion(out var olderVersion))
                {
                    var version = currentText.ContentEquals(olderText) ? olderVersion : olderVersion.GetNewerVersion();
                    state = entry.State.WithChangedHostDocument(older.HostDocument, currentText, version);
                }
                else
                {
                    state = entry.State.WithChangedHostDocument(older.HostDocument, async () =>
                    {
                        olderText = await older.GetTextAsync().ConfigureAwait(false);
                        olderVersion = await older.GetTextVersionAsync().ConfigureAwait(false);

                        var version = currentText.ContentEquals(olderText) ? olderVersion : olderVersion.GetNewerVersion();
                        return TextAndVersion.Create(currentText, version, documentFilePath);
                    });
                }

                ProjectChangeEventArgs eventArgs = null;

                using (var _ = upgradeableLock.GetWriteLock())
                {
                    _openDocuments_needsLock.Add(documentFilePath);

                    // Document updates can no-op.
                    if (!object.ReferenceEquals(state, entry.State))
                    {
                        var oldSnapshot = entry.GetSnapshot();
                        entry = new Entry(state);
                        _projects_needsLock[projectFilePath] = entry;
                        eventArgs = new(oldSnapshot, entry.GetSnapshot(), documentFilePath, ProjectChangeKind.DocumentChanged, IsSolutionClosing);
                    }
                }

                // Don't notify with a write lock to avoid reentrancy problems
                if (eventArgs is not null)
                {
                    NotifyListeners(eventArgs);
                }
            }
        }
    }

    internal override void DocumentClosed(string projectFilePath, string documentFilePath, TextLoader textLoader)
    {
        if (projectFilePath is null)
        {
            throw new ArgumentNullException(nameof(projectFilePath));
        }

        if (documentFilePath is null)
        {
            throw new ArgumentNullException(nameof(documentFilePath));
        }

        if (textLoader is null)
        {
            throw new ArgumentNullException(nameof(textLoader));
        }

        using var upgradeableLock = _locksFactory.EnterUpgradeAbleReadLock();

        if (_projects_needsLock.TryGetValue(projectFilePath, out var entry) &&
                entry.State.Documents.TryGetValue(documentFilePath, out var older))
        {
            // if the solution is closing we don't need to bother computing new state
            if (IsSolutionClosing)
            {
                var oldSnapshot = entry.GetSnapshot();
                NotifyListeners(oldSnapshot, oldSnapshot, documentFilePath, ProjectChangeKind.DocumentChanged);
            }
            else
            {
                var state = entry.State.WithChangedHostDocument(
                    older.HostDocument,
                    async () => await textLoader.LoadTextAndVersionAsync(LoadTextOptions, cancellationToken: default).ConfigureAwait(false));

                ProjectChangeEventArgs eventArgs = null;
                using (var _ = upgradeableLock.GetWriteLock())
                {
                    _openDocuments_needsLock.Remove(documentFilePath);

                    // Document updates can no-op.
                    if (!object.ReferenceEquals(state, entry.State))
                    {
                        var oldSnapshot = entry.GetSnapshot();
                        entry = new Entry(state);
                        _projects_needsLock[projectFilePath] = entry;
                        eventArgs = new(oldSnapshot, entry.GetSnapshot(), documentFilePath, ProjectChangeKind.DocumentChanged, IsSolutionClosing);
                    }
                }

                if (eventArgs is not null)
                {
                    NotifyListeners(eventArgs);
                }
            }
        }
    }

    internal override void DocumentChanged(string projectFilePath, string documentFilePath, SourceText sourceText)
    {
        if (projectFilePath is null)
        {
            throw new ArgumentNullException(nameof(projectFilePath));
        }

        if (documentFilePath is null)
        {
            throw new ArgumentNullException(nameof(documentFilePath));
        }

        if (sourceText is null)
        {
            throw new ArgumentNullException(nameof(sourceText));
        }

        using var upgradeableLock = _locksFactory.EnterUpgradeAbleReadLock();

        if (_projects_needsLock.TryGetValue(projectFilePath, out var entry) &&
                    entry.State.Documents.TryGetValue(documentFilePath, out var older))
        {
            ProjectState state;

            var currentText = sourceText;

            // if the solution is closing we don't need to bother computing new state
            if (IsSolutionClosing)
            {
                var oldSnapshot = entry.GetSnapshot();
                NotifyListeners(oldSnapshot, oldSnapshot, documentFilePath, ProjectChangeKind.DocumentChanged);
            }
            else
            {
                if (older.TryGetText(out var olderText) &&
                older.TryGetTextVersion(out var olderVersion))
                {
                    var version = currentText.ContentEquals(olderText) ? olderVersion : olderVersion.GetNewerVersion();
                    state = entry.State.WithChangedHostDocument(older.HostDocument, currentText, version);
                }
                else
                {
                    state = entry.State.WithChangedHostDocument(older.HostDocument, async () =>
                    {
                        olderText = await older.GetTextAsync().ConfigureAwait(false);
                        olderVersion = await older.GetTextVersionAsync().ConfigureAwait(false);

                        var version = currentText.ContentEquals(olderText) ? olderVersion : olderVersion.GetNewerVersion();
                        return TextAndVersion.Create(currentText, version, documentFilePath);
                    });
                }

                // Document updates can no-op.
                if (!object.ReferenceEquals(state, entry.State))
                {
                    var oldSnapshot = entry.GetSnapshot();
                    entry = new Entry(state);

                    using (var _ = upgradeableLock.GetWriteLock())
                    {
                        _projects_needsLock[projectFilePath] = entry;
                    }

                    NotifyListeners(oldSnapshot, entry.GetSnapshot(), documentFilePath, ProjectChangeKind.DocumentChanged);
                }
            }
        }
    }

    internal override void DocumentChanged(string projectFilePath, string documentFilePath, TextLoader textLoader)
    {
        if (projectFilePath is null)
        {
            throw new ArgumentNullException(nameof(projectFilePath));
        }

        if (documentFilePath is null)
        {
            throw new ArgumentNullException(nameof(documentFilePath));
        }

        if (textLoader is null)
        {
            throw new ArgumentNullException(nameof(textLoader));
        }

        using var upgradeableLock = _locksFactory.EnterUpgradeAbleReadLock();

        if (_projects_needsLock.TryGetValue(projectFilePath, out var entry) &&
                    entry.State.Documents.TryGetValue(documentFilePath, out var older))
        {
            // if the solution is closing we don't need to bother computing new state
            if (IsSolutionClosing)
            {
                var oldSnapshot = entry.GetSnapshot();
                NotifyListeners(oldSnapshot, oldSnapshot, documentFilePath, ProjectChangeKind.DocumentChanged);
            }
            else
            {
                var state = entry.State.WithChangedHostDocument(
                    older.HostDocument,
                    async () => await textLoader.LoadTextAndVersionAsync(LoadTextOptions, cancellationToken: default).ConfigureAwait(false));

                // Document updates can no-op.
                if (!ReferenceEquals(state, entry.State))
                {
                    var oldSnapshot = entry.GetSnapshot();
                    entry = new Entry(state);

                    using (var _ = upgradeableLock.GetWriteLock())
                    {
                        _projects_needsLock[projectFilePath] = entry;
                    }

                    NotifyListeners(oldSnapshot, entry.GetSnapshot(), documentFilePath, ProjectChangeKind.DocumentChanged);
                }
            }
        }
    }

    internal override void ProjectAdded(HostProject hostProject)
    {
        if (hostProject is null)
        {
            throw new ArgumentNullException(nameof(hostProject));
        }

        using var upgradeableLock = _locksFactory.EnterUpgradeAbleReadLock();
        ProjectAdded(hostProject, upgradeableLock);
    }

    internal override IProjectSnapshot GetOrAddLoadedProject(string normalizedPath, RazorConfiguration configuration, string rootNamespace)
    {
        using var upgradeableReadLock = _locksFactory.EnterUpgradeAbleReadLock();
        var project = GetLoadedProject(normalizedPath);

        if (project is not null)
        {
            return project;
        }

        var newProject = new HostProject(normalizedPath, configuration, rootNamespace);
        ProjectAdded(newProject, upgradeableReadLock);

        return GetLoadedProject(normalizedPath);
    }

    private void ProjectAdded(HostProject hostProject, LockFactory.UpgradeAbleReadLock upgradeableLock)
    {
        // We don't expect to see a HostProject initialized multiple times for the same path. Just ignore it.
        if (_projects_needsLock.ContainsKey(hostProject.FilePath))
        {
            return;
        }

        var state = ProjectState.Create(Workspace.Services, hostProject);
        var entry = new Entry(state);

        using (var _ = upgradeableLock.GetWriteLock())
        {
            _projects_needsLock[hostProject.FilePath] = entry;
        }

        // We need to notify listeners about every project add.
        NotifyListeners(older: null, entry.GetSnapshot(), documentFilePath: null, ProjectChangeKind.ProjectAdded);
    }

    internal override void ProjectConfigurationChanged(HostProject hostProject)
    {
        if (hostProject is null)
        {
            throw new ArgumentNullException(nameof(hostProject));
        }

        using var upgradeableLock = _locksFactory.EnterUpgradeAbleReadLock();

        if (_projects_needsLock.TryGetValue(hostProject.FilePath, out var entry))
        {
            // if the solution is closing we don't need to bother computing new state
            if (IsSolutionClosing)
            {
                var oldSnapshot = entry.GetSnapshot();
                NotifyListeners(oldSnapshot, oldSnapshot, documentFilePath: null, ProjectChangeKind.ProjectChanged);
            }
            else
            {
                var state = entry.State.WithHostProject(hostProject);

                // HostProject updates can no-op.
                if (!object.ReferenceEquals(state, entry.State))
                {
                    var oldSnapshot = entry.GetSnapshot();
                    entry = new Entry(state);

                    using (var _ = upgradeableLock.GetWriteLock())
                    {
                        _projects_needsLock[hostProject.FilePath] = entry;
                    }

                    NotifyListeners(oldSnapshot, entry.GetSnapshot(), documentFilePath: null, ProjectChangeKind.ProjectChanged);
                }
            }
        }
    }

    internal override void ProjectWorkspaceStateChanged(string projectFilePath, ProjectWorkspaceState projectWorkspaceState)
    {
        if (projectFilePath is null)
        {
            throw new ArgumentNullException(nameof(projectFilePath));
        }

        if (projectWorkspaceState is null)
        {
            throw new ArgumentNullException(nameof(projectWorkspaceState));
        }

        using var upgradeableLock = _locksFactory.EnterUpgradeAbleReadLock();

        if (_projects_needsLock.TryGetValue(projectFilePath, out var entry))
        {
            // if the solution is closing we don't need to bother computing new state
            if (IsSolutionClosing)
            {
                var oldSnapshot = entry.GetSnapshot();
                NotifyListeners(oldSnapshot, oldSnapshot, documentFilePath: null, ProjectChangeKind.ProjectChanged);
            }
            else
            {
                var state = entry.State.WithProjectWorkspaceState(projectWorkspaceState);

                // HostProject updates can no-op.
                if (!object.ReferenceEquals(state, entry.State))
                {
                    var oldSnapshot = entry.GetSnapshot();
                    entry = new Entry(state);

                    using (var _ = upgradeableLock.GetWriteLock())
                    {
                        _projects_needsLock[projectFilePath] = entry;
                    }

                    NotifyListeners(oldSnapshot, entry.GetSnapshot(), documentFilePath: null, ProjectChangeKind.ProjectChanged);
                }
            }
        }
    }

    internal override void ProjectRemoved(HostProject hostProject)
    {
        if (hostProject is null)
        {
            throw new ArgumentNullException(nameof(hostProject));
        }

        using var upgradeableLock = _locksFactory.EnterUpgradeAbleReadLock();
        ProjectRemoved(hostProject, upgradeableLock);
    }

    private void ProjectRemoved(HostProject hostProject, LockFactory.UpgradeAbleReadLock upgradeableLock)
    {
        if (_projects_needsLock.TryGetValue(hostProject.FilePath, out var entry))
        {
            // We need to notify listeners about every project removal.
            var oldSnapshot = entry.GetSnapshot();

            using (var _ = upgradeableLock.GetWriteLock())
            {
                _projects_needsLock.Remove(hostProject.FilePath);
            }

            NotifyListeners(oldSnapshot, newer: null, documentFilePath: null, ProjectChangeKind.ProjectRemoved);
        }
    }

    internal override bool TryRemoveLoadedProject(string normalizedPath, out IProjectSnapshot project)
    {
        using var upgradeableLock = _locksFactory.EnterUpgradeAbleReadLock();

        project = GetLoadedProject(normalizedPath);

        if (project is HostProject hostProject)
        {
            ProjectRemoved(hostProject, upgradeableLock);
            return true;
        }

        return false;
    }

    internal override void SolutionOpened()
    {
        IsSolutionClosing = false;
    }

    internal override void SolutionClosed()
    {
        IsSolutionClosing = true;
    }

    internal override void ReportError(Exception exception)
    {
        if (exception is null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        ErrorReporter.ReportError(exception);
    }

    internal override void ReportError(Exception exception, IProjectSnapshot project)
    {
        if (exception is null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        ErrorReporter.ReportError(exception, project);
    }

    internal override void ReportError(Exception exception, HostProject hostProject)
    {
        if (exception is null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        var snapshot = hostProject?.FilePath is null ? null : GetLoadedProject(hostProject.FilePath);
        ErrorReporter.ReportError(exception, snapshot);
    }

    private void NotifyListeners(IProjectSnapshot older, IProjectSnapshot newer, string documentFilePath, ProjectChangeKind kind)
    {
        NotifyListeners(new ProjectChangeEventArgs(older, newer, documentFilePath, kind, IsSolutionClosing));
    }

    // virtual so it can be overridden in tests
    protected virtual void NotifyListeners(ProjectChangeEventArgs e)
    {
        _locksFactory.EnsureNoWriteLock();

        // Get a read lock to make sure nothing changes while notifications
        // are going out.
        using var _ = _locksFactory.EnterReadLock();

        _notificationWork.Enqueue(e);

        if (_notificationWork.Count == 1)
        {
            // Only one notification, go ahead and start notifying. In the situation where Count > 1 it means an event was triggered as a response to another event.
            // To ensure order we wont immediately re-invoke Changed here, we'll wait for the stack to unwind to notify others. This process still happens synchronously
            // it just ensures that events happen in the correct order. For instance lets take the situation where a document is added to a project. That document will be
            // added and then opened. However, if the result of "adding" causes an "open" to triger we want to ensure that "add" finishes prior to "open" being notified.

            // Start unwinding the notification queue
            do
            {
                // Don't dequeue yet, we want the notification to sit in the queue until we've finished notifying to ensure other calls to NotifyListeners know there's a currently running event loop.
                var args = _notificationWork.Peek();
                Changed?.Invoke(this, args);

                _notificationWork.Dequeue();
            }
            while (_notificationWork.Count > 0);
        }
    }

    internal override void UpdateProject(
        string filePath,
        RazorConfiguration configuration,
        ProjectWorkspaceState projectWorkspaceState,
        string rootNamespace,
        Func<IProjectSnapshot, ImmutableArray<IUpdateProjectAction>> calculate)
    {
        // Get an upgradeableLock, which will keep a read lock while we compute the changes
        // and then get a write lock to actually apply them. Only one upgradeable lock
        // can be held at any given time. Write lock must be retrieved on the same
        // thread that the lock was acquired
        using var upgradeableLock = _locksFactory.EnterUpgradeAbleReadLock();

        var project = GetLoadedProject(filePath);
        if (project is not ProjectSnapshot projectSnapshot)
        {
            return;
        }

        var originalHostProject = projectSnapshot.HostProject;
        var changes = calculate(project);

        var originalEntry = _projects_needsLock[filePath];
        Dictionary<string, Entry> updatedProjectsMap = new(changes.Length, FilePathComparer.Instance);
        using var _ = ListPool<ProjectChangeEventArgs>.GetPooledObject(out var changesToNotify);

        // Resolve all the changes and add notifications as needed
        foreach (var change in changes)
        {
            switch (change)
            {
                case AddDocumentAction addAction:
                    {
                        var entry = GetCurrentEntry(project);
                        TryAddNotificationAndUpdate(entry, entry.State.WithAddedHostDocument(addAction.NewDocument, CreateTextAndVersionFunc(addAction.TextLoader)), ProjectChangeKind.DocumentAdded);
                    }

                    break;

                case RemoveDocumentAction removeAction:
                    {
                        var entry = GetCurrentEntry(project);
                        TryAddNotificationAndUpdate(entry, entry.State.WithRemovedHostDocument(removeAction.OriginalDocument), ProjectChangeKind.DocumentRemoved);
                    }

                    break;

                case UpdateDocumentAction updateAction:
                    {
                        var entry = GetCurrentEntry(project);
                        TryAddNotificationAndUpdate(entry, entry.State.WithRemovedHostDocument(updateAction.OriginalDocument), ProjectChangeKind.DocumentRemoved);

                        entry = GetCurrentEntry(project);
                        TryAddNotificationAndUpdate(entry, entry.State.WithAddedHostDocument(updateAction.NewDocument, CreateTextAndVersionFunc(updateAction.TextLoader)), ProjectChangeKind.DocumentAdded);
                    }

                    break;

                case MoveDocumentAction moveAction:
                    var (from, to) = (moveAction.OriginalProject, moveAction.DestinationProject);
                    Debug.Assert(from == project || to == project);
                    Debug.Assert(from != to);

                    var fromEntry = GetCurrentEntry(from);
                    var toEntry = GetCurrentEntry(to);

                    TryAddNotificationAndUpdate(fromEntry, fromEntry.State.WithRemovedHostDocument(moveAction.Document), ProjectChangeKind.DocumentRemoved);
                    TryAddNotificationAndUpdate(toEntry, toEntry.State.WithAddedHostDocument(moveAction.Document, CreateTextAndVersionFunc(moveAction.TextLoader)), ProjectChangeKind.DocumentAdded);
                    break;

                default:
                    throw new InvalidOperationException($"Unexpected action type {change.GetType()}");
            }
        }

        var newHostProject = new HostProject(filePath, configuration, rootNamespace);
        var entryBeforeHostProject = GetCurrentEntry(project);
        var stateWithHostProject = entryBeforeHostProject.State.WithHostProject(newHostProject);
        TryAddNotificationAndUpdate(entryBeforeHostProject, stateWithHostProject, ProjectChangeKind.ProjectChanged);

        var entryBeforeWorkspaceState = GetCurrentEntry(project);
        var stateWithProjectWorkspaceState = entryBeforeWorkspaceState.State.WithProjectWorkspaceState(projectWorkspaceState);
        TryAddNotificationAndUpdate(entryBeforeWorkspaceState, stateWithProjectWorkspaceState, ProjectChangeKind.ProjectChanged);

        // Update current state first so we can get rid of the write lock and downgrade
        // back to a read lock when notifying changes
        using (upgradeableLock.GetWriteLock())
        {
            foreach (var (path, entry) in updatedProjectsMap)
            {
                _projects_needsLock[path] = entry;
            }
        }

        foreach (var notification in changesToNotify)
        {
            NotifyListeners(notification);
        }

        void TryAddNotificationAndUpdate(Entry currentEntry, ProjectState newState, ProjectChangeKind changeKind)
        {
            if (newState.Equals(currentEntry.State))
            {
                return;
            }

            var newEntry = new Entry(newState);
            updatedProjectsMap[currentEntry.State.HostProject.FilePath] = newEntry;
            changesToNotify.Add(new ProjectChangeEventArgs(currentEntry.GetSnapshot(), newEntry.GetSnapshot(), changeKind));
        }

        Entry GetCurrentEntry(IProjectSnapshot project)
        {
            if (!updatedProjectsMap.TryGetValue(project.FilePath, out var entry))
            {
                entry = _projects_needsLock[project.FilePath];
                updatedProjectsMap[project.FilePath] = entry;
            }

            return entry;
        }
    }

    private Func<Task<TextAndVersion>> CreateTextAndVersionFunc(TextLoader textLoader)
        => textLoader is null
            ? DocumentState.EmptyLoader
            : (() => textLoader.LoadTextAndVersionAsync(LoadTextOptions, CancellationToken.None));

    private class Entry
    {
        private IProjectSnapshot _snapshotUnsafe;
        public readonly ProjectState State;

        public Entry(ProjectState state)
        {
            State = state;
        }

        public IProjectSnapshot GetSnapshot()
        {
            return _snapshotUnsafe ??= new ProjectSnapshot(State);
        }
    }
}
