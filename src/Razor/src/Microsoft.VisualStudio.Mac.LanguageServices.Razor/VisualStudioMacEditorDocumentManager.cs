﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.VisualStudio.Editor.Razor.Documents;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using MonoDevelop.Ide;

namespace Microsoft.VisualStudio.Mac.LanguageServices.Razor
{
    internal class VisualStudioMacEditorDocumentManager : EditorDocumentManagerBase
    {
        public VisualStudioMacEditorDocumentManager(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            JoinableTaskContext joinableTaskContext,
            FileChangeTrackerFactory fileChangeTrackerFactory)
            : base(projectSnapshotManagerDispatcher, joinableTaskContext, fileChangeTrackerFactory)
        {
        }

        protected override ITextBuffer? GetTextBufferForOpenDocument(string filePath)
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            var document = IdeApp.Workbench.GetDocument(filePath);
            return document?.GetContent<ITextBuffer>();
        }

        protected override void OnDocumentOpened(EditorDocument document)
        {
        }

        protected override void OnDocumentClosed(EditorDocument document)
        {
        }

        public void HandleDocumentOpened(string filePath, ITextBuffer textBuffer)
        {
            JoinableTaskContext.AssertUIThread();

            lock (Lock)
            {
                if (!TryGetMatchingDocuments(filePath, out var documents))
                {
                    // This isn't a document that we're interesting in.
                    return;
                }

                BufferLoaded(textBuffer, filePath, documents);
            }
        }

        public void HandleDocumentClosed(string filePath)
        {
            JoinableTaskContext.AssertUIThread();

            lock (Lock)
            {
                if (!TryGetMatchingDocuments(filePath, out var documents))
                {
                    return;
                }

                // We have to deal with some complications here due to renames and event ordering and such.
                // We might see multiple documents open for a cookie (due to linked files), but only one of them
                // has been renamed. In that case, we just process the change that we know about.
                var matchingFilePaths = documents.Select(d => d.DocumentFilePath);
                var filePaths = new HashSet<string>(matchingFilePaths, FilePathComparer.Instance);

                foreach (var file in filePaths)
                {
                    DocumentClosed(file);
                }
            }
        }

        public void HandleDocumentRenamed(string fromFilePath, string toFilePath, ITextBuffer textBuffer)
        {
            JoinableTaskContext.AssertUIThread();

            if (string.Equals(fromFilePath, toFilePath, FilePathComparison.Instance))
            {
                return;
            }

            lock (Lock)
            {
                // Treat a rename as a close + reopen.
                //
                // Due to ordering issues, we could see a partial rename. This is why we need to pass the new
                // file path here.
                DocumentClosed(fromFilePath);
            }

            DocumentOpened(toFilePath, textBuffer);
        }

        public void BufferLoaded(ITextBuffer textBuffer, string filePath, EditorDocument[] documents)
        {
            JoinableTaskContext.AssertUIThread();

            lock (Lock)
            {
                for (var i = 0; i < documents.Length; i++)
                {
                    DocumentOpened(filePath, textBuffer);
                }
            }
        }
    }
}
