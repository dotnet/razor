// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class WorkspaceSemanticTokensRefreshNotifier : IWorkspaceSemanticTokensRefreshNotifier, IDisposable
{
    private static readonly TimeSpan s_delay = TimeSpan.FromMilliseconds(250);

    private readonly IClientCapabilitiesService _clientCapabilitiesService;
    private readonly IClientConnection _clientConnection;
    private readonly CancellationTokenSource _disposeTokenSource;
    private readonly IDisposable _optionsChangeListener;

    private readonly AsyncBatchingWorkQueue _queue;
    private Task _refreshTask = Task.CompletedTask;

    private bool _isColoringBackground;
    private bool? _supportsRefresh;

    public WorkspaceSemanticTokensRefreshNotifier(
        IClientCapabilitiesService clientCapabilitiesService,
        IClientConnection clientConnection,
        RazorLSPOptionsMonitor optionsMonitor)
    {
        _clientCapabilitiesService = clientCapabilitiesService;
        _clientConnection = clientConnection;

        _disposeTokenSource = new();

        _queue = new(
            TimeSpan.FromMilliseconds(250),
            ProcessBatchAsync,
            _disposeTokenSource.Token);

        _isColoringBackground = optionsMonitor.CurrentValue.ColorBackground;
        _optionsChangeListener = optionsMonitor.OnChange(HandleOptionsChange);
    }

    private ValueTask ProcessBatchAsync(CancellationToken token)
    {
        _clientConnection
            .SendNotificationAsync(Methods.WorkspaceSemanticTokensRefreshName, _disposeTokenSource.Token)
            .Forget();

        return new(Task.CompletedTask);
    }

    public void Dispose()
    {
        if (_disposeTokenSource.IsCancellationRequested)
        {
            return;
        }

        _optionsChangeListener.Dispose();

        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
    }

    private void HandleOptionsChange(RazorLSPOptions options)
    {
        if (options.ColorBackground != _isColoringBackground)
        {
            _isColoringBackground = options.ColorBackground;
            NotifyWorkspaceSemanticTokensRefresh();
        }
    }

    public void NotifyWorkspaceSemanticTokensRefresh()
    {
        if (_disposeTokenSource.IsCancellationRequested)
        {
            return;
        }

        // We could have been called before the LSP server has even been initialized
        if (!_clientCapabilitiesService.CanGetClientCapabilities)
        {
            return;
        }

        _supportsRefresh ??= _clientCapabilitiesService.ClientCapabilities.Workspace?.SemanticTokens?.RefreshSupport ?? false;

        if (_supportsRefresh is false)
        {
            return;
        }

        _queue.AddWork();
    }

    internal TestAccessor GetTestAccessor()
        => new(this);

    internal class TestAccessor(WorkspaceSemanticTokensRefreshNotifier instance)
    {
        public Task WaitForNotificationAsync()
        {
            return instance._queue.WaitUntilCurrentBatchCompletesAsync();
        }
    }
}
