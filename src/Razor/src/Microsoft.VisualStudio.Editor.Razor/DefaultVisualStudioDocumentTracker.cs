// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Editor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor;

internal class DefaultVisualStudioDocumentTracker : VisualStudioDocumentTracker
{
    private readonly ProjectSnapshotManagerDispatcher _dispatcher;
    private readonly JoinableTaskContext _joinableTaskContext;
    private readonly string _filePath;
    private readonly string _projectPath;
    private readonly ProjectSnapshotManager _projectManager;
    private readonly WorkspaceEditorSettings _workspaceEditorSettings;
    private readonly ITextBuffer _textBuffer;
    private readonly IImportDocumentManager _importDocumentManager;
    private readonly List<ITextView> _textViews;
    private readonly Workspace _workspace;
    private bool _isSupportedProject;
    private IProjectSnapshot? _projectSnapshot;
    private int _subscribeCount;

    public override event EventHandler<ContextChangeEventArgs>? ContextChanged;

    public DefaultVisualStudioDocumentTracker(
        ProjectSnapshotManagerDispatcher dispatcher,
        JoinableTaskContext joinableTaskContext,
        string filePath,
        string projectPath,
        ProjectSnapshotManager projectManager,
        WorkspaceEditorSettings workspaceEditorSettings,
        Workspace workspace,
        ITextBuffer textBuffer,
        IImportDocumentManager importDocumentManager)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _joinableTaskContext = joinableTaskContext ?? throw new ArgumentNullException(nameof(joinableTaskContext));

        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException(SR.ArgumentCannotBeNullOrEmpty, nameof(filePath));
        }

        _filePath = filePath;
        _projectPath = projectPath ?? throw new ArgumentNullException(nameof(projectPath));
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        _workspaceEditorSettings = workspaceEditorSettings ?? throw new ArgumentNullException(nameof(workspaceEditorSettings));
        _textBuffer = textBuffer ?? throw new ArgumentNullException(nameof(textBuffer));
        _importDocumentManager = importDocumentManager ?? throw new ArgumentNullException(nameof(importDocumentManager));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace)); // For now we assume that the workspace is the always default VS workspace.

        _textViews = new List<ITextView>();
    }

    public override RazorConfiguration? Configuration => _projectSnapshot?.Configuration;

    public override ClientSpaceSettings EditorSettings => _workspaceEditorSettings.Current.ClientSpaceSettings;

    public override ImmutableArray<TagHelperDescriptor> TagHelpers => ProjectSnapshot?.TagHelpers ?? ImmutableArray<TagHelperDescriptor>.Empty;

    public override bool IsSupportedProject => _isSupportedProject;

    internal override IProjectSnapshot? ProjectSnapshot => _projectSnapshot;

    public override ITextBuffer TextBuffer => _textBuffer;

    public override IReadOnlyList<ITextView> TextViews => _textViews;

    public override Workspace Workspace => _workspace;

    public override string FilePath => _filePath;

    public override string ProjectPath => _projectPath;

    internal void AddTextView(ITextView textView)
    {
        if (textView is null)
        {
            throw new ArgumentNullException(nameof(textView));
        }

        _joinableTaskContext.AssertUIThread();

        if (!_textViews.Contains(textView))
        {
            _textViews.Add(textView);
        }
    }

    internal void RemoveTextView(ITextView textView)
    {
        if (textView is null)
        {
            throw new ArgumentNullException(nameof(textView));
        }

        _joinableTaskContext.AssertUIThread();

        if (_textViews.Contains(textView))
        {
            _textViews.Remove(textView);
        }
    }

    public override ITextView? GetFocusedTextView()
    {
        _joinableTaskContext.AssertUIThread();

        for (var i = 0; i < TextViews.Count; i++)
        {
            if (TextViews[i].HasAggregateFocus)
            {
                return TextViews[i];
            }
        }

        return null;
    }

    public async ValueTask SubscribeAsync(CancellationToken cancellationToken)
    {
        await _dispatcher.SwitchToAsync(cancellationToken);

        if (_subscribeCount++ > 0)
        {
            return;
        }

        _projectSnapshot = GetOrCreateProject(_projectPath);
        _isSupportedProject = true;

        _projectManager.Changed += ProjectManager_Changed;
        _workspaceEditorSettings.Changed += EditorSettingsManager_Changed;
        _importDocumentManager.Changed += Import_Changed;

        await _importDocumentManager.OnSubscribedAsync(this, cancellationToken);

        OnContextChangedAsync(ContextChangeKind.ProjectChanged).Forget();
    }

    private IProjectSnapshot GetOrCreateProject(string projectPath)
    {
        _dispatcher.AssertDispatcherThread();

        var projectKey = _projectManager.GetAllProjectKeys(projectPath).FirstOrDefault();

        if (_projectManager.GetLoadedProject(projectKey) is not { } project)
        {
            return new EphemeralProjectSnapshot(Workspace.Services, projectPath);
        }

        return project;
    }

    public async ValueTask UnsubscribeAsync(CancellationToken cancellationToken)
    {
        await _dispatcher.SwitchToAsync(cancellationToken);

        if (_subscribeCount == 0 || _subscribeCount-- > 1)
        {
            return;
        }

        await _importDocumentManager.OnUnsubscribedAsync(this, cancellationToken);

        _projectManager.Changed -= ProjectManager_Changed;
        _workspaceEditorSettings.Changed -= EditorSettingsManager_Changed;
        _importDocumentManager.Changed -= Import_Changed;

        // Detached from project.
        _isSupportedProject = false;
        _projectSnapshot = null;

        _ = OnContextChangedAsync(kind: ContextChangeKind.ProjectChanged);
    }

    private async Task OnContextChangedAsync(ContextChangeKind kind)
    {
        await _joinableTaskContext.Factory.SwitchToMainThreadAsync();
        ContextChanged?.Invoke(this, new ContextChangeEventArgs(kind));
    }

    // Internal for testing
    internal void ProjectManager_Changed(object sender, ProjectChangeEventArgs e)
    {
        // Don't do any work if the solution is closing
        if (e.SolutionIsClosing)
        {
            return;
        }

        _dispatcher.AssertDispatcherThread();

        if (_projectPath is not null &&
            string.Equals(_projectPath, e.ProjectFilePath, StringComparison.OrdinalIgnoreCase))
        {
            // This will be the new snapshot unless the project was removed.
            _projectSnapshot = _projectManager.GetLoadedProject(e.ProjectKey);

            switch (e.Kind)
            {
                case ProjectChangeKind.DocumentAdded:
                case ProjectChangeKind.DocumentRemoved:
                case ProjectChangeKind.DocumentChanged:

                    // Nothing to do.
                    break;

                case ProjectChangeKind.ProjectAdded:
                case ProjectChangeKind.ProjectChanged:

                    // Just an update
                    _ = OnContextChangedAsync(ContextChangeKind.ProjectChanged);

                    if (e.Older is null ||
                        !e.Older.TagHelpers.SequenceEqual(e.Newer!.TagHelpers))
                    {
                        _ = OnContextChangedAsync(ContextChangeKind.TagHelpersChanged);
                    }

                    break;

                case ProjectChangeKind.ProjectRemoved:

                    // Fall back to ephemeral project
                    _projectSnapshot = GetOrCreateProject(ProjectPath);
                    _ = OnContextChangedAsync(ContextChangeKind.ProjectChanged);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown ProjectChangeKind {e.Kind}");
            }
        }
    }

    // Internal for testing
    internal void EditorSettingsManager_Changed(object sender, ClientSettingsChangedEventArgs args)
        => _ = OnContextChangedAsync(ContextChangeKind.EditorSettingsChanged);

    // Internal for testing
    internal void Import_Changed(object sender, ImportChangedEventArgs args)
    {
        _dispatcher.AssertDispatcherThread();

        foreach (var path in args.AssociatedDocuments)
        {
            if (string.Equals(_filePath, path, StringComparison.OrdinalIgnoreCase))
            {
                _ = OnContextChangedAsync(ContextChangeKind.ImportsChanged);
                break;
            }
        }
    }
}
