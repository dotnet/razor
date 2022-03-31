// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    /// <summary>
    /// Sends a 'workspace\semanticTokens\refresh' request each time the project changes.
    /// </summary>
    internal class DefaultWorkspaceSemanticTokensRefreshTrigger : ProjectSnapshotChangeTrigger
    {
        private WorkspaceSemanticTokensRefreshPublisher? _workspaceChangedPublisher;
        private ProjectSnapshotManagerBase? _projectSnapshotManager;
        private readonly IClientLanguageServer _clientLanguageServer;

        internal DefaultWorkspaceSemanticTokensRefreshTrigger(IClientLanguageServer clientLanguageServer!!)
        {
            _clientLanguageServer = clientLanguageServer;
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

        private WorkspaceSemanticTokensRefreshPublisher WorkspaceChangedPublisher
        {
            get
            {
                if (_workspaceChangedPublisher is null)
                {
                    throw new InvalidOperationException($"{WorkspaceChangedPublisher} accessed before Initialized was called.");
                }

                return _workspaceChangedPublisher;
            }
        }

        public override void Initialize(ProjectSnapshotManagerBase projectManager)
        {
            _projectSnapshotManager = projectManager;
            _workspaceChangedPublisher = GetWorkspaceSemanticTokensRefreshPublisher(_projectSnapshotManager);

            ProjectSnapshotManager.Changed += ProjectSnapshotManager_Changed;
        }

        internal virtual WorkspaceSemanticTokensRefreshPublisher GetWorkspaceSemanticTokensRefreshPublisher(ProjectSnapshotManagerBase projectManager)
        {
            var errorReporter = projectManager.Workspace.Services.GetRequiredService<ErrorReporter>();
            return new DefaultWorkspaceSemanticTokensRefreshPublisher(_clientLanguageServer, errorReporter);
        }

        // Does not handle C# files
        private void ProjectSnapshotManager_Changed(object sender, ProjectChangeEventArgs args)
        {
            // Don't send for a simple Document edit. The platform should re-request any range that
            // is edited and if a parameter or type change is made it should be reflected as a ProjectChanged.
            if (args.Kind != ProjectChangeKind.DocumentChanged)
            {
                WorkspaceChangedPublisher.PublishWorkspaceSemanticTokensRefresh();
            }
        }
    }
}
