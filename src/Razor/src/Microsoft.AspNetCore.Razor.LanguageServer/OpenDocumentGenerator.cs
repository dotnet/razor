// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class OpenDocumentGenerator : ProjectSnapshotChangeTrigger
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly IReadOnlyList<DocumentProcessedListener> _documentProcessedListeners;
        private readonly Dictionary<string, DocumentSnapshot> _work;
        private ProjectSnapshotManagerBase? _projectManager;
        private Timer? _timer;
        private bool _solutionIsClosing;

        public OpenDocumentGenerator(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher!!,
            IEnumerable<DocumentProcessedListener> documentProcessedListeners!!)
        {
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _documentProcessedListeners = documentProcessedListeners.ToArray();
            _work = new Dictionary<string, DocumentSnapshot>(StringComparer.Ordinal);
        }

        // For testing only
        protected OpenDocumentGenerator(ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher)
        {
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _work = new Dictionary<string, DocumentSnapshot>(StringComparer.Ordinal);
            _documentProcessedListeners = Array.Empty<DocumentProcessedListener>();
        }

        public bool HasPendingNotifications
        {
            get
            {
                lock (_work)
                {
                    return _work.Count > 0;
                }
            }
        }

        private ProjectSnapshotManagerBase ProjectManager => _projectManager ?? throw new InvalidOperationException($"{nameof(ProjectManager)} was called because {nameof(Initialize)}");

        public TimeSpan Delay { get; set; } = TimeSpan.Zero;

        public bool IsScheduledOrRunning => _timer != null;

        // Used in tests to ensure we can control when background work starts.
        public ManualResetEventSlim? BlockBackgroundWorkStart { get; set; }

        // Used in tests to ensure we can know when background work finishes.
        public ManualResetEventSlim? NotifyBackgroundWorkStarting { get; set; }

        // Used in unit tests to ensure we can know when background has captured its current workload.
        public ManualResetEventSlim? NotifyBackgroundCapturedWorkload { get; set; }

        // Used in tests to ensure we can control when background work completes.
        public ManualResetEventSlim? BlockBackgroundWorkCompleting { get; set; }

        // Used in tests to ensure we can know when background work finishes.
        public ManualResetEventSlim? NotifyBackgroundWorkCompleted { get; set; }

        public override void Initialize(ProjectSnapshotManagerBase projectManager!!)
        {
            _projectManager = projectManager;

            ProjectManager.Changed += ProjectSnapshotManager_Changed;

            foreach (var documentProcessedListener in _documentProcessedListeners)
            {
                documentProcessedListener.Initialize(ProjectManager);
            }
        }

        private void OnStartingBackgroundWork()
        {
            if (BlockBackgroundWorkStart != null)
            {
                BlockBackgroundWorkStart.Wait();
                BlockBackgroundWorkStart.Reset();
            }

            if (NotifyBackgroundWorkStarting != null)
            {
                NotifyBackgroundWorkStarting.Set();
            }
        }

        private void OnCompletingBackgroundWork()
        {
            if (BlockBackgroundWorkCompleting != null)
            {
                BlockBackgroundWorkCompleting.Wait();
                BlockBackgroundWorkCompleting.Reset();
            }
        }

        private void OnCompletedBackgroundWork()
        {
            if (NotifyBackgroundWorkCompleted != null)
            {
                NotifyBackgroundWorkCompleted.Set();
            }
        }

        private void OnBackgroundCapturedWorkload()
        {
            if (NotifyBackgroundCapturedWorkload != null)
            {
                NotifyBackgroundCapturedWorkload.Set();
            }
        }

        // Internal for testing
        internal void Enqueue(DocumentSnapshot document)
        {
            _projectSnapshotManagerDispatcher.AssertDispatcherThread();

            if (!ProjectManager.IsDocumentOpen(document.FilePath))
            {
                // We don't parse closed documents
                return;
            }

            lock (_work)
            {
                // We only want to store the last 'seen' version of any given document. That way when we pick one to process
                // it's always the best version to use.
                _work[document.FilePath] = document;

                StartWorker();
            }
        }

        private void StartWorker()
        {
            // Access to the timer is protected by the lock in Synchronize and in Timer_Tick
            if (_timer is null)
            {
                // Timer will fire after a fixed delay, but only once.
                _timer = NonCapturingTimer.Create(Timer_Tick, state: null, Delay, Timeout.InfiniteTimeSpan);
            }
        }

        private void Timer_Tick(object state)
        {
            _ = Timer_TickAsync(CancellationToken.None);
        }

        private async Task Timer_TickAsync(CancellationToken cancellationToken)
        {
            try
            {
                OnStartingBackgroundWork();

                KeyValuePair<string, DocumentSnapshot>[] work;
                lock (_work)
                {
                    work = _work.ToArray();
                    _work.Clear();
                }

                OnBackgroundCapturedWorkload();

                for (var i = 0; i < work.Length; i++)
                {
                    if (_solutionIsClosing)
                    {
                        break;
                    }

                    var document = work[i].Value;
                    try
                    {
                        await document.GetGeneratedOutputAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        ReportError(ex);
                    }
                }

                OnCompletingBackgroundWork();

                if (_documentProcessedListeners.Count != 0 && !_solutionIsClosing)
                {
                    await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                        () => NotifyDocumentsProcessed(work),
                        cancellationToken).ConfigureAwait(false);
                }

                lock (_work)
                {
                    // Resetting the timer allows another batch of work to start.
                    _timer?.Dispose();
                    _timer = null;

                    // If more work came in while we were running start the worker again.
                    if (_work.Count > 0 && !_solutionIsClosing)
                    {
                        StartWorker();
                    }
                }

                OnCompletedBackgroundWork();
            }
            catch (Exception ex)
            {
                // This is something totally unexpected, let's just send it over to the workspace.
                ReportError(ex);

                _timer?.Dispose();
                _timer = null;
            }
        }

        private void NotifyDocumentsProcessed(KeyValuePair<string, DocumentSnapshot>[] work)
        {
            for (var i = 0; i < work.Length; i++)
            {
                foreach (var documentProcessedTrigger in _documentProcessedListeners)
                {
                    var document = work[i].Value;
                    documentProcessedTrigger.DocumentProcessed(document);
                }
            }
        }

        private void ProjectSnapshotManager_Changed(object sender, ProjectChangeEventArgs args)
        {
            // Don't do any work if the solution is closing
            if (args.SolutionIsClosing)
            {
                _solutionIsClosing = true;
                return;
            }

            _solutionIsClosing = false;

            _projectSnapshotManagerDispatcher.AssertDispatcherThread();

            switch (args.Kind)
            {
                case ProjectChangeKind.ProjectChanged:
                    {
                        var projectSnapshot = args.Newer!;
                        foreach (var documentFilePath in projectSnapshot.DocumentFilePaths)
                        {
                            var document = projectSnapshot.GetDocument(documentFilePath);
                            Enqueue(document);
                        }

                        break;
                    }

                case ProjectChangeKind.DocumentAdded:
                    {
                        var projectSnapshot = args.Newer!;
                        var document = projectSnapshot.GetDocument(args.DocumentFilePath);

                        // We don't enqueue the current document because added documents are by default closed.

                        foreach (var relatedDocument in projectSnapshot.GetRelatedDocuments(document))
                        {
                            Enqueue(relatedDocument);
                        }

                        break;
                    }

                case ProjectChangeKind.DocumentChanged:
                    {
                        var projectSnapshot = args.Newer!;
                        var document = projectSnapshot.GetDocument(args.DocumentFilePath);
                        Enqueue(document);

                        foreach (var relatedDocument in projectSnapshot.GetRelatedDocuments(document))
                        {
                            Enqueue(relatedDocument);
                        }

                        break;
                    }

                case ProjectChangeKind.DocumentRemoved:
                    {
                        var olderProject = args.Older!;
                        var document = olderProject.GetDocument(args.DocumentFilePath);

                        foreach (var relatedDocument in olderProject.GetRelatedDocuments(document))
                        {
                            var newerRelatedDocument = args.Newer!.GetDocument(relatedDocument.FilePath);
                            Enqueue(newerRelatedDocument);
                        }

                        break;
                    }
            }
        }

        private void ReportError(Exception ex)
        {
            if (_projectManager is null)
            {
                return;
            }

            _ = _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                () => _projectManager.ReportError(ex),
                CancellationToken.None);
        }
    }
}
