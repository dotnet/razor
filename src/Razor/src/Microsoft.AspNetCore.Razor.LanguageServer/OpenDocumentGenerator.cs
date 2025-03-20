// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal partial class OpenDocumentGenerator : IRazorStartupService, IDisposable
{
    // Using 10 milliseconds for the delay here because we want document synchronization to be very fast,
    // so that features like completion are not delayed, but at the same time we don't want to do more work
    // than necessary when both C# and HTML documents change at the same time, firing our event handler
    // twice. Through testing 10ms was a good balance towards providing some de-bouncing but having minimal
    // to no impact on results.
    //
    // It's worth noting that the queue implementation means that this delay is not restarted with each new
    // work item, so even in very high speed typing, with changes coming in at sub-10-millisecond speed,
    // the queue will still process documents even if the user doesn't pause at all, but also will not process
    // a document for each keystroke.
    private static readonly TimeSpan s_delay = TimeSpan.FromMilliseconds(10);

    private readonly ImmutableArray<IDocumentProcessedListener> _listeners;
    private readonly ProjectSnapshotManager _projectManager;
    private readonly LanguageServerFeatureOptions _options;
    private readonly ILogger _logger;

    private readonly AsyncBatchingWorkQueue<DocumentKey> _workQueue;
    private readonly CancellationTokenSource _disposeTokenSource;
    private readonly HashSet<DocumentKey> _workerSet;

    public OpenDocumentGenerator(
        IEnumerable<IDocumentProcessedListener> listeners,
        ProjectSnapshotManager projectManager,
        LanguageServerFeatureOptions options,
        ILoggerFactory loggerFactory)
    {
        _listeners = [.. listeners];
        _projectManager = projectManager;
        _options = options;

        _workerSet = [];
        _disposeTokenSource = new();
        _workQueue = new AsyncBatchingWorkQueue<DocumentKey>(
            s_delay,
            ProcessBatchAsync,
            _disposeTokenSource.Token);

        _projectManager.Changed += ProjectManager_Changed;
        _logger = loggerFactory.GetOrCreateLogger<OpenDocumentGenerator>();
    }

    public void Dispose()
    {
        if (_disposeTokenSource.IsCancellationRequested)
        {
            return;
        }

        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
    }

    private async ValueTask ProcessBatchAsync(ImmutableArray<DocumentKey> items, CancellationToken token)
    {
        _workerSet.Clear();

        foreach (var key in items.GetMostRecentUniqueItems(_workerSet))
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (!_projectManager.TryGetDocument(key, out var document))
            {
                continue;
            }

            _logger.LogDebug($"Generating {key} at version {document.Version}");

            var codeDocument = await document.GetGeneratedOutputAsync(token).ConfigureAwait(false);

            foreach (var listener in _listeners)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                listener.DocumentProcessed(codeDocument, document);
            }
        }
    }

    private void ProjectManager_Changed(object? sender, ProjectChangeEventArgs args)
    {
        // Don't do any work if the solution is closing
        if (args.IsSolutionClosing)
        {
            return;
        }

        _logger.LogDebug($"Got a project change of type {args.Kind} for {args.ProjectKey.Id}");

        switch (args.Kind)
        {
            case ProjectChangeKind.ProjectChanged:
                {
                    var newProject = args.Newer.AssumeNotNull();

                    foreach (var documentFilePath in newProject.DocumentFilePaths)
                    {
                        EnqueueIfNecessary(new(newProject.Key, documentFilePath));
                    }

                    break;
                }

            case ProjectChangeKind.DocumentAdded:
            case ProjectChangeKind.DocumentChanged:
                {
                    // Most of the time Add will be called on closed files, but when migrating files to/from the misc files
                    // project they could already be open, but with a different generated C# path

                    var newProject = args.Newer.AssumeNotNull();
                    var documentFilePath = args.DocumentFilePath.AssumeNotNull();

                    EnqueueIfNecessary(new(newProject.Key, documentFilePath));

                    if (newProject.TryGetDocument(documentFilePath, out var document))
                    {
                        foreach (var relatedDocument in newProject.GetRelatedDocuments(document))
                        {
                            EnqueueIfNecessary(relatedDocument.Key);
                        }
                    }

                    break;
                }

            case ProjectChangeKind.DocumentRemoved:
                {
                    var newProject = args.Newer.AssumeNotNull();
                    var oldProject = args.Older.AssumeNotNull();
                    var documentFilePath = args.DocumentFilePath.AssumeNotNull();

                    if (oldProject.TryGetDocument(documentFilePath, out var document))
                    {
                        foreach (var relatedDocument in oldProject.GetRelatedDocuments(document))
                        {
                            var relatedDocumentFilePath = relatedDocument.FilePath;

                            if (newProject.TryGetDocument(relatedDocumentFilePath, out var newRelatedDocument))
                            {
                                EnqueueIfNecessary(newRelatedDocument.Key);
                            }
                        }
                    }

                    break;
                }

            case ProjectChangeKind.ProjectRemoved:
                {
                    // No-op. We don't need to enqueue recompilations if the project is being removed
                    break;
                }
        }

        void EnqueueIfNecessary(DocumentKey documentKey)
        {
            if (!_options.UpdateBuffersForClosedDocuments &&
                !_projectManager.IsDocumentOpen(documentKey.FilePath))
            {
                return;
            }

            _workQueue.AddWork(documentKey);
        }
    }
}
