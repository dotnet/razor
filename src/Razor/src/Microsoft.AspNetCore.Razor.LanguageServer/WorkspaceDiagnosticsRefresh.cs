// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class WorkspaceDiagnosticsRefresh : IRazorStartupService
{
    private readonly object _gate = new();
    private readonly IClientCapabilitiesService _clientCapabilitiesService;
    private readonly IClientConnection _clientConnection;
    private bool _refreshQueued;
    private Task? _refreshTask;

    private static readonly TimeSpan s_delay = TimeSpan.FromMilliseconds(200);

    public WorkspaceDiagnosticsRefresh(
        IProjectSnapshotManager projectSnapshotManager,
        IClientCapabilitiesService clientCapabilitiesService,
        IClientConnection clientConnection)
    {
        projectSnapshotManager.Changed += ProjectSnapshotManager_Changed;
        _clientCapabilitiesService = clientCapabilitiesService;
        _clientConnection = clientConnection;
    }

    private void ProjectSnapshotManager_Changed(object? sender, ProjectChangeEventArgs e)
    {
        if (e.SolutionIsClosing)
        {
            return;
        }

        if (e.Kind is not ProjectChangeKind.DocumentChanged)
        {
            QueueRefresh();
        }
    }

    private void QueueRefresh()
    {
        lock(_gate)
        {
            if (_refreshQueued)
            {
                return;
            }

            if (!_clientCapabilitiesService.CanGetClientCapabilities)
            {
                return;
            }

            var supported = _clientCapabilitiesService.ClientCapabilities.Workspace?.Diagnostics?.RefreshSupport;
            if (supported != true)
            {
                return;
            }

            _refreshQueued = true;
            _refreshTask = RefreshAfterDelayAsync();
        }
    }

    private async Task RefreshAfterDelayAsync()
    {
        await Task.Delay(s_delay).ConfigureAwait(false);

        _clientConnection
            .SendNotificationAsync(Methods.WorkspaceDiagnosticRefreshName, default)
            .Forget();

        _refreshQueued = false;
    }

    public TestAccessor GetTestAccessor()
        => new(this);

    public class TestAccessor
    {
        private readonly WorkspaceDiagnosticsRefresh _instance;

        public TestAccessor(WorkspaceDiagnosticsRefresh instance)
        {
            _instance = instance;
        }

        public Task WaitForRefreshAsync()
        {
            return _instance._refreshTask ?? Task.CompletedTask;
        }
    }
}
