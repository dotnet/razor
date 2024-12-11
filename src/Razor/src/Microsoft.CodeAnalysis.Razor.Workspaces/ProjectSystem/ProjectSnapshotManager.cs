// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.ProjectEngineHost;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

// The implementation of project snapshot manager abstracts the host's underlying project system (HostProject),
// to provide a immutable view of the underlying project systems.
//
// The HostProject support all of the configuration that the Razor SDK exposes via the project system
// (language version, extensions, named configuration).
//
// The implementation will create a ProjectSnapshot for each HostProject.
internal partial class ProjectSnapshotManager : IProjectSnapshotManager, IDisposable
{
    private readonly IProjectEngineFactoryProvider _projectEngineFactoryProvider;
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger _logger;
    private readonly bool _initialized;

    public event EventHandler<ProjectChangeEventArgs>? PriorityChanged;
    public event EventHandler<ProjectChangeEventArgs>? Changed;

    private readonly ReaderWriterLockSlim _readerWriterLock = new(LockRecursionPolicy.NoRecursion);

    #region protected by lock

    /// <summary>
    /// A map of <see cref="ProjectKey"/> to <see cref="Entry"/>, which wraps a <see cref="ProjectState"/>
    /// and lazily creates a <see cref="ProjectSnapshot"/>.
    /// </summary>
    private readonly Dictionary<ProjectKey, Entry> _projectMap = [];

    /// <summary>
    /// The set of open documents.
    /// </summary>
    private readonly HashSet<string> _openDocumentSet = new(FilePathComparer.Instance);

    /// <summary>
    /// Determines whether or not the solution is closing.
    /// </summary>
    private bool _isSolutionClosing;

    #endregion

    #region protected by dispatcher

    /// <summary>
    ///  A queue of ordered notifications to process.
    /// </summary>
    /// <remarks>
    ///  ⚠️ This field must only be accessed when running on the dispatcher.
    /// </remarks>
    private readonly Queue<ProjectChangeEventArgs> _notificationQueue = new();

    /// <summary>
    ///  <see langword="true"/> while <see cref="_notificationQueue"/> is being processed.
    /// </summary>
    /// <remarks>
    ///  ⚠️ This field must only be accessed when running on the dispatcher.
    /// </remarks>
    private bool _processingNotifications;

    #endregion

    /// <summary>
    /// Constructs an instance of <see cref="ProjectSnapshotManager"/>.
    /// </summary>
    /// <param name="projectEngineFactoryProvider">The <see cref="IProjectEngineFactoryProvider"/> to
    /// use when creating <see cref="ProjectState"/>.</param>
    /// <param name="languageServerFeatureOptions">The options that were used to start the language server</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to use.</param>
    /// <param name="initializer">An optional callback to set up the initial set of projects and documents.
    /// Note that this is called during construction, so it does not run on the dispatcher and notifications
    /// will not be sent.</param>
    public ProjectSnapshotManager(
        IProjectEngineFactoryProvider projectEngineFactoryProvider,
        LanguageServerFeatureOptions languageServerFeatureOptions,
        ILoggerFactory loggerFactory,
        Action<Updater>? initializer = null)
    {
        _projectEngineFactoryProvider = projectEngineFactoryProvider;
        _languageServerFeatureOptions = languageServerFeatureOptions;
        _dispatcher = new(loggerFactory);
        _logger = loggerFactory.GetOrCreateLogger(GetType());

        initializer?.Invoke(new(this));

        _initialized = true;
    }

    public void Dispose()
    {
        _dispatcher.Dispose();
        _readerWriterLock.Dispose();
    }

    public bool IsSolutionClosing
    {
        get
        {
            using (_readerWriterLock.DisposableRead())
            {
                return _isSolutionClosing;
            }
        }
    }

    public ImmutableArray<IProjectSnapshot> GetProjects()
    {
        using (_readerWriterLock.DisposableRead())
        {
            using var builder = new PooledArrayBuilder<IProjectSnapshot>(_projectMap.Count);

            foreach (var (_, entry) in _projectMap)
            {
                builder.Add(entry.GetSnapshot());
            }

            return builder.DrainToImmutable();
        }
    }

    public ImmutableArray<string> GetOpenDocuments()
    {
        using (_readerWriterLock.DisposableRead())
        {
            return [.. _openDocumentSet];
        }
    }

    public IProjectSnapshot GetLoadedProject(ProjectKey projectKey)
    {
        using (_readerWriterLock.DisposableRead())
        {
            if (_projectMap.TryGetValue(projectKey, out var entry))
            {
                return entry.GetSnapshot();
            }
        }

        throw new InvalidOperationException($"No project snapshot exists with the key, '{projectKey}'");
    }

    public bool TryGetLoadedProject(ProjectKey projectKey, [NotNullWhen(true)] out IProjectSnapshot? project)
    {
        using (_readerWriterLock.DisposableRead())
        {
            if (_projectMap.TryGetValue(projectKey, out var entry))
            {
                project = entry.GetSnapshot();
                return true;
            }
        }

        project = null;
        return false;
    }

    public ImmutableArray<ProjectKey> GetAllProjectKeys(string projectFileName)
    {
        using (_readerWriterLock.DisposableRead())
        {
            using var projects = new PooledArrayBuilder<ProjectKey>(capacity: _projectMap.Count);

            foreach (var (key, entry) in _projectMap)
            {
                if (FilePathComparer.Instance.Equals(entry.State.HostProject.FilePath, projectFileName))
                {
                    projects.Add(key);
                }
            }

            return projects.DrainToImmutable();
        }
    }

    public bool IsDocumentOpen(string documentFilePath)
    {
        using (_readerWriterLock.DisposableRead())
        {
            return _openDocumentSet.Contains(documentFilePath);
        }
    }

    private void DocumentAdded(ProjectKey projectKey, HostDocument document, TextLoader textLoader)
    {
        if (_initialized)
        {
            _dispatcher.AssertRunningOnDispatcher();
        }

        if (TryUpdate(
            projectKey,
            document.FilePath,
            new AddDocumentAction(document, textLoader),
            out var oldSnapshot,
            out var newSnapshot))
        {
            NotifyListeners(ProjectChangeEventArgs.DocumentAdded(oldSnapshot, newSnapshot, document.FilePath, IsSolutionClosing));
        }
    }

    private void DocumentRemoved(ProjectKey projectKey, HostDocument document)
    {
        if (_initialized)
        {
            _dispatcher.AssertRunningOnDispatcher();
        }

        if (TryUpdate(
            projectKey,
            document.FilePath,
            new RemoveDocumentAction(document),
            out var oldSnapshot,
            out var newSnapshot))
        {
            NotifyListeners(ProjectChangeEventArgs.DocumentRemoved(oldSnapshot, newSnapshot, document.FilePath, IsSolutionClosing));
        }
    }

    private void DocumentOpened(ProjectKey projectKey, string documentFilePath, SourceText sourceText)
    {
        if (_initialized)
        {
            _dispatcher.AssertRunningOnDispatcher();
        }

        if (TryUpdate(
            projectKey,
            documentFilePath,
            new OpenDocumentAction(sourceText),
            out var oldSnapshot,
            out var newSnapshot))
        {
            NotifyListeners(ProjectChangeEventArgs.DocumentChanged(oldSnapshot, newSnapshot, documentFilePath, IsSolutionClosing));
        }
    }

    private void DocumentClosed(ProjectKey projectKey, string documentFilePath, TextLoader textLoader)
    {
        if (_initialized)
        {
            _dispatcher.AssertRunningOnDispatcher();
        }

        if (TryUpdate(
            projectKey,
            documentFilePath,
            new CloseDocumentAction(textLoader),
            out var oldSnapshot,
            out var newSnapshot))
        {
            NotifyListeners(ProjectChangeEventArgs.DocumentChanged(oldSnapshot, newSnapshot, documentFilePath, IsSolutionClosing));
        }
    }

    private void DocumentChanged(ProjectKey projectKey, string documentFilePath, SourceText sourceText)
    {
        if (_initialized)
        {
            _dispatcher.AssertRunningOnDispatcher();
        }

        if (TryUpdate(
            projectKey,
            documentFilePath,
            new DocumentTextChangedAction(sourceText),
            out var oldSnapshot,
            out var newSnapshot))
        {
            NotifyListeners(ProjectChangeEventArgs.DocumentChanged(oldSnapshot, newSnapshot, documentFilePath, IsSolutionClosing));
        }
    }

    private void DocumentChanged(ProjectKey projectKey, string documentFilePath, TextLoader textLoader)
    {
        if (_initialized)
        {
            _dispatcher.AssertRunningOnDispatcher();
        }

        if (TryUpdate(
            projectKey,
            documentFilePath,
            new DocumentTextLoaderChangedAction(textLoader),
            out var oldSnapshot,
            out var newSnapshot))
        {
            NotifyListeners(ProjectChangeEventArgs.DocumentChanged(oldSnapshot, newSnapshot, documentFilePath, IsSolutionClosing));
        }
    }

    private void AddProject(HostProject hostProject)
    {
        if (TryAddProject(hostProject, out var newSnapshot, out var isSolutionClosing))
        {
            NotifyListeners(ProjectChangeEventArgs.ProjectAdded(newSnapshot, isSolutionClosing));
        }
    }

    private void ProjectConfigurationChanged(HostProject hostProject)
    {
        if (_initialized)
        {
            _dispatcher.AssertRunningOnDispatcher();
        }

        if (TryUpdate(
            hostProject.Key,
            documentFilePath: null,
            new HostProjectUpdatedAction(hostProject),
            out var oldSnapshot,
            out var newSnapshot))
        {
            NotifyListeners(ProjectChangeEventArgs.ProjectChanged(oldSnapshot, newSnapshot, IsSolutionClosing));
        }
    }

    private void ProjectWorkspaceStateChanged(ProjectKey projectKey, ProjectWorkspaceState projectWorkspaceState)
    {
        if (_initialized)
        {
            _dispatcher.AssertRunningOnDispatcher();
        }

        if (TryUpdate(
            projectKey,
            documentFilePath: null,
            new ProjectWorkspaceStateChangedAction(projectWorkspaceState),
            out var oldSnapshot,
            out var newSnapshot))
        {
            NotifyListeners(ProjectChangeEventArgs.ProjectChanged(oldSnapshot, newSnapshot, IsSolutionClosing));
        }
    }

    private void RemoveProject(ProjectKey projectKey)
    {
        if (TryRemoveProject(projectKey, out var oldProject, out var isSolutionClosing))
        {
            NotifyListeners(ProjectChangeEventArgs.ProjectRemoved(oldProject, isSolutionClosing));
        }
    }

    private void SolutionOpened()
    {
        if (_initialized)
        {
            _dispatcher.AssertRunningOnDispatcher();
        }

        using (_readerWriterLock.DisposableWrite())
        {
            _isSolutionClosing = false;
        }
    }

    private void SolutionClosed()
    {
        if (_initialized)
        {
            _dispatcher.AssertRunningOnDispatcher();
        }

        using (_readerWriterLock.DisposableWrite())
        {
            _isSolutionClosing = true;
        }
    }

    private void NotifyListeners(ProjectChangeEventArgs notification)
    {
        if (!_initialized)
        {
            return;
        }

        // Notifications are *always* sent using the dispatcher.
        // This ensures that _notificationQueue and _processingNotifications are synchronized.
        _dispatcher.AssertRunningOnDispatcher();

        // Enqueue the latest notification.
        _notificationQueue.Enqueue(notification);

        // We're already processing the notification queue, so we're done.
        if (_processingNotifications)
        {
            return;
        }

        Debug.Assert(_notificationQueue.Count == 1, "There should only be a single queued notification when it processing begins.");

        // The notification queue is processed when it contains *exactly* one notification.
        // Note that a notification subscriber may mutate the current solution and cause additional
        // notifications to be be enqueued. However, because we are already running on the dispatcher,
        // those updates will occur synchronously.

        _processingNotifications = true;
        try
        {
            while (_notificationQueue.Count > 0)
            {
                var current = _notificationQueue.Dequeue();

                PriorityChanged?.Invoke(this, current);
                Changed?.Invoke(this, current);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while sending notifications.");
        }
        finally
        {
            _processingNotifications = false;
        }
    }

    private bool TryAddProject(HostProject hostProject, [NotNullWhen(true)] out IProjectSnapshot? newProject, out bool isSolutionClosing)
    {
        if (_initialized)
        {
            _dispatcher.AssertRunningOnDispatcher();
        }

        using var upgradeableLock = _readerWriterLock.DisposableUpgradeableRead();

        isSolutionClosing = _isSolutionClosing;

        // If the solution is closing or the project already exists, don't add a new project.
        if (isSolutionClosing || _projectMap.ContainsKey(hostProject.Key))
        {
            newProject = null;
            return false;
        }

        var state = ProjectState.Create(
            _projectEngineFactoryProvider,
            _languageServerFeatureOptions,
            hostProject,
            ProjectWorkspaceState.Default);

        var newEntry = new Entry(state);

        upgradeableLock.EnterWrite();
        _projectMap.Add(hostProject.Key, newEntry);

        newProject = newEntry.GetSnapshot();
        return true;
    }

    private bool TryRemoveProject(ProjectKey projectKey, [NotNullWhen(true)] out IProjectSnapshot? oldProject, out bool isSolutionClosing)
    {
        if (_initialized)
        {
            _dispatcher.AssertRunningOnDispatcher();
        }

        using var upgradeableLock = _readerWriterLock.DisposableUpgradeableRead();

        isSolutionClosing = _isSolutionClosing;

        if (!_projectMap.TryGetValue(projectKey, out var entry))
        {
            oldProject = null;
            return false;
        }

        oldProject = entry.GetSnapshot();

        upgradeableLock.EnterWrite();
        _projectMap.Remove(projectKey);

        return true;
    }

    private bool TryUpdate(
        ProjectKey projectKey,
        string? documentFilePath,
        IUpdateProjectAction action,
        [NotNullWhen(true)] out IProjectSnapshot? oldSnapshot,
        [NotNullWhen(true)] out IProjectSnapshot? newSnapshot)
    {
        using var upgradeableLock = _readerWriterLock.DisposableUpgradeableRead();

        if (_projectMap.TryGetValue(projectKey, out var entry))
        {
            // if the solution is closing we don't need to bother computing new state
            if (_isSolutionClosing)
            {
                oldSnapshot = newSnapshot = entry.GetSnapshot();
                return true;
            }

            // ... otherwise, compute a new entry and update if it's changed from the old state.
            var documentState = documentFilePath is not null
                ? entry.State.Documents.GetValueOrDefault(documentFilePath)
                : null;

            var newEntry = ComputeNewEntry(entry, action, documentState);

            if (!ReferenceEquals(newEntry.State, entry.State))
            {
                upgradeableLock.EnterWrite();

                _projectMap[projectKey] = newEntry;

                switch (action)
                {
                    case OpenDocumentAction:
                        _openDocumentSet.Add(documentFilePath.AssumeNotNull());
                        break;
                    case CloseDocumentAction:
                        _openDocumentSet.Remove(documentFilePath.AssumeNotNull());
                        break;
                }

                oldSnapshot = entry.GetSnapshot();
                newSnapshot = newEntry.GetSnapshot();

                return true;
            }
        }

        oldSnapshot = newSnapshot = null;
        return false;
    }

    private static Entry ComputeNewEntry(Entry originalEntry, IUpdateProjectAction action, DocumentState? documentState)
    {
        switch (action)
        {
            case AddDocumentAction(var newDocument, var textLoader):
                return new Entry(originalEntry.State.WithAddedHostDocument(newDocument, textLoader));

            case RemoveDocumentAction(var originalDocument):
                return new Entry(originalEntry.State.WithRemovedHostDocument(originalDocument));

            case CloseDocumentAction(var textLoader):
                {
                    // If the document being closed has already been removed from the project then we no-op
                    if (documentState is null)
                    {
                        return originalEntry;
                    }

                    var state = originalEntry.State.WithChangedHostDocument(
                        documentState.HostDocument,
                        textLoader);

                    return new Entry(state);
                }

            case OpenDocumentAction(var sourceText):
                {
                    documentState.AssumeNotNull();

                    if (documentState.TryGetText(out var olderText) &&
                        documentState.TryGetTextVersion(out var olderVersion))
                    {
                        var version = sourceText.ContentEquals(olderText) ? olderVersion : olderVersion.GetNewerVersion();
                        var newState = originalEntry.State.WithChangedHostDocument(documentState.HostDocument, sourceText, version);
                        return new Entry(newState);
                    }
                    else
                    {
                        var newState = originalEntry.State.WithChangedHostDocument(
                            documentState.HostDocument,
                            new UpdatedTextLoader(documentState, sourceText));

                        return new Entry(newState);
                    }
                }

            case DocumentTextLoaderChangedAction(var textLoader):
                {
                    var newState = originalEntry.State.WithChangedHostDocument(
                        documentState.AssumeNotNull().HostDocument,
                        textLoader);

                    return new Entry(newState);
                }

            case DocumentTextChangedAction(var sourceText):
                {
                    // If the document being changed has already been removed from the project then we no-op
                    if (documentState is null)
                    {
                        return originalEntry;
                    }

                    if (documentState.TryGetText(out var olderText) &&
                        documentState.TryGetTextVersion(out var olderVersion))
                    {
                        var version = sourceText.ContentEquals(olderText) ? olderVersion : olderVersion.GetNewerVersion();
                        var state = originalEntry.State.WithChangedHostDocument(documentState.HostDocument, sourceText, version);

                        return new Entry(state);
                    }
                    else
                    {
                        var state = originalEntry.State.WithChangedHostDocument(
                            documentState.HostDocument,
                            new UpdatedTextLoader(documentState, sourceText));

                        return new Entry(state);
                    }
                }

            case ProjectWorkspaceStateChangedAction(var workspaceState):
                return new Entry(originalEntry.State.WithProjectWorkspaceState(workspaceState));

            case HostProjectUpdatedAction(var hostProject):
                return new Entry(originalEntry.State.WithHostProject(hostProject));

            default:
                throw new InvalidOperationException($"Unexpected action type {action.GetType()}");
        }
    }

    private sealed class UpdatedTextLoader(DocumentState oldState, SourceText newSourceText) : TextLoader
    {
        public override async Task<TextAndVersion> LoadTextAndVersionAsync(LoadTextOptions options, CancellationToken cancellationToken)
        {
            var oldTextAndVersion = await oldState.GetTextAndVersionAsync(cancellationToken).ConfigureAwait(false);

            var version = newSourceText.ContentEquals(oldTextAndVersion.Text)
                ? oldTextAndVersion.Version
                : oldTextAndVersion.Version.GetNewerVersion();

            return TextAndVersion.Create(newSourceText, version);
        }
    }

    public Task UpdateAsync(Action<Updater> updater, CancellationToken cancellationToken)
    {
        return _dispatcher.RunAsync(
            static x => x.updater(new(x.instance)),
            (updater, instance: this),
            cancellationToken);
    }

    public Task UpdateAsync<TState>(Action<Updater, TState> updater, TState state, CancellationToken cancellationToken)
    {
        return _dispatcher.RunAsync(
            static x => x.updater(new(x.instance), x.state),
            (updater, state, instance: this),
            cancellationToken);
    }

    public Task<TResult> UpdateAsync<TResult>(Func<Updater, TResult> updater, CancellationToken cancellationToken)
    {
        return _dispatcher.RunAsync(
            static x => x.updater(new(x.instance)),
            (updater, instance: this),
            cancellationToken);
    }

    public Task<TResult> UpdateAsync<TState, TResult>(Func<Updater, TState, TResult> updater, TState state, CancellationToken cancellationToken)
    {
        return _dispatcher.RunAsync(
            static x => x.updater(new(x.instance), x.state),
            (updater, state, instance: this),
            cancellationToken);
    }

    public Task UpdateAsync(Func<Updater, Task> updater, CancellationToken cancellationToken)
    {
        return _dispatcher.RunAsync(
            static x => x.updater(new(x.instance)),
            (updater, instance: this),
            cancellationToken).Unwrap();
    }

    public Task UpdateAsync<TState>(Func<Updater, TState, Task> updater, TState state, CancellationToken cancellationToken)
    {
        return _dispatcher.RunAsync(
            static x => x.updater(new(x.instance), x.state),
            (updater, state, instance: this),
            cancellationToken).Unwrap();
    }

    public Task<TResult> UpdateAsync<TResult>(Func<Updater, Task<TResult>> updater, CancellationToken cancellationToken)
    {
        return _dispatcher.RunAsync(
            static x => x.updater(new(x.instance)),
            (updater, instance: this),
            cancellationToken).Unwrap();
    }

    public Task<TResult> UpdateAsync<TState, TResult>(Func<Updater, TState, Task<TResult>> updater, TState state, CancellationToken cancellationToken)
    {
        return _dispatcher.RunAsync(
            static x => x.updater(new(x.instance), x.state),
            (updater, state, instance: this),
            cancellationToken).Unwrap();
    }
}
