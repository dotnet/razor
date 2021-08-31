﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorDiagnosticsPublisher : DocumentProcessedListener
    {
        // Internal for testing
        internal TimeSpan _publishDelay = TimeSpan.FromSeconds(2);
        internal readonly Dictionary<string, IReadOnlyList<RazorDiagnostic>> PublishedDiagnostics;
        internal Timer _workTimer;
        internal Timer _documentClosedTimer;

        private static readonly TimeSpan s_checkForDocumentClosedDelay = TimeSpan.FromSeconds(5);
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly ITextDocumentLanguageServer _languageServer;
        private readonly Dictionary<string, DocumentSnapshot> _work;
        private readonly ILogger<RazorDiagnosticsPublisher> _logger;
        private ProjectSnapshotManager _projectManager;

        public RazorDiagnosticsPublisher(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            ITextDocumentLanguageServer languageServer,
            ILoggerFactory loggerFactory)
        {
            if (projectSnapshotManagerDispatcher == null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (languageServer == null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _languageServer = languageServer;
            PublishedDiagnostics = new Dictionary<string, IReadOnlyList<RazorDiagnostic>>(FilePathComparer.Instance);
            _work = new Dictionary<string, DocumentSnapshot>(FilePathComparer.Instance);
            _logger = loggerFactory.CreateLogger<RazorDiagnosticsPublisher>();
        }

        // Used in tests to ensure we can control when background work completes.
        public ManualResetEventSlim BlockBackgroundWorkCompleting { get; set; }

        // Used in tests to ensure we can control when background work completes.
        public ManualResetEventSlim NotifyBackgroundWorkCompleting { get; set; }

        public override void Initialize(ProjectSnapshotManager projectManager)
        {
            if (projectManager == null)
            {
                throw new ArgumentNullException(nameof(projectManager));
            }

            _projectManager = projectManager;
        }

        public override void DocumentProcessed(DocumentSnapshot document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            _projectSnapshotManagerDispatcher.AssertDispatcherThread();

            lock (_work)
            {
                _work[document.FilePath] = document;
                StartWorkTimer();
                StartDocumentClosedCheckTimer();
            }
        }

        private void StartWorkTimer()
        {
            // Access to the timer is protected by the lock in Synchronize and in Timer_Tick
            if (_workTimer == null)
            {
                // Timer will fire after a fixed delay, but only once.
                _workTimer = new Timer(WorkTimer_Tick, null, _publishDelay, Timeout.InfiniteTimeSpan);
            }
        }

        private void StartDocumentClosedCheckTimer()
        {
            if (_documentClosedTimer == null)
            {
                _documentClosedTimer = new Timer(DocumentClosedTimer_Tick, null, s_checkForDocumentClosedDelay, Timeout.InfiniteTimeSpan);
            }
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void DocumentClosedTimer_Tick(object state)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                ClearClosedDocuments,
                CancellationToken.None).ConfigureAwait(false);
        }

        // Internal for testing
        internal void ClearClosedDocuments()
        {
            lock (PublishedDiagnostics)
            {
                var publishedDiagnostics = new Dictionary<string, IReadOnlyList<RazorDiagnostic>>(PublishedDiagnostics);
                foreach (var entry in publishedDiagnostics)
                {
                    if (!_projectManager.IsDocumentOpen(entry.Key))
                    {
                        // Document is now closed, we shouldn't track its diagnostics anymore.
                        PublishedDiagnostics.Remove(entry.Key);

                        // If the last published diagnostics for the document were > 0 then we need to clear them out so the user
                        // doesn't have a ton of closed document errors that they can't get rid of.
                        if (entry.Value.Count > 0)
                        {
                            PublishDiagnosticsForFilePath(entry.Key, Array.Empty<Diagnostic>());
                        }
                    }
                }

                _documentClosedTimer?.Dispose();
                _documentClosedTimer = null;

                if (PublishedDiagnostics.Count > 0)
                {
                    lock (_work)
                    {
                        // There's no way for us to know when a document is closed at this layer. Therefore, we need to poll every X seconds
                        // and check if the currently tracked documents are closed. In practice this work is super minimal.
                        StartDocumentClosedCheckTimer();
                    }
                }
            }
        }

        // Internal for testing
        internal async Task PublishDiagnosticsAsync(DocumentSnapshot document)
        {
            var result = await document.GetGeneratedOutputAsync();

            var diagnostics = result.GetCSharpDocument().Diagnostics;

            lock (PublishedDiagnostics)
            {
                if (PublishedDiagnostics.TryGetValue(document.FilePath, out var previousDiagnostics) &&
                    diagnostics.SequenceEqual(previousDiagnostics))
                {
                    // Diagnostics are the same as last publish
                    return;
                }

                PublishedDiagnostics[document.FilePath] = diagnostics;
            }

            if (!document.TryGetText(out var sourceText))
            {
                Debug.Fail("Document source text should already be available.");
            }
            var convertedDiagnostics = diagnostics.Select(razorDiagnostic => RazorDiagnosticConverter.Convert(razorDiagnostic, sourceText));

            PublishDiagnosticsForFilePath(document.FilePath, convertedDiagnostics);

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                var diagnosticString = string.Join(", ", diagnostics.Select(diagnostic => diagnostic.Id));
                _logger.LogTrace($"Publishing diagnostics for document '{document.FilePath}': {diagnosticString}");
            }
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void WorkTimer_Tick(object state)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            DocumentSnapshot[] documents;
            lock (_work)
            {
                documents = _work.Values.ToArray();
                _work.Clear();
            }

            for (var i = 0; i < documents.Length; i++)
            {
                var document = documents[i];
                await PublishDiagnosticsAsync(document);
            }

            OnCompletingBackgroundWork();

            lock (_work)
            {
                // Resetting the timer allows another batch of work to start.
                _workTimer.Dispose();
                _workTimer = null;

                // If more work came in while we were running start the timer again.
                if (_work.Count > 0)
                {
                    StartWorkTimer();
                }
            }
        }

        private void OnCompletingBackgroundWork()
        {
            if (NotifyBackgroundWorkCompleting != null)
            {
                NotifyBackgroundWorkCompleting.Set();
            }

            if (BlockBackgroundWorkCompleting != null)
            {
                BlockBackgroundWorkCompleting.Wait();
                BlockBackgroundWorkCompleting.Reset();
            }
        }

        private void PublishDiagnosticsForFilePath(string filePath, IEnumerable<Diagnostic> diagnostics)
        {
            var uriBuilder = new UriBuilder()
            {
                Scheme = Uri.UriSchemeFile,
                Path = filePath,
                Host = string.Empty,
            };

            _languageServer.PublishDiagnostics(new PublishDiagnosticsParams()
            {
                Uri = uriBuilder.Uri,
                Diagnostics = new Container<Diagnostic>(diagnostics),
            });
        }
    }
}
