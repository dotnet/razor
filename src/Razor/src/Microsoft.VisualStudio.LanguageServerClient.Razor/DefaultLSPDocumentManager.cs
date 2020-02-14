// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Shared]
    [Export(typeof(LSPDocumentManager))]
    internal class DefaultLSPDocumentManager : LSPDocumentManager
    {
        private readonly FileUriProvider _fileUriProvider;
        private readonly LSPDocumentFactory _documentFactory;
        private readonly Dictionary<Uri, DocumentTracker> _documents;

        [ImportingConstructor]
        public DefaultLSPDocumentManager(
            FileUriProvider fileUriProvider,
            LSPDocumentFactory documentFactory)
        {
            if (fileUriProvider is null)
            {
                throw new ArgumentNullException(nameof(fileUriProvider));
            }

            if (documentFactory is null)
            {
                throw new ArgumentNullException(nameof(documentFactory));
            }

            _fileUriProvider = fileUriProvider;
            _documentFactory = documentFactory;
            _documents = new Dictionary<Uri, DocumentTracker>();
        }

        public void TrackDocumentView(ITextBuffer buffer, ITextView textView)
        {
            if (buffer is null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            ThreadHelper.CheckAccess();

            var uri = _fileUriProvider.GetOrCreate(buffer);
            if (!_documents.TryGetValue(uri, out var documentTracker))
            {
                var lspDocument = _documentFactory.Create(buffer);
                documentTracker = new DocumentTracker(lspDocument);
                _documents[uri] = documentTracker;
            }

            documentTracker.TextViews.Add(textView);
        }

        public void UntrackDocumentView(ITextBuffer buffer, ITextView textView)
        {
            ThreadHelper.CheckAccess();

            var uri = _fileUriProvider.GetOrCreate(buffer);
            if (!_documents.TryGetValue(uri, out var documentTracker))
            {
                // We don't know about this document, noop.
                return;
            }

            documentTracker.TextViews.Remove(textView);

            if (documentTracker.TextViews.Count == 0)
            {
                _documents.Remove(uri);
            }
        }

        public override LSPDocument GetDocument(Uri uri)
        {
            ThreadHelper.CheckAccess();

            if (!_documents.TryGetValue(uri, out var documentTracker))
            {
                Debug.Fail("We should always know about the documents we're asked for.");
            }

            return documentTracker.Document;
        }

        private class DocumentTracker
        {
            public DocumentTracker(LSPDocument document)
            {
                Document = document;
                TextViews = new HashSet<ITextView>();
            }

            public LSPDocument Document { get; }

            public HashSet<ITextView> TextViews { get; }
        }
    }
}
