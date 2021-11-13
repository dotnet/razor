﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    internal class DefaultLSPDocument : LSPDocument
    {
        private LSPDocumentSnapshot _currentSnapshot;

        public DefaultLSPDocument(
            Uri uri,
            ITextBuffer textBuffer,
            IReadOnlyList<VirtualDocument> virtualDocuments)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (textBuffer is null)
            {
                throw new ArgumentNullException(nameof(textBuffer));
            }

            if (virtualDocuments is null)
            {
                throw new ArgumentNullException(nameof(virtualDocuments));
            }

            Uri = uri;
            TextBuffer = textBuffer;
            VirtualDocuments = virtualDocuments;
        }

        public override int Version => TextBuffer.CurrentSnapshot.Version.VersionNumber;

        public override Uri Uri { get; }

        public override ITextBuffer TextBuffer { get; }

        public override IReadOnlyList<VirtualDocument> VirtualDocuments { get; }

        public override LSPDocumentSnapshot CurrentSnapshot
        {
            get
            {
                if (TextBuffer.CurrentSnapshot.ContentType.IsOfType(InertContentType.Instance.TypeName))
                {
                    // TextBuffer is tearing itself down, return last known snapshot to avoid generating
                    // a snapshot for an invalid TextBuffer
                    return _currentSnapshot;
                }

                if (_currentSnapshot?.Snapshot != TextBuffer.CurrentSnapshot)
                {
                    _currentSnapshot = UpdateSnapshot();
                }

                return _currentSnapshot;
            }
        }

        public override LSPDocumentSnapshot UpdateVirtualDocument<TVirtualDocument>(IReadOnlyList<ITextChange> changes, int hostDocumentVersion, object state)
        {
            if (!TryGetVirtualDocument<TVirtualDocument>(out var virtualDocument))
            {
                throw new InvalidOperationException($"Cannot update virtual document of type {typeof(TVirtualDocument)} because LSP document {Uri} does not contain a virtual document of that type.");
            }

            virtualDocument.Update(changes, hostDocumentVersion, state);

            _currentSnapshot = UpdateSnapshot();

            return CurrentSnapshot;
        }

        private DefaultLSPDocumentSnapshot UpdateSnapshot()
        {
            var virtualDocumentSnapshots = new VirtualDocumentSnapshot[VirtualDocuments.Count];
            for (var i = 0; i < VirtualDocuments.Count; i++)
            {
                virtualDocumentSnapshots[i] = VirtualDocuments[i].CurrentSnapshot;
            }

            return new DefaultLSPDocumentSnapshot(Uri, TextBuffer.CurrentSnapshot, virtualDocumentSnapshots, Version);
        }

        private class DefaultLSPDocumentSnapshot : LSPDocumentSnapshot
        {
            public DefaultLSPDocumentSnapshot(
                Uri uri,
                ITextSnapshot snapshot,
                IReadOnlyList<VirtualDocumentSnapshot> virtualDocuments,
                int version)
            {
                if (uri is null)
                {
                    throw new ArgumentNullException(nameof(uri));
                }

                if (snapshot is null)
                {
                    throw new ArgumentNullException(nameof(snapshot));
                }

                if (virtualDocuments is null)
                {
                    throw new ArgumentNullException(nameof(virtualDocuments));
                }

                Uri = uri;
                Snapshot = snapshot;
                VirtualDocuments = virtualDocuments;
                Version = version;
            }

            public override Uri Uri { get; }

            public override ITextSnapshot Snapshot { get; }

            public override IReadOnlyList<VirtualDocumentSnapshot> VirtualDocuments { get; }

            public override int Version { get; }

        }
    }
}
