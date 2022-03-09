// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.OmniSharpPlugin.StrongNamed
{
    public class OmniSharpBackgroundDocumentGenerator : IOmniSharpProjectSnapshotManagerChangeTrigger
    {
        private readonly BackgroundDocumentGenerator _backgroundDocumentGenerator;

        public OmniSharpBackgroundDocumentGenerator(
            OmniSharpProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher!!,
            RemoteTextLoaderFactory remoteTextLoaderFactory!!,
            IEnumerable<OmniSharpDocumentProcessedListener> documentProcessedListeners!!)
        {
            var wrappedListeners = documentProcessedListeners.Select(listener => new WrappedDocumentProcessedListener(remoteTextLoaderFactory, listener));
            _backgroundDocumentGenerator = new BackgroundDocumentGenerator(projectSnapshotManagerDispatcher.InternalDispatcher, wrappedListeners);
        }

        public void Initialize(OmniSharpProjectSnapshotManagerBase projectManager)
        {
            _backgroundDocumentGenerator.Initialize(projectManager.InternalProjectSnapshotManager);
        }

        private class WrappedDocumentProcessedListener : DocumentProcessedListener
        {
            private readonly RemoteTextLoaderFactory _remoteTextLoaderFactory;
            private readonly OmniSharpDocumentProcessedListener _innerDocumentProcessedListener;

            public WrappedDocumentProcessedListener(
                RemoteTextLoaderFactory remoteTextLoaderFactory!!,
                OmniSharpDocumentProcessedListener innerDocumentProcessedListener!!)
            {
                _remoteTextLoaderFactory = remoteTextLoaderFactory;
                _innerDocumentProcessedListener = innerDocumentProcessedListener;
            }

            public override void DocumentProcessed(DocumentSnapshot document)
            {
                var omniSharpDocument = new OmniSharpDocumentSnapshot(document);
                _innerDocumentProcessedListener.DocumentProcessed(omniSharpDocument);
            }

            public override void Initialize(ProjectSnapshotManager projectManager)
            {
                var omniSharpProjectManager = new DefaultOmniSharpProjectSnapshotManager((ProjectSnapshotManagerBase)projectManager, _remoteTextLoaderFactory);
                _innerDocumentProcessedListener.Initialize(omniSharpProjectManager);
            }
        }
    }
}
