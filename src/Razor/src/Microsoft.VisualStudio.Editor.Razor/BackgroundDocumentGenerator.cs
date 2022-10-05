﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Internal;

namespace Microsoft.CodeAnalysis.Razor
{
    [Shared]
    [Export(typeof(ProjectSnapshotChangeTrigger))]
    internal class BackgroundDocumentGenerator : ProjectSnapshotChangeTrigger
    {
        // Internal for testing
        internal readonly Dictionary<DocumentKey, (ProjectSnapshot project, DocumentSnapshot document)> Work;

        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly RazorDynamicFileInfoProvider _infoProvider;
        private readonly HashSet<string> _suppressedDocuments;
        private ProjectSnapshotManagerBase? _projectManager;
        private Timer? _timer;
        private bool _solutionIsClosing;

        [ImportingConstructor]
        public BackgroundDocumentGenerator(ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher, RazorDynamicFileInfoProvider infoProvider)
        {
            if (projectSnapshotManagerDispatcher is null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (infoProvider is null)
            {
                throw new ArgumentNullException(nameof(infoProvider));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _infoProvider = infoProvider;
            _suppressedDocuments = new HashSet<string>(FilePathComparer.Instance);

            Work = new Dictionary<DocumentKey, (ProjectSnapshot project, DocumentSnapshot document)>();
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

        public override void Initialize(ProjectSnapshotManagerBase projectManager)
        {
            if (projectManager is null)
            {
                throw new ArgumentNullException(nameof(projectManager));
            }

            _projectManager = projectManager;
            _projectManager.Changed += ProjectManager_Changed;
        }

        protected virtual async Task ProcessDocumentAsync(ProjectSnapshot project, DocumentSnapshot document)
        {
            await document.GetGeneratedOutputAsync().ConfigureAwait(false);

            UpdateFileInfo(project, document);
        }

        public void Enqueue(ProjectSnapshot project, DocumentSnapshot document)
        {
            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            _projectSnapshotManagerDispatcher.AssertDispatcherThread();

            lock (Work)
            {
                if (Suppressed(project, document))
                {
                    return;
                }

                // We only want to store the last 'seen' version of any given document. That way when we pick one to process
                // it's always the best version to use.
                Work[new DocumentKey(project.FilePath, document.FilePath)] = (project, document);

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
            _ = TimerTickAsync();
        }

        private async Task TimerTickAsync()
        {
            Assumes.NotNull(_timer);

            try
            {
                // Timer is stopped.
                _timer.Change(Timeout.Infinite, Timeout.Infinite);

                OnStartingBackgroundWork();

                KeyValuePair<DocumentKey, (ProjectSnapshot project, DocumentSnapshot document)>[] work;
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
                        // Ignore IOException. These can occur when a file was in the middle of being renamed and it dissapears as we're processing it.
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
                await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                    () => _projectManager.ReportError(ex),
                    CancellationToken.None).ConfigureAwait(false);
            }
        }

        private void ReportError(ProjectSnapshot project, Exception ex)
        {
            OnErrorBeingReported();

            Assumes.NotNull(_projectManager);

            _ = _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                () => _projectManager.ReportError(ex, project),
                CancellationToken.None);
        }

        private bool Suppressed(ProjectSnapshot project, DocumentSnapshot document)
        {
            Assumes.NotNull(_projectManager);

            lock (_suppressedDocuments)
            {
                if (_projectManager.IsDocumentOpen(document.FilePath))
                {
                    _suppressedDocuments.Add(document.FilePath);
                    _infoProvider.SuppressDocument(project.FilePath, document.FilePath);
                    return true;
                }

                _suppressedDocuments.Remove(document.FilePath);
                return false;
            }
        }

        private void UpdateFileInfo(ProjectSnapshot project, DocumentSnapshot document)
        {
            lock (_suppressedDocuments)
            {
                if (!_suppressedDocuments.Contains(document.FilePath))
                {
                    var container = new DefaultDynamicDocumentContainer(document);
                    _infoProvider.UpdateFileInfo(project.FilePath, container);
                }
            }
        }

        private void ProjectManager_Changed(object sender, ProjectChangeEventArgs e)
        {
            // We don't want to do any work on solution close
            if (e.SolutionIsClosing)
            {
                _solutionIsClosing = true;
                return;
            }

            _solutionIsClosing = false;

            switch (e.Kind)
            {
                case ProjectChangeKind.ProjectAdded:
                    {
                        var projectSnapshot = e.Newer!;
                        foreach (var documentFilePath in projectSnapshot.DocumentFilePaths)
                        {
                            Enqueue(projectSnapshot, projectSnapshot.GetDocument(documentFilePath));
                        }

                        break;
                    }
                case ProjectChangeKind.ProjectChanged:
                    {
                        var projectSnapshot = e.Newer!;
                        foreach (var documentFilePath in projectSnapshot.DocumentFilePaths)
                        {
                            Enqueue(projectSnapshot, projectSnapshot.GetDocument(documentFilePath));
                        }

                        break;
                    }

                case ProjectChangeKind.DocumentAdded:
                case ProjectChangeKind.DocumentChanged:
                    {
                        var project = e.Newer!;
                        var document = project.GetDocument(e.DocumentFilePath);

                        Enqueue(project, document);
                        foreach (var relatedDocument in project.GetRelatedDocuments(document))
                        {
                            Enqueue(project, relatedDocument);
                        }

                        break;
                    }

                case ProjectChangeKind.DocumentRemoved:
                    {
                        // For removals use the old snapshot to find the removed document, so we can figure out
                        // what the imports were in the new snapshot.
                        var document = e.Older!.GetDocument(e.DocumentFilePath);

                        foreach (var relatedDocument in e.Newer!.GetRelatedDocuments(document))
                        {
                            Enqueue(e.Newer, relatedDocument);
                        }

                        break;
                    }

                case ProjectChangeKind.ProjectRemoved:
                    {
                        // ignore
                        break;
                    }

                default:
                    throw new InvalidOperationException($"Unknown ProjectChangeKind {e.Kind}");
            }
        }
    }
}
