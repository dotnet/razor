// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Diagnostics;
using System.Threading;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor.Documents
{
    // Hooks up the document manager to project snapshot events. The project snapshot manager
    // tracks the existance of projects/files and the the document manager watches for changes.
    //
    // This class forwards notifications in both directions.
    [Export(typeof(ProjectSnapshotChangeTrigger))]
    internal class EditorDocumentManagerListener : ProjectSnapshotChangeTrigger
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly JoinableTaskContext _joinableTaskContext;
        private readonly EventHandler _onChangedOnDisk;
        private readonly EventHandler _onChangedInEditor;
        private readonly EventHandler _onOpened;
        private readonly EventHandler _onClosed;

        private EditorDocumentManager _documentManager;
        private ProjectSnapshotManagerBase _projectManager;

        [ImportingConstructor]
        public EditorDocumentManagerListener(ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher, JoinableTaskContext joinableTaskContext)
        {
            if (projectSnapshotManagerDispatcher is null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (joinableTaskContext is null)
            {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _joinableTaskContext = joinableTaskContext;
            _onChangedOnDisk = Document_ChangedOnDisk;
            _onChangedInEditor = Document_ChangedInEditor;
            _onOpened = Document_Opened;
            _onClosed = Document_Closed;
        }

        // For testing purposes only.
        internal EditorDocumentManagerListener(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            JoinableTaskContext joinableTaskContext,
            EditorDocumentManager documentManager,
            EventHandler onChangedOnDisk,
            EventHandler onChangedInEditor,
            EventHandler onOpened,
            EventHandler onClosed)
        {
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _joinableTaskContext = joinableTaskContext;
            _documentManager = documentManager;
            _onChangedOnDisk = onChangedOnDisk;
            _onChangedInEditor = onChangedInEditor;
            _onOpened = onOpened;
            _onClosed = onClosed;
        }

        // InitializePriority controls when a snapshot change trigger gets initialized. By specifying 100 we're saying we're pretty important and should get initialized before
        // other triggers with lesser priority so we can attach to Changed sooner. We happen to be so important because we control the open/close state of documents. If other triggers
        // depend on a document being open/closed (some do) then we need to ensure we can mark open/closed prior to them running.
        public override int InitializePriority => 100;

        public override void Initialize(ProjectSnapshotManagerBase projectManager)
        {
            if (projectManager is null)
            {
                throw new ArgumentNullException(nameof(projectManager));
            }

            _projectManager = projectManager;
            _documentManager = projectManager.Workspace.Services.GetRequiredService<EditorDocumentManager>();

            _projectManager.Changed += ProjectManager_Changed;
        }

        // Internal for testing.
#pragma warning disable VSTHRD100 // Avoid async void methods
        internal async void ProjectManager_Changed(object sender, ProjectChangeEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
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

                            var key = new DocumentKey(e.ProjectFilePath, e.DocumentFilePath);

                            // GetOrCreateDocument needs to be run on the UI thread
                            await _joinableTaskContext.Factory.SwitchToMainThreadAsync();

                            var document = _documentManager.GetOrCreateDocument(
                                key, _onChangedOnDisk, _onChangedInEditor, _onOpened, _onClosed);
                            if (document.IsOpenInEditor)
                            {
                                _onOpened(document, EventArgs.Empty);
                            }

                            break;
                        }

                    case ProjectChangeKind.DocumentRemoved:
                        {
                            // Need to run this even if the solution is closing because document dispose cleans up file watchers etc.

                            // TryGetDocument and Dispose need to be run on the UI thread
                            await _joinableTaskContext.Factory.SwitchToMainThreadAsync();

                            var documentFound = _documentManager.TryGetDocument(
                                new DocumentKey(e.ProjectFilePath, e.DocumentFilePath), out var document);

                            // This class 'owns' the document entry so it's safe for us to dispose it.
                            if (documentFound)
                            {
                                document.Dispose();
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                Debug.Fail("EditorDocumentManagerListener.ProjectManager_Changed threw exception:" +
                    Environment.NewLine + ex.Message + Environment.NewLine + "Stack trace:" + Environment.NewLine + ex.StackTrace);
            }
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void Document_ChangedOnDisk(object sender, EventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                // This event is called by the EditorDocumentManager, which runs on the UI thread.
                // However, due to accessing the project snapshot manager, we need to switch to
                // running on the project snapshot manager's specialized thread.
                await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
                {
                    var document = (EditorDocument)sender;
                    _projectManager.DocumentChanged(document.ProjectFilePath, document.DocumentFilePath, document.TextLoader);
                }, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.Fail("EditorDocumentManagerListener.Document_ChangedOnDisk threw exception:" +
                    Environment.NewLine + ex.Message + Environment.NewLine + "Stack trace:" + Environment.NewLine + ex.StackTrace);
            }
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void Document_ChangedInEditor(object sender, EventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                // This event is called by the EditorDocumentManager, which runs on the UI thread.
                // However, due to accessing the project snapshot manager, we need to switch to
                // running on the project snapshot manager's specialized thread.
                await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
                {
                    var document = (EditorDocument)sender;
                    _projectManager.DocumentChanged(document.ProjectFilePath, document.DocumentFilePath, document.EditorTextContainer.CurrentText);
                }, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.Fail("EditorDocumentManagerListener.Document_ChangedInEditor threw exception:" +
                    Environment.NewLine + ex.Message + Environment.NewLine + "Stack trace:" + Environment.NewLine + ex.StackTrace);
            }
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void Document_Opened(object sender, EventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                // This event is called by the EditorDocumentManager, which runs on the UI thread.
                // However, due to accessing the project snapshot manager, we need to switch to
                // running on the project snapshot manager's specialized thread.
                await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
                {
                    var document = (EditorDocument)sender;
                    _projectManager.DocumentOpened(document.ProjectFilePath, document.DocumentFilePath, document.EditorTextContainer.CurrentText);
                }, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.Fail("EditorDocumentManagerListener.Document_Opened threw exception:" +
                    Environment.NewLine + ex.Message + Environment.NewLine + "Stack trace:" + Environment.NewLine + ex.StackTrace);
            }
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void Document_Closed(object sender, EventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                // This event is called by the EditorDocumentManager, which runs on the UI thread.
                // However, due to accessing the project snapshot manager, we need to switch to
                // running on the project snapshot manager's specialized thread.
                await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
                {
                    var document = (EditorDocument)sender;
                    _projectManager.DocumentClosed(document.ProjectFilePath, document.DocumentFilePath, document.TextLoader);
                }, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.Fail("EditorDocumentManagerListener.Document_Closed threw exception:" +
                    Environment.NewLine + ex.Message + Environment.NewLine + "Stack trace:" + Environment.NewLine + ex.StackTrace);
            }
        }
    }
}
