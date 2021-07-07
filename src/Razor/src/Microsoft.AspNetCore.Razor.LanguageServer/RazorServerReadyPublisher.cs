// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorServerReadyPublisher : ProjectSnapshotChangeTrigger
    {
        private readonly SingleThreadedDispatcher _singleThreadedDispatcher;
        private ProjectSnapshotManagerBase _projectManager;
        private readonly ClientNotifierServiceBase _clientNotifierService;
        private bool _hasNotified = false;

        public RazorServerReadyPublisher(
            SingleThreadedDispatcher singleThreadedDispatcher,
            ClientNotifierServiceBase clientNotifierService)
        {
            if (singleThreadedDispatcher is null)
            {
                throw new ArgumentNullException(nameof(singleThreadedDispatcher));
            }

            if (clientNotifierService is null)
            {
                throw new ArgumentNullException(nameof(clientNotifierService));
            }

            _singleThreadedDispatcher = singleThreadedDispatcher;
            _clientNotifierService = clientNotifierService;
        }

        public override void Initialize(ProjectSnapshotManagerBase projectManager)
        {
            if (projectManager == null)
            {
                throw new ArgumentNullException(nameof(projectManager));
            }

            _projectManager = projectManager;

            _projectManager.Changed += ProjectSnapshotManager_Changed;
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void ProjectSnapshotManager_Changed(object sender, ProjectChangeEventArgs args)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            _singleThreadedDispatcher.AssertDispatcherThread();

            var projectSnapshot = args.Newer;
            if (projectSnapshot?.ProjectWorkspaceState != null && !_hasNotified)
            {
                // Un-register this method, we only need to send this once.
                _projectManager.Changed -= ProjectSnapshotManager_Changed;

                var response = await _clientNotifierService.SendRequestAsync(LanguageServerConstants.RazorServerReadyEndpoint);
                await response.ReturningVoid(CancellationToken.None);

                _hasNotified = true;
            }
        }
    }
}
