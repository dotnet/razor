// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Editor;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.Editor.Razor
{
    [System.Composition.Shared]
    [Export(typeof(VisualStudioDocumentTrackerFactory))]
    internal class DefaultVisualStudioDocumentTrackerFactory : VisualStudioDocumentTrackerFactory
    {
        private readonly ITextDocumentFactoryService _textDocumentFactory;
        private readonly ProjectPathProvider _projectPathProvider;
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly WorkspaceEditorSettings _workspaceEditorSettings;

        [ImportingConstructor]
        public DefaultVisualStudioDocumentTrackerFactory(
            ForegroundDispatcher foregroundDispatcher,
            WorkspaceEditorSettings workspaceEditorSettings,
            ProjectPathProvider projectPathProvider,
            ITextDocumentFactoryService textDocumentFactory)
        {
            if (foregroundDispatcher == null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (workspaceEditorSettings == null)
            {
                throw new ArgumentNullException(nameof(workspaceEditorSettings));
            }

            if (projectPathProvider == null)
            {
                throw new ArgumentNullException(nameof(projectPathProvider));
            }

            if (textDocumentFactory == null)
            {
                throw new ArgumentNullException(nameof(textDocumentFactory));
            }

            _foregroundDispatcher = foregroundDispatcher;
            _workspaceEditorSettings = workspaceEditorSettings;
            _projectPathProvider = projectPathProvider;
            _textDocumentFactory = textDocumentFactory;
        }

        public override VisualStudioDocumentTracker Create(ITextBuffer textBuffer)
        {
            if (textBuffer == null)
            {
                throw new ArgumentNullException(nameof(textBuffer));
            }

            if (!_textDocumentFactory.TryGetTextDocument(textBuffer, out var textDocument))
            {
                Debug.Fail("Text document should be available from the text buffer.");
                return null;
            }

            if (!_projectPathProvider.TryGetProjectPath(textBuffer, out var projectPath))
            {
                return null;
            }

            var filePath = textDocument.FilePath;
            var tracker = new DefaultVisualStudioDocumentTracker(_foregroundDispatcher, filePath, projectPath, _workspaceEditorSettings, textBuffer);

            return tracker;
        }
    }
}
