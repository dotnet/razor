// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    /// <summary>
    /// Sends a 'workspace\semanticTokens\refresh' request each time the project changes.
    /// </summary>
    internal class DefaultWorkspaceChangedRefresh : ProjectSnapshotChangeTrigger
    {
        private readonly WorkspaceChangedPublisher _workspaceChangedPublisher;
        private ProjectSnapshotManagerBase? _projectSnapshotManager;

        internal DefaultWorkspaceChangedRefresh(WorkspaceChangedPublisher workspaceChangedPublisher!!)
        {
            _workspaceChangedPublisher = workspaceChangedPublisher;
        }

        private ProjectSnapshotManagerBase ProjectSnapshotManager
        {
            get
            {
                if (_projectSnapshotManager is null)
                {
                    throw new InvalidOperationException("ProjectSnapshotManager accessed before Initialized was called.");
                }

                return _projectSnapshotManager;
            }
        }

        public override void Initialize(ProjectSnapshotManagerBase projectManager)
        {
            _projectSnapshotManager = projectManager;
            ProjectSnapshotManager.Changed += ProjectSnapshotManager_Changed;
        }

        // Does not handle C# files
#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void ProjectSnapshotManager_Changed(object sender, ProjectChangeEventArgs? args)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                await _workspaceChangedPublisher.PublishWorkspaceChangedAsync(CancellationToken.None);
            }
            catch (Exception)
            {
                // Catch all exceptions to prevent crashing the process.
            }
        }
    }
}
