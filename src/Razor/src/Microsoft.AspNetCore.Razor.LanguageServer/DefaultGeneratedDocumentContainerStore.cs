// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultGeneratedDocumentContainerStore : GeneratedDocumentContainerStore
    {
        private readonly ConcurrentDictionary<string, ReferenceOutputCapturingContainer> _store;
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentVersionCache _documentVersionCache;
        private readonly GeneratedDocumentPublisher _generatedDocumentPublisher;
        private ProjectSnapshotManagerBase _projectSnapshotManager;

        public DefaultGeneratedDocumentContainerStore(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentVersionCache documentVersionCache,
            GeneratedDocumentPublisher generatedDocumentPublisher)
        {
            if (projectSnapshotManagerDispatcher == null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (documentVersionCache == null)
            {
                throw new ArgumentNullException(nameof(documentVersionCache));
            }

            if (generatedDocumentPublisher is null)
            {
                throw new ArgumentNullException(nameof(generatedDocumentPublisher));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _documentVersionCache = documentVersionCache;
            _generatedDocumentPublisher = generatedDocumentPublisher;
            _store = new ConcurrentDictionary<string, ReferenceOutputCapturingContainer>(FilePathComparer.Instance);
        }

        public override ReferenceOutputCapturingContainer Get(string physicalFilePath)
        {
            if (physicalFilePath == null)
            {
                throw new ArgumentNullException(nameof(physicalFilePath));
            }

            lock (_store)
            {
                var codeContainer = _store.GetOrAdd(physicalFilePath, Create);
                return codeContainer;
            }
        }

        public override void Initialize(ProjectSnapshotManagerBase projectManager)
        {
            _projectSnapshotManager = projectManager;
            _projectSnapshotManager.Changed += ProjectSnapshotManager_Changed;
        }

        // Internal for testing
        internal void ProjectSnapshotManager_Changed(object sender, ProjectChangeEventArgs args)
        {
            // Don't do any work if the solution is closing
            if (args.SolutionIsClosing)
            {
                return;
            }

            _projectSnapshotManagerDispatcher.AssertDispatcherThread();

            switch (args.Kind)
            {
                case ProjectChangeKind.DocumentChanged:
                case ProjectChangeKind.DocumentRemoved:
                    lock (_store)
                    {
                        if (_store.ContainsKey(args.DocumentFilePath) &&
                            !_projectSnapshotManager.IsDocumentOpen(args.DocumentFilePath))
                        {
                            // Document closed or removed, evict entry.
                            _store.TryRemove(args.DocumentFilePath, out var _);
                        }
                    }
                    break;
            }
        }

        private ReferenceOutputCapturingContainer Create(string filePath)
        {
            var documentContainer = new ReferenceOutputCapturingContainer();
            documentContainer.GeneratedCSharpChanged += (sender, args) =>
            {
                var generatedDocumentContainer = (GeneratedDocumentContainer)sender;

                var latestDocument = generatedDocumentContainer.LatestDocument;

                _ = _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
                {
                    if (!_projectSnapshotManager.IsDocumentOpen(filePath))
                    {
                        // Document isn't opened, no need to notify the client
                        return;
                    }

                    if (!_documentVersionCache.TryGetDocumentVersion(latestDocument, out var nullableHostDocumentVersion))
                    {
                        // Cache entry doesn't exist, document most likely was evicted from the cache/too old.
                        return;
                    }
                    var hostDocumentVersion = nullableHostDocumentVersion.Value;

                    _generatedDocumentPublisher.PublishCSharp(filePath, args.NewText, hostDocumentVersion);
                }, CancellationToken.None);
            };

            documentContainer.GeneratedHtmlChanged += (sender, args) =>
            {
                var generatedDocumentContainer = (GeneratedDocumentContainer)sender;

                var latestDocument = generatedDocumentContainer.LatestDocument;

                _ = _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
                {
                    if (!_projectSnapshotManager.IsDocumentOpen(filePath))
                    {
                        // Document isn't opened, no need to notify the client
                        return;
                    }

                    if (!_documentVersionCache.TryGetDocumentVersion(latestDocument, out var nullableHostDocumentVersion))
                    {
                        // Cache entry doesn't exist, document most likely was evicted from the cache/too old.
                        return;
                    }
                    var hostDocumentVersion = nullableHostDocumentVersion.Value;

                    _generatedDocumentPublisher.PublishHtml(filePath, args.NewText, hostDocumentVersion);
                }, CancellationToken.None);
            };

            return documentContainer;
        }
    }
}
