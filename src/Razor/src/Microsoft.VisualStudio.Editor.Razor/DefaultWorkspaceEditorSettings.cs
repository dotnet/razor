// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Editor;

namespace Microsoft.VisualStudio.Editor.Razor
{
    internal class DefaultWorkspaceEditorSettings : WorkspaceEditorSettings
    {
        private readonly EditorSettingsManager _editorSettingsManager;
        private readonly EventHandler<EditorSettingsChangedEventArgs> _onChanged;
        private EventHandler<EditorSettingsChangedEventArgs> _changed;
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private int _listenerCount = 0;

        public DefaultWorkspaceEditorSettings(ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher, EditorSettingsManager editorSettingsManager)
        {
            if (projectSnapshotManagerDispatcher is null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (editorSettingsManager is null)
            {
                throw new ArgumentNullException(nameof(editorSettingsManager));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _editorSettingsManager = editorSettingsManager;
            _onChanged = OnChanged;
        }

        public override event EventHandler<EditorSettingsChangedEventArgs> Changed
        {
            add
            {
                _projectSnapshotManagerDispatcher.AssertDispatcherThread();

                _listenerCount++;
                _changed += value;

                if (_listenerCount == 1)
                {
                    // We bind to the editor settings manager only when we have listeners to avoid leaking memory.
                    // Basically we're relying on anyone listening to us to have an understanding of when they're going
                    // to be torn down. In Razor's case this will just be the document tracker factory (which does know).
                    AttachToEditorSettingsManager();
                }
            }
            remove
            {
                _projectSnapshotManagerDispatcher.AssertDispatcherThread();

                _listenerCount--;
                _changed -= value;

                if (_listenerCount == 0)
                {
                    // We detach from the editor settings manager when no one is listening to allow us to be garbage
                    // collected in the case that the workspace is tearing down.
                    DetachFromEditorSettingsManager();
                }
            }
        }

        // Internal for testing
        internal virtual void AttachToEditorSettingsManager()
        {
            _editorSettingsManager.Changed += _onChanged;
        }

        // Internal for testing
        internal virtual void DetachFromEditorSettingsManager()
        {
            _editorSettingsManager.Changed -= _onChanged;
        }

        public override EditorSettings Current => _editorSettingsManager.Current;

        // Internal for testing
        internal void OnChanged(object sender, EditorSettingsChangedEventArgs e)
        {
            _projectSnapshotManagerDispatcher.AssertDispatcherThread();

            Debug.Assert(_changed != null, nameof(OnChanged) + " should not be invoked when there are no listeners.");

            var args = new EditorSettingsChangedEventArgs(Current);
            _changed?.Invoke(this, args);
        }
    }
}
