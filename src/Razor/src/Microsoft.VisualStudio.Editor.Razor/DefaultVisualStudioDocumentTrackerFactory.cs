// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Editor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor;

internal class DefaultVisualStudioDocumentTrackerFactory : VisualStudioDocumentTrackerFactory
{
    private readonly ProjectSnapshotManagerDispatcher _dispatcher;
    private readonly JoinableTaskContext _joinableTaskContext;
    private readonly ITextDocumentFactoryService _textDocumentFactory;
    private readonly ProjectPathProvider _projectPathProvider;
    private readonly Workspace _workspace;
    private readonly IImportDocumentManager _importDocumentManager;
    private readonly ProjectSnapshotManager _projectManager;
    private readonly WorkspaceEditorSettings _workspaceEditorSettings;

    public DefaultVisualStudioDocumentTrackerFactory(
        ProjectSnapshotManagerDispatcher dispatcher,
        JoinableTaskContext joinableTaskContext,
        ProjectSnapshotManager projectManager,
        WorkspaceEditorSettings workspaceEditorSettings,
        ProjectPathProvider projectPathProvider,
        ITextDocumentFactoryService textDocumentFactory,
        IImportDocumentManager importDocumentManager,
        Workspace workspace)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _joinableTaskContext = joinableTaskContext ?? throw new ArgumentNullException(nameof(joinableTaskContext));
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        _workspaceEditorSettings = workspaceEditorSettings ?? throw new ArgumentNullException(nameof(workspaceEditorSettings));
        _projectPathProvider = projectPathProvider ?? throw new ArgumentNullException(nameof(projectPathProvider));
        _textDocumentFactory = textDocumentFactory ?? throw new ArgumentNullException(nameof(textDocumentFactory));
        _importDocumentManager = importDocumentManager ?? throw new ArgumentNullException(nameof(importDocumentManager));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public override VisualStudioDocumentTracker? Create(ITextBuffer textBuffer)
    {
        if (textBuffer is null)
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
        var tracker = new DefaultVisualStudioDocumentTracker(
            _dispatcher, _joinableTaskContext, filePath, projectPath, _projectManager, _workspaceEditorSettings, _workspace, textBuffer, _importDocumentManager);

        return tracker;
    }
}
