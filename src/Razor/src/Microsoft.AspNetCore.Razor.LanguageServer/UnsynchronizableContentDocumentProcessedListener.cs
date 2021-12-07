// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class UnsynchronizableContentDocumentProcessedListener : DocumentProcessedListener
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentVersionCache _documentVersionCache;
        private readonly GeneratedDocumentPublisher _generatedDocumentPublisher;
        private ProjectSnapshotManager _projectManager;

        public UnsynchronizableContentDocumentProcessedListener(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentVersionCache documentVersionCache,
            GeneratedDocumentPublisher generatedDocumentPublisher)
        {
            if (projectSnapshotManagerDispatcher is null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (documentVersionCache is null)
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
        }

        public override void DocumentProcessed(DocumentSnapshot document)
        {
            _projectSnapshotManagerDispatcher.AssertDispatcherThread();

            if (!_projectManager.IsDocumentOpen(document.FilePath))
            {
                return;
            }

            if (document is not DefaultDocumentSnapshot defaultDocument)
            {
                return;
            }

            if (!_documentVersionCache.TryGetDocumentVersion(document, out var nullableSyncVersion))
            {
                // Document is no longer important.
                return;
            }

            var syncVersion = nullableSyncVersion.Value;

            var documentContainer = defaultDocument.State.GeneratedDocumentContainer;
            var latestSynchronizedDocument = documentContainer.LatestDocument;
            if (latestSynchronizedDocument is null ||
                latestSynchronizedDocument == document)
            {
                // Already up-to-date
                return;
            }

            if (UnchangedHostDocument(document, latestSynchronizedDocument, syncVersion))
            {
                // Documents are identical but we didn't synchronize them because they didn't need to be re-evaluated.
                _generatedDocumentPublisher.PublishCSharp(document.FilePath, documentContainer.CSharpSourceTextContainer.CurrentText, syncVersion);
                _generatedDocumentPublisher.PublishHtml(document.FilePath, documentContainer.HtmlSourceTextContainer.CurrentText, syncVersion);
            }
        }

        public override void Initialize(ProjectSnapshotManager projectManager)
        {
            _projectManager = projectManager;
        }

        private bool UnchangedHostDocument(DocumentSnapshot document, DocumentSnapshot latestSynchronizedDocument, int syncVersion)
        {
            return latestSynchronizedDocument.TryGetTextVersion(out var latestSourceVersion) &&
                document.TryGetTextVersion(out var documentSourceVersion) &&
                _documentVersionCache.TryGetDocumentVersion(latestSynchronizedDocument, out var lastSynchronizedVersion) &&
                syncVersion > lastSynchronizedVersion &&
                latestSourceVersion == documentSourceVersion;
        }
    }
}
