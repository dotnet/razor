﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

[Name(nameof(RazorContentTypeChangeListener))]
[Export(typeof(ITextBufferContentTypeListener))]
[ContentType(RazorConstants.RazorLSPContentTypeName)]
internal class RazorContentTypeChangeListener : ITextBufferContentTypeListener
{
    private readonly TrackingLSPDocumentManager _lspDocumentManager;
    private readonly ITextDocumentFactoryService _textDocumentFactory;
    private readonly LSPEditorFeatureDetector _lspEditorFeatureDetector;
    private readonly IEditorOptionsFactoryService _editorOptionsFactory;
    private readonly IFileToContentTypeService _fileToContentTypeService;

    [ImportingConstructor]
    public RazorContentTypeChangeListener(
        ITextDocumentFactoryService textDocumentFactory,
        LSPDocumentManager lspDocumentManager,
        LSPEditorFeatureDetector lspEditorFeatureDetector,
        IEditorOptionsFactoryService editorOptionsFactory,
        IFileToContentTypeService fileToContentTypeService)
    {
        if (textDocumentFactory is null)
        {
            throw new ArgumentNullException(nameof(textDocumentFactory));
        }

        if (lspDocumentManager is null)
        {
            throw new ArgumentNullException(nameof(lspDocumentManager));
        }

        if (lspEditorFeatureDetector is null)
        {
            throw new ArgumentNullException(nameof(lspEditorFeatureDetector));
        }

        if (editorOptionsFactory is null)
        {
            throw new ArgumentNullException(nameof(editorOptionsFactory));
        }

        if (fileToContentTypeService is null)
        {
            throw new ArgumentNullException(nameof(fileToContentTypeService));
        }

        if (lspDocumentManager is not TrackingLSPDocumentManager tracking)
        {
            throw new ArgumentException("The LSP document manager should be of type " + typeof(TrackingLSPDocumentManager).FullName, nameof(_lspDocumentManager));
        }

        _lspDocumentManager = tracking;

        _textDocumentFactory = textDocumentFactory;
        _lspEditorFeatureDetector = lspEditorFeatureDetector;
        _editorOptionsFactory = editorOptionsFactory;
        _fileToContentTypeService = fileToContentTypeService;
    }

    public void ContentTypeChanged(ITextBuffer textBuffer, IContentType oldContentType, IContentType newContentType)
    {
        var supportedBefore = oldContentType.IsOfType(RazorConstants.RazorLSPContentTypeName);
        var supportedAfter = newContentType.IsOfType(RazorConstants.RazorLSPContentTypeName);

        if (supportedBefore == supportedAfter)
        {
            // We went from a Razor content type to another Razor content type.
            return;
        }

        if (supportedAfter)
        {
            RazorBufferCreated(textBuffer);
        }
        else if (supportedBefore)
        {
            RazorBufferDisposed(textBuffer);
        }
    }

    // Internal for testing
    internal void RazorBufferCreated(ITextBuffer textBuffer)
    {
        if (textBuffer is null)
        {
            throw new ArgumentNullException(nameof(textBuffer));
        }

        if (!_lspEditorFeatureDetector.IsRemoteClient())
        {
            // Renames on open files don't dispose buffer state so we need to separately monitor the buffer for document renames to ensure
            // we can tell the lsp document manager when state changes.
            MonitorDocumentForRenames(textBuffer);

            // Only need to track documents on a host because we don't do any extra work on remote clients.
            _lspDocumentManager.TrackDocument(textBuffer);
        }
    }

    // Internal for testing
    internal void RazorBufferDisposed(ITextBuffer textBuffer)
    {
        if (textBuffer is null)
        {
            throw new ArgumentNullException(nameof(textBuffer));
        }

        StopMonitoringDocumentForRenames(textBuffer);

        // If we don't know about this document we'll no-op
        _lspDocumentManager.UntrackDocument(textBuffer);
    }

    // Internal for testing
    internal void TextDocument_FileActionOccurred(object sender, TextDocumentFileActionEventArgs args)
    {
        if (args.FileActionType != FileActionTypes.DocumentRenamed)
        {
            // We're only interested in document rename events.
            return;
        }

        if (sender is not ITextDocument textDocument)
        {
            return;
        }

        var textBuffer = textDocument.TextBuffer;

        if (textBuffer is null)
        {
            return;
        }

        // Document was renamed, translate that rename into an untrack -> track to refresh state.

        RazorBufferDisposed(textBuffer);

        // Normally we could just look at the buffer again to see if the content type was still Razor; however,
        // there's a bug in the platform which prevents that from working:
        // https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1161307/
        // To counteract this we need to re-calculate the content type based off of the filepath.
        var newContentType = _fileToContentTypeService.GetContentTypeForFilePath(textDocument.FilePath);
        if (newContentType.IsOfType(RazorConstants.RazorLSPContentTypeName))
        {
            // Renamed to another RazorLSP based document, lets treat it as a re-creation.
            RazorBufferCreated(textBuffer);
        }
    }

    private void MonitorDocumentForRenames(ITextBuffer textBuffer)
    {
        if (!_textDocumentFactory.TryGetTextDocument(textBuffer, out var textDocument))
        {
            // Cannot monitor buffers that don't have an associated text document. In practice, this should never happen but being extra defensive here.
            return;
        }

        textDocument.FileActionOccurred += TextDocument_FileActionOccurred;
    }

    private void StopMonitoringDocumentForRenames(ITextBuffer textBuffer)
    {
        if (!_textDocumentFactory.TryGetTextDocument(textBuffer, out var textDocument))
        {
            // Text document must have been torn down, no need to unsubscribe to something that's already been torn down.
            return;
        }

        textDocument.FileActionOccurred -= TextDocument_FileActionOccurred;
    }
}
