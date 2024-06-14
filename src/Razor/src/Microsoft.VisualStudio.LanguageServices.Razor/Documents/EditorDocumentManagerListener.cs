﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Razor.ProjectSystem;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor.Documents;

// Hooks up the document manager to project snapshot events. The project snapshot manager
// tracks the existence of projects/files and the the document manager watches for changes.
//
// This class forwards notifications in both directions.
//
// By implementing IPriorityProjectSnapshotChangeTrigger we're saying we're pretty important and should get initialized before
// other triggers with lesser priority so we can attach to Changed sooner. We happen to be so important because we control the
// open/close state of documents. If other triggers depend on a document being open/closed (some do) then we need to ensure we
// can mark open/closed prior to them running.
[Export(typeof(IRazorStartupService))]
internal partial class EditorDocumentManagerListener : IRazorStartupService, IDisposable
{
    private abstract record Work(ProjectKey ProjectKey);
    private sealed record DocumentAdded(ProjectKey ProjectKey, string DocumentFilePath, string ProjectFilePath) : Work(ProjectKey);
    private sealed record DocumentRemoved(ProjectKey ProjectKey, string DocumentFilePath) : Work(ProjectKey);
    private sealed record ProjectRemoved(ProjectKey ProjectKey, IEnumerable<string> DocumentFilePaths) : Work(ProjectKey);

    private static readonly TimeSpan s_delay = TimeSpan.FromMilliseconds(10);

    private readonly IEditorDocumentManager _documentManager;
    private readonly IProjectSnapshotManager _projectManager;
    private readonly JoinableTaskContext _joinableTaskContext;
    private readonly ITelemetryReporter _telemetryReporter;

    private readonly AsyncBatchingWorkQueue<Work> _workQueue;
    private readonly CancellationTokenSource _disposeTokenSource;

    private EventHandler? _onChangedOnDisk;
    private EventHandler? _onChangedInEditor;
    private EventHandler? _onOpened;
    private EventHandler? _onClosed;

    [ImportingConstructor]
    public EditorDocumentManagerListener(
        IEditorDocumentManager documentManager,
        IProjectSnapshotManager projectManager,
        JoinableTaskContext joinableTaskContext,
        ITelemetryReporter telemetryReporter)
    {
        _documentManager = documentManager;
        _projectManager = projectManager;
        _joinableTaskContext = joinableTaskContext;
        _telemetryReporter = telemetryReporter;

        _disposeTokenSource = new();
        _workQueue = new AsyncBatchingWorkQueue<Work>(s_delay, ProcessBatchAsync, _disposeTokenSource.Token);

        _onChangedOnDisk = Document_ChangedOnDisk;
        _onChangedInEditor = Document_ChangedInEditor;
        _onOpened = Document_Opened;
        _onClosed = Document_Closed;

        _projectManager.PriorityChanged += ProjectManager_Changed;
    }

    public void Dispose()
    {
        if (_disposeTokenSource.IsCancellationRequested)
        {
            return;
        }

        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
    }

    private void ProjectManager_Changed(object? sender, ProjectChangeEventArgs e)
    {
        Work? work = e.Kind switch
        {
            ProjectChangeKind.DocumentAdded => new DocumentAdded(e.ProjectKey, e.DocumentFilePath.AssumeNotNull(), e.ProjectFilePath),
            ProjectChangeKind.DocumentRemoved => new DocumentRemoved(e.ProjectKey, e.DocumentFilePath.AssumeNotNull()),
            ProjectChangeKind.ProjectRemoved => new ProjectRemoved(e.ProjectKey, e.Older.AssumeNotNull().DocumentFilePaths),
            _ => null
        };

        if (work is null)
        {
            return;
        }

        // Don't do any work if the solution is closing
        if (work is DocumentAdded && e.SolutionIsClosing)
        {
            return;
        }

        _workQueue.AddWork(work);
    }

    private async ValueTask ProcessBatchAsync(ImmutableArray<Work> items, CancellationToken cancellationToken)
    {
        // GetOrCreateDocument needs to be run on the UI thread
        await _joinableTaskContext.Factory.SwitchToMainThreadAsync(cancellationToken);

        foreach (var work in items)
        {
            try
            {
                switch (work)
                {
                    case DocumentAdded e:
                        {
                            var key = new DocumentKey(e.ProjectKey, e.DocumentFilePath);

                            var document = _documentManager.GetOrCreateDocument(
                                key, e.ProjectFilePath, e.ProjectKey, _onChangedOnDisk, _onChangedInEditor, _onOpened, _onClosed);
                            if (document.IsOpenInEditor)
                            {
                                _onOpened?.Invoke(document, EventArgs.Empty);
                            }

                            break;
                        }

                    case DocumentRemoved e:
                        {
                            RemoveAndDisposeDocument_RunOnUIThread(e.ProjectKey, e.DocumentFilePath);

                            break;
                        }

                    case ProjectRemoved e:
                        {
                            foreach (var documentFilePath in e.DocumentFilePaths)
                            {
                                RemoveAndDisposeDocument_RunOnUIThread(e.ProjectKey, documentFilePath);
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

    private Task Document_ChangedOnDiskAsync(EditorDocument document, CancellationToken cancellationToken)
    {
        try
        {
            return _projectManager.UpdateAsync(
                static (updater, document) => updater.DocumentChanged(document.ProjectKey, document.DocumentFilePath, document.TextLoader),
                state: document,
                cancellationToken);
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

        return Task.CompletedTask;
    }

    private void Document_ChangedInEditor(object sender, EventArgs e)
    {
        Document_ChangedInEditorAsync(sender, CancellationToken.None).Forget();
    }

    private Task Document_ChangedInEditorAsync(object sender, CancellationToken cancellationToken)
    {
        try
        {
            // This event is called by the EditorDocumentManager, which runs on the UI thread.
            // However, due to accessing the project snapshot manager, we need to switch to
            // running on the project snapshot manager's specialized thread.
            return _projectManager.UpdateAsync(
                static (updater, document) => updater.DocumentChanged(document.ProjectKey, document.DocumentFilePath, document.EditorTextContainer!.CurrentText),
                state: (EditorDocument)sender,
                cancellationToken);
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

        return Task.CompletedTask;
    }

    private void Document_Opened(object sender, EventArgs e)
    {
        Document_OpenedAsync(sender, CancellationToken.None).Forget();
    }

    private Task Document_OpenedAsync(object sender, CancellationToken cancellationToken)
    {
        try
        {
            return _projectManager.UpdateAsync(
                static async (updater, state) =>
                {
                    var (document, telemetryReporter, cancellationToken) = state;

                    if (updater.TryGetLoadedProject(document.ProjectKey, out var project) &&
                        project is ProjectSnapshot { HostProject: FallbackHostProject } projectSnapshot)
                    {
                        // The user is opening a document that is part of a fallback project. This is a scenario we are very interested in knowing more about
                        // so fire some telemetry. We can't log details about the project, for PII reasons, but we can use document count and tag helper count
                        // as some kind of measure of complexity.
                        var tagHelpers = await project.GetTagHelpersAsync(cancellationToken).ConfigureAwait(false);
                        telemetryReporter.ReportEvent(
                            "fallbackproject/documentopen",
                            Severity.Normal,
                            new Property("document.count", projectSnapshot.DocumentCount),
                            new Property("taghelper.count", tagHelpers.Length));
                    }

                    updater.DocumentOpened(document.ProjectKey, document.DocumentFilePath, document.EditorTextContainer!.CurrentText);
                },
                state: (document: (EditorDocument)sender, _telemetryReporter, cancellationToken),
                cancellationToken);
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

        return Task.CompletedTask;
    }

    private void Document_Closed(object sender, EventArgs e)
    {
        Document_ClosedAsync(sender, CancellationToken.None).Forget();
    }

    private Task Document_ClosedAsync(object sender, CancellationToken cancellationToken)
    {
        try
        {
            return _projectManager.UpdateAsync(
                static (updater, document) => updater.DocumentClosed(document.ProjectKey, document.DocumentFilePath, document.TextLoader),
                state: (EditorDocument)sender,
                cancellationToken);
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

        return Task.CompletedTask;
    }
}
