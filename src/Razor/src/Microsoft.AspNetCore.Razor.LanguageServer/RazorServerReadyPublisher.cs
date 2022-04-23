// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorServerReadyPublisher : ProjectSnapshotChangeTrigger
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private ProjectSnapshotManagerBase? _projectManager;
        private readonly ClientNotifierServiceBase _clientNotifierService;
        private bool _hasNotified = false;

        public RazorServerReadyPublisher(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher!!,
            ClientNotifierServiceBase clientNotifierService!!)
        {
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _clientNotifierService = clientNotifierService;
        }

        public override void Initialize(ProjectSnapshotManagerBase projectManager!!)
        {
            _projectManager = projectManager;

            _projectManager.Changed += ProjectSnapshotManager_Changed;
        }

        private void ProjectSnapshotManager_Changed(object sender, ProjectChangeEventArgs args)
        {
            _ = ProjectSnapshotManager_ChangedAsync(args, CancellationToken.None);
        }

        private async Task ProjectSnapshotManager_ChangedAsync(ProjectChangeEventArgs args, CancellationToken cancellationToken)
        {
            try
            {
                // Don't do any work if the solution is closing
                if (args.SolutionIsClosing)
                {
                    return;
                }

                _projectSnapshotManagerDispatcher.AssertDispatcherThread();

                var projectSnapshot = args.Newer;
                if (projectSnapshot?.ProjectWorkspaceState != null && !_hasNotified)
                {
                    // Un-register this method, we only need to send this once.
                    _projectManager!.Changed -= ProjectSnapshotManager_Changed;
                    var response = await _clientNotifierService.SendRequestAsync(LanguageServerConstants.RazorServerReadyEndpoint);
                    await response.ReturningVoid(cancellationToken);

                    _hasNotified = true;
                }
            }
            catch (Exception ex)
            {
                _projectManager?.ReportError(ex);
            }
        }
    }
}
