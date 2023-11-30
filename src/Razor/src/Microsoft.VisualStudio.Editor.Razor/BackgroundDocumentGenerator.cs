﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Internal;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.CodeAnalysis.Razor;

[Shared]
[Export(typeof(IProjectSnapshotChangeTrigger))]
internal class BackgroundDocumentGenerator : IProjectSnapshotChangeTrigger
{
    // Internal for testing
    internal readonly Dictionary<DocumentKey, (IProjectSnapshot project, IDocumentSnapshot document)> Work;

    private readonly ProjectSnapshotManagerDispatcher _dispatcher;
    private readonly RazorDynamicFileInfoProvider _infoProvider;
    private readonly HashSet<string> _suppressedDocuments;
    private ProjectSnapshotManagerBase? _projectManager;
    private Timer? _timer;
    private bool _solutionIsClosing;

    [ImportingConstructor]
    public BackgroundDocumentGenerator(ProjectSnapshotManagerDispatcher dispatcher, RazorDynamicFileInfoProvider infoProvider)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _infoProvider = infoProvider ?? throw new ArgumentNullException(nameof(infoProvider));
        _suppressedDocuments = new HashSet<string>(FilePathComparer.Instance);

        Work = new Dictionary<DocumentKey, (IProjectSnapshot project, IDocumentSnapshot document)>();
    }

    public bool HasPendingNotifications
    {
        get
        {
            lock (Work)
            {
                return Work.Count > 0;
            }
        }
    }

    // Used in unit tests to control the timer delay.
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(2);

    public bool IsScheduledOrRunning => _timer != null;

    // Used in unit tests to ensure we can control when background work starts.
    public ManualResetEventSlim? BlockBackgroundWorkStart { get; set; }

    // Used in unit tests to ensure we can know when background work finishes.
    public ManualResetEventSlim? NotifyBackgroundWorkStarting { get; set; }

    // Used in unit tests to ensure we can know when background has captured its current workload.
    public ManualResetEventSlim? NotifyBackgroundCapturedWorkload { get; set; }

    // Used in unit tests to ensure we can control when background work completes.
    public ManualResetEventSlim? BlockBackgroundWorkCompleting { get; set; }

    // Used in unit tests to ensure we can know when background work finishes.
    public ManualResetEventSlim? NotifyBackgroundWorkCompleted { get; set; }

    // Used in unit tests to ensure we can know when errors are reported
    public ManualResetEventSlim? NotifyErrorBeingReported { get; set; }

    private void OnStartingBackgroundWork()
    {
        if (BlockBackgroundWorkStart is not null)
        {
            BlockBackgroundWorkStart.Wait();
            BlockBackgroundWorkStart.Reset();
        }

        NotifyBackgroundWorkStarting?.Set();
    }

    private void OnCompletingBackgroundWork()
    {
        if (BlockBackgroundWorkCompleting is not null)
        {
            BlockBackgroundWorkCompleting.Wait();
            BlockBackgroundWorkCompleting.Reset();
        }
    }

    private void OnCompletedBackgroundWork()
    {
        NotifyBackgroundWorkCompleted?.Set();
    }

    private void OnBackgroundCapturedWorkload()
    {
        NotifyBackgroundCapturedWorkload?.Set();
    }

    private void OnErrorBeingReported()
    {
        NotifyErrorBeingReported?.Set();
    }

    public void Initialize(ProjectSnapshotManagerBase projectManager)
    {
        _projectManager = projectManager;
        _projectManager.Changed += ProjectManager_Changed;
    }

    protected virtual async Task ProcessDocumentAsync(IProjectSnapshot project, IDocumentSnapshot document)
    {
        await document.GetGeneratedOutputAsync().ConfigureAwait(false);

        UpdateFileInfo(project, document);
    }

    public void Enqueue(IProjectSnapshot project, IDocumentSnapshot document)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (project is ProjectSnapshot { HostProject: FallbackHostProject })
        {
            // We don't support closed file code generation for fallback projects
            return;
        }

        _dispatcher.AssertDispatcherThread();

        lock (Work)
        {
            if (Suppressed(project, document))
            {
                return;
            }

            // We only want to store the last 'seen' version of any given document. That way when we pick one to process
            // it's always the best version to use.
            Work[new DocumentKey(project.Key, document.FilePath.AssumeNotNull())] = (project, document);

            StartWorker();
        }
    }

    protected virtual void StartWorker()
    {
        // Access to the timer is protected by the lock in Enqueue and in Timer_Tick
        // Timer will fire after a fixed delay, but only once.
        _timer ??= NonCapturingTimer.Create(state => ((BackgroundDocumentGenerator)state).Timer_Tick(), this, Delay, Timeout.InfiniteTimeSpan);
    }

    private void Timer_Tick()
    {
        TimerTickAsync().Forget();
    }

    private async Task TimerTickAsync()
    {
        Assumes.NotNull(_timer);

        try
        {
            // Timer is stopped.
            _timer.Change(Timeout.Infinite, Timeout.Infinite);

            OnStartingBackgroundWork();

            KeyValuePair<DocumentKey, (IProjectSnapshot project, IDocumentSnapshot document)>[] work;
            lock (Work)
            {
                work = Work.ToArray();
                Work.Clear();
            }

            OnBackgroundCapturedWorkload();

            for (var i = 0; i < work.Length; i++)
            {
                // If the solution is closing, suspect any in-progress work
                if (_solutionIsClosing)
                {
                    break;
                }

                var (project, document) = work[i].Value;
                try
                {
                    await ProcessDocumentAsync(project, document).ConfigureAwait(false);
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore UnauthorizedAccessException. These can occur when a file gets its permissions changed as we're processing it.
                }
                catch (IOException)
                {
                    // Ignore IOException. These can occur when a file was in the middle of being renamed and it disappears as we're processing it.
                    // This is a common case and does not warrant an activity log entry.
                }
                catch (Exception ex)
                {
                    ReportError(project, ex);
                }
            }

            OnCompletingBackgroundWork();

            lock (Work)
            {
                // Resetting the timer allows another batch of work to start.
                _timer.Dispose();
                _timer = null;

                // If more work came in while we were running start the worker again, unless the solution
                // is being closed.
                if (Work.Count > 0 && !_solutionIsClosing)
                {
                    StartWorker();
                }
            }

            OnCompletedBackgroundWork();
        }
        catch (Exception ex)
        {
            Assumes.NotNull(_projectManager);

            // This is something totally unexpected, let's just send it over to the workspace.
            await _dispatcher
                .RunOnDispatcherThreadAsync(
                    () => _projectManager.ReportError(ex),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private void ReportError(IProjectSnapshot project, Exception ex)
    {
        OnErrorBeingReported();

        Assumes.NotNull(_projectManager);

        _dispatcher.RunOnDispatcherThreadAsync(
            () => _projectManager.ReportError(ex, project),
            CancellationToken.None).Forget();
    }

    private bool Suppressed(IProjectSnapshot project, IDocumentSnapshot document)
    {
        Assumes.NotNull(_projectManager);

        lock (_suppressedDocuments)
        {
            var filePath = document.FilePath.AssumeNotNull();

            if (_projectManager.IsDocumentOpen(filePath))
            {
                _suppressedDocuments.Add(filePath);
                _infoProvider.SuppressDocument(project.Key, filePath);
                return true;
            }

            _suppressedDocuments.Remove(filePath);
            return false;
        }
    }

    private void UpdateFileInfo(IProjectSnapshot project, IDocumentSnapshot document)
    {
        lock (_suppressedDocuments)
        {
            var filePath = document.FilePath.AssumeNotNull();

            if (!_suppressedDocuments.Contains(filePath))
            {
                var container = new DefaultDynamicDocumentContainer(document);
                _infoProvider.UpdateFileInfo(project.Key, container);
            }
        }
    }

    private void ProjectManager_Changed(object sender, ProjectChangeEventArgs args)
    {
        // We don't want to do any work on solution close
        if (args.SolutionIsClosing)
        {
            _solutionIsClosing = true;
            return;
        }

        _solutionIsClosing = false;

        switch (args.Kind)
        {
            case ProjectChangeKind.ProjectAdded:
                {
                    var newProject = args.Newer.AssumeNotNull();

                    foreach (var documentFilePath in newProject.DocumentFilePaths)
                    {
                        if (newProject.GetDocument(documentFilePath) is { } document)
                        {
                            Enqueue(newProject, document);
                        }
                    }

                    break;
                }
            case ProjectChangeKind.ProjectChanged:
                {
                    var newProject = args.Newer.AssumeNotNull();

                    foreach (var documentFilePath in newProject.DocumentFilePaths)
                    {
                        if (newProject.GetDocument(documentFilePath) is { } document)
                        {
                            Enqueue(newProject, document);
                        }
                    }

                    break;
                }

            case ProjectChangeKind.DocumentAdded:
            case ProjectChangeKind.DocumentChanged:
                {
                    var newProject = args.Newer.AssumeNotNull();
                    var documentFilePath = args.DocumentFilePath.AssumeNotNull();

                    if (newProject.GetDocument(documentFilePath) is { } document)
                    {
                        Enqueue(newProject, document);

                        foreach (var relatedDocument in newProject.GetRelatedDocuments(document))
                        {
                            Enqueue(newProject, relatedDocument);
                        }
                    }

                    break;
                }

            case ProjectChangeKind.DocumentRemoved:
                {
                    // For removals use the old snapshot to find the removed document, so we can figure out
                    // what the imports were in the new snapshot.
                    var newProject = args.Newer.AssumeNotNull();
                    var oldProject = args.Older.AssumeNotNull();
                    var documentFilePath = args.DocumentFilePath.AssumeNotNull();

                    if (oldProject.GetDocument(documentFilePath) is { } document)
                    {
                        foreach (var relatedDocument in newProject.GetRelatedDocuments(document))
                        {
                            Enqueue(newProject, relatedDocument);
                        }
                    }

                    break;
                }

            case ProjectChangeKind.ProjectRemoved:
                {
                    // ignore
                    break;
                }

            default:
                throw new InvalidOperationException($"Unknown ProjectChangeKind {args.Kind}");
        }
    }
}
