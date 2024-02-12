﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.LiveShare.Razor.Host;

internal class ProjectSnapshotManagerProxy : IProjectSnapshotManagerProxy, ICollaborationService, IDisposable
{
    private readonly CollaborationSession _session;
    private readonly ProjectSnapshotManagerDispatcher _dispatcher;
    private readonly IProjectSnapshotManager _projectSnapshotManager;
    private readonly JoinableTaskFactory _jtf;
    private readonly AsyncSemaphore _latestStateSemaphore;
    private bool _disposed;
    private ProjectSnapshotManagerProxyState? _latestState;

    private JoinableTask? _processingChangedEventTestTask;

    public ProjectSnapshotManagerProxy(
        CollaborationSession session,
        IProjectSnapshotManager projectSnapshotManager,
        ProjectSnapshotManagerDispatcher dispatcher,
        JoinableTaskFactory jtf)
    {
        _session = session;
        _dispatcher = dispatcher;
        _projectSnapshotManager = projectSnapshotManager;
        _jtf = jtf;

        _latestStateSemaphore = new AsyncSemaphore(initialCount: 1);
        _projectSnapshotManager.Changed += ProjectSnapshotManager_Changed;
    }

    public event EventHandler<ProjectChangeEventProxyArgs>? Changed;

    public async Task<ProjectSnapshotManagerProxyState> GetProjectManagerStateAsync(CancellationToken cancellationToken)
    {
        using (await _latestStateSemaphore.EnterAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_latestState is not null)
            {
                return _latestState;
            }
        }

        var projects = await GetLatestProjectsAsync();
        var state = await CalculateUpdatedStateAsync(projects);

        return state;
    }

    public void Dispose()
    {
        _projectSnapshotManager.Changed -= ProjectSnapshotManager_Changed;
        _latestStateSemaphore.Dispose();
        _disposed = true;
    }

    // Internal for testing
    internal async Task<IReadOnlyList<IProjectSnapshot>> GetLatestProjectsAsync()
    {
        if (!_jtf.Context.IsOnMainThread)
        {
            await _jtf.SwitchToMainThreadAsync(CancellationToken.None);
        }

        return _projectSnapshotManager.GetProjects();
    }

    // Internal for testing
    internal async Task<ProjectSnapshotManagerProxyState> CalculateUpdatedStateAsync(IReadOnlyList<IProjectSnapshot> projects)
    {
        using (await _latestStateSemaphore.EnterAsync().ConfigureAwait(false))
        {
            var projectHandles = new List<ProjectSnapshotHandleProxy>();
            foreach (var project in projects)
            {
                var projectHandleProxy = await ConvertToProxyAsync(project).ConfigureAwait(false);
                projectHandles.Add(projectHandleProxy.AssumeNotNull());
            }

            _latestState = new ProjectSnapshotManagerProxyState(projectHandles);
            return _latestState;
        }
    }

    private async Task<ProjectSnapshotHandleProxy?> ConvertToProxyAsync(IProjectSnapshot? project)
    {
        if (project is null)
        {
            return null;
        }

        var tagHelpers = await project.GetTagHelpersAsync(CancellationToken.None).ConfigureAwait(false);
        var projectWorkspaceState = ProjectWorkspaceState.Create(tagHelpers, project.CSharpLanguageVersion);
        var projectFilePath = _session.ConvertLocalPathToSharedUri(project.FilePath);
        var intermediateOutputPath = _session.ConvertLocalPathToSharedUri(project.IntermediateOutputPath);
        var projectHandleProxy = new ProjectSnapshotHandleProxy(projectFilePath, intermediateOutputPath, project.Configuration, project.RootNamespace, projectWorkspaceState);
        return projectHandleProxy;
    }

    private void ProjectSnapshotManager_Changed(object sender, ProjectChangeEventArgs args)
    {
        _dispatcher.AssertDispatcherThread();

        if (_disposed)
        {
            return;
        }

        if (args.Kind == ProjectChangeKind.DocumentAdded ||
            args.Kind == ProjectChangeKind.DocumentRemoved ||
            args.Kind == ProjectChangeKind.DocumentChanged)
        {
            // Razor LiveShare doesn't currently support document based notifications over the wire.
            return;
        }

        _processingChangedEventTestTask = _jtf.RunAsync(async () =>
        {
            var projects = await GetLatestProjectsAsync();

            await _jtf.SwitchToMainThreadAsync();

            var oldProjectProxy = await ConvertToProxyAsync(args.Older).ConfigureAwait(false);
            var newProjectProxy = await ConvertToProxyAsync(args.Newer).ConfigureAwait(false);
            var remoteProjectChangeArgs = new ProjectChangeEventProxyArgs(oldProjectProxy, newProjectProxy, (ProjectProxyChangeKind)args.Kind);

            OnChanged(remoteProjectChangeArgs);
        });
    }

    private void OnChanged(ProjectChangeEventProxyArgs args)
    {
        _dispatcher.AssertDispatcherThread();

        if (_disposed)
        {
            return;
        }

        Changed?.Invoke(this, args);
    }

    internal TestAccessor GetTestAccessor()
        => new(this);

    internal sealed class TestAccessor(ProjectSnapshotManagerProxy instance)
    {
        public JoinableTask? ProcessingChangedEventTestTask => instance._processingChangedEventTestTask;
    }
}
