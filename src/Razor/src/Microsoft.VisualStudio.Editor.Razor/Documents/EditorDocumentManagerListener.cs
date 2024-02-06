﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor.Documents;

// Hooks up the document manager to project snapshot events. The project snapshot manager
// tracks the existence of projects/files and the the document manager watches for changes.
//
// This class forwards notifications in both directions.
//
// By implementing IPriorityProjectSnapshotChangeTrigger we're saying we're pretty important and should get initialized before
// other triggers with lesser priority so we can attach to Changed sooner. We happen to be so important because we control the
// open/close state of documents. If other triggers depend on a document being open/closed (some do) then we need to ensure we
// can mark open/closed prior to them running.
[Export(typeof(IProjectSnapshotChangeTrigger))]
internal class EditorDocumentManagerListener : IPriorityProjectSnapshotChangeTrigger
{
    private readonly IEditorDocumentManager _documentManager;
    private readonly ProjectSnapshotManagerDispatcher _dispatcher;
    private readonly JoinableTaskContext _joinableTaskContext;
    private readonly ITelemetryReporter _telemetryReporter;
    private readonly EventHandler? _onChangedOnDisk;
    private readonly EventHandler? _onChangedInEditor;
    private readonly EventHandler _onOpened;
    private readonly EventHandler? _onClosed;

    private ProjectSnapshotManagerBase? _projectManager;

    public ProjectSnapshotManagerBase ProjectManager
        => _projectManager ?? throw new InvalidOperationException($"{nameof(ProjectManager)} called before {nameof(Initialize)}");

    [ImportingConstructor]
    public EditorDocumentManagerListener(
        IEditorDocumentManager documentManager,
        ProjectSnapshotManagerDispatcher dispatcher,
        JoinableTaskContext joinableTaskContext,
        ITelemetryReporter telemetryReporter)
    {
        _documentManager = documentManager;
        _dispatcher = dispatcher;
        _joinableTaskContext = joinableTaskContext;
        _telemetryReporter = telemetryReporter;

        _onChangedOnDisk = Document_ChangedOnDisk;
        _onChangedInEditor = Document_ChangedInEditor;
        _onOpened = Document_Opened;
        _onClosed = Document_Closed;
    }

    // For testing purposes only.
    internal EditorDocumentManagerListener(
        IEditorDocumentManager documentManager,
        ProjectSnapshotManagerDispatcher dispatcher,
        JoinableTaskContext joinableTaskContext,
        EventHandler? onChangedOnDisk,
        EventHandler? onChangedInEditor,
        EventHandler onOpened,
        EventHandler? onClosed)
    {
        _dispatcher = dispatcher;
        _joinableTaskContext = joinableTaskContext;
        _documentManager = documentManager;
        _onChangedOnDisk = onChangedOnDisk;
        _onChangedInEditor = onChangedInEditor;
        _onOpened = onOpened;
        _onClosed = onClosed;

        _telemetryReporter = null!;
    }

    [MemberNotNull(nameof(_projectManager))]
    public void Initialize(ProjectSnapshotManagerBase projectManager)
    {
        _projectManager = projectManager;
        _projectManager.Changed += ProjectManager_Changed;
    }

    private void ProjectManager_Changed(object? sender, ProjectChangeEventArgs e)
    {
        ProjectManager_ChangedAsync(e, CancellationToken.None).Forget();
    }

    // Internal for testing.
    internal async Task ProjectManager_ChangedAsync(ProjectChangeEventArgs e, CancellationToken cancellationToken)
    {
        try
        {
            switch (e.Kind)
            {
                case ProjectChangeKind.DocumentAdded:
                    {
                        // Don't do any work if the solution is closing
                        if (e.SolutionIsClosing)
                        {
                            return;
                        }

                        var key = new DocumentKey(e.ProjectKey, e.DocumentFilePath.AssumeNotNull());

                        // GetOrCreateDocument needs to be run on the UI thread
                        await _joinableTaskContext.Factory.SwitchToMainThreadAsync(cancellationToken);

                        var document = _documentManager.GetOrCreateDocument(
                            key, e.ProjectFilePath, e.ProjectKey, _onChangedOnDisk, _onChangedInEditor, _onOpened, _onClosed);
                        if (document.IsOpenInEditor)
                        {
                            _onOpened(document, EventArgs.Empty);
                        }

                        break;
                    }

                case ProjectChangeKind.DocumentRemoved:
                    {
                        await _joinableTaskContext.Factory.SwitchToMainThreadAsync(cancellationToken);

                        RemoveAndDisposeDocument_RunOnUIThread(e.ProjectKey, e.DocumentFilePath.AssumeNotNull());

                        break;
                    }

                case ProjectChangeKind.ProjectRemoved:
                    {
                        await _joinableTaskContext.Factory.SwitchToMainThreadAsync(cancellationToken);

                        foreach (var documentFilePath in e.Older.AssumeNotNull().DocumentFilePaths)
                        {
                            RemoveAndDisposeDocument_RunOnUIThread(e.ProjectKey, documentFilePath.AssumeNotNull());
                        }

                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            Debug.Fail($"""
                EditorDocumentManagerListener.ProjectManager_Changed threw exception:
                {ex.Message}
                Stack trace:
                {ex.StackTrace}
                """);
        }

        void RemoveAndDisposeDocument_RunOnUIThread(ProjectKey projectKey, string documentFilePath)
        {
            if (_documentManager.TryGetDocument(new DocumentKey(projectKey, documentFilePath), out var document))
            {
                // This class 'owns' the document entry so it's safe for us to dispose it.
                document.Dispose();
            }
        }
    }

    private void Document_ChangedOnDisk(object sender, EventArgs e)
    {
        Document_ChangedOnDiskAsync((EditorDocument)sender, CancellationToken.None).Forget();
    }

    private async Task Document_ChangedOnDiskAsync(EditorDocument document, CancellationToken cancellationToken)
    {
        try
        {
            // This event is called by the EditorDocumentManager, which runs on the UI thread.
            // However, due to accessing the project snapshot manager, we need to switch to
            // running on the project snapshot manager's specialized thread.
            await _dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                ProjectManager.DocumentChanged(document.ProjectKey, document.DocumentFilePath, document.TextLoader);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.Fail($"""
                EditorDocumentManagerListener.Document_ChangedOnDisk threw exception:
                {ex.Message}
                Stack trace:
                {ex.StackTrace}
                """);
        }
    }

    private void Document_ChangedInEditor(object sender, EventArgs e)
    {
        _ = Document_ChangedInEditorAsync(sender, CancellationToken.None);
    }

    private async Task Document_ChangedInEditorAsync(object sender, CancellationToken cancellationToken)
    {
        try
        {
            // This event is called by the EditorDocumentManager, which runs on the UI thread.
            // However, due to accessing the project snapshot manager, we need to switch to
            // running on the project snapshot manager's specialized thread.
            await _dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                var document = (EditorDocument)sender;
                ProjectManager.DocumentChanged(document.ProjectKey, document.DocumentFilePath, document.EditorTextContainer!.CurrentText);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.Fail($"""
                EditorDocumentManagerListener.Document_ChangedInEditor threw exception:
                {ex.Message}
                Stack trace:
                {ex.StackTrace}
                """);
        }
    }

    private void Document_Opened(object sender, EventArgs e)
    {
        // Don't use JoinableTaskFactory here, the double Thread-switching causes a hang.
        _ = Document_OpenedAsync(sender, CancellationToken.None);
    }

    private async Task Document_OpenedAsync(object sender, CancellationToken cancellationToken)
    {
        try
        {
            // This event is called by the EditorDocumentManager, which runs on the UI thread.
            // However, due to accessing the project snapshot manager, we need to switch to
            // running on the project snapshot manager's specialized thread.
            await _dispatcher.RunOnDispatcherThreadAsync(async () =>
            {
                var document = (EditorDocument)sender;

                if (ProjectManager.TryGetLoadedProject(document.ProjectKey, out var project) &&
                    project is ProjectSnapshot { HostProject: FallbackHostProject } projectSnapshot)
                {
                    // The user is opening a document that is part of a fallback project. This is a scenario we are very interested in knowing more about
                    // so fire some telemetry. We can't log details about the project, for PII reasons, but we can use document count and tag helper count
                    // as some kind of measure of complexity.
                    var tagHelpers = await project.GetTagHelpersAsync(cancellationToken).ConfigureAwait(false);
                    _telemetryReporter.ReportEvent(
                        "fallbackproject/documentopen",
                        Severity.Normal,
                        new Property("document.count", projectSnapshot.DocumentCount),
                        new Property("taghelper.count", tagHelpers.Length));
                }

                ProjectManager.DocumentOpened(document.ProjectKey, document.DocumentFilePath, document.EditorTextContainer!.CurrentText);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.Fail($"""
                EditorDocumentManagerListener.Document_Opened threw exception:
                {ex.Message}
                Stack trace:
                {ex.StackTrace}
                """);
        }
    }

    private void Document_Closed(object sender, EventArgs e)
    {
        _ = Document_ClosedAsync(sender, CancellationToken.None);
    }

    private async Task Document_ClosedAsync(object sender, CancellationToken cancellationToken)
    {
        try
        {
            // This event is called by the EditorDocumentManager, which runs on the UI thread.
            // However, due to accessing the project snapshot manager, we need to switch to
            // running on the project snapshot manager's specialized thread.
            await _dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                var document = (EditorDocument)sender;
                ProjectManager.DocumentClosed(document.ProjectKey, document.DocumentFilePath, document.TextLoader);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.Fail($"""
                EditorDocumentManagerListener.Document_Closed threw exception:
                {ex.Message}
                Stack trace:
                {ex.StackTrace}
                """);
        }
    }
}
