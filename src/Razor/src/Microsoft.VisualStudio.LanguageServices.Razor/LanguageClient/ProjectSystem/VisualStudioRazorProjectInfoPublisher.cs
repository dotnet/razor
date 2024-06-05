// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor.LanguageClient.ProjectSystem;

[Export(typeof(IRazorProjectInfoPublisher))]
internal sealed partial class VisualStudioRazorProjectInfoPublisher : IRazorProjectInfoPublisher, IDisposable
{
    private abstract record Work(ProjectKey ProjectKey);
    private sealed record Update(RazorProjectInfo ProjectInfo) : Work(ProjectInfo.ProjectKey);
    private sealed record Remove(ProjectKey ProjectKey) : Work(ProjectKey);

    private static readonly TimeSpan s_delay = TimeSpan.FromMilliseconds(250);

    private readonly CancellationTokenSource _disposeTokenSource;
    private readonly AsyncBatchingWorkQueue<Work> _workQueue;

    private readonly Dictionary<ProjectKey, RazorProjectInfo> _latestProjectInfo;
    private ImmutableArray<IRazorProjectInfoListener> _listeners;

    private readonly JoinableTask _initializeTask;

    [ImportingConstructor]
    public VisualStudioRazorProjectInfoPublisher(
        IProjectSnapshotManager projectManager,
        LSPEditorFeatureDetector lspEditorFeatureDetector,
        JoinableTaskContext joinableTaskContext)
        : this(projectManager, lspEditorFeatureDetector, joinableTaskContext, s_delay)
    {
    }

    public VisualStudioRazorProjectInfoPublisher(
        IProjectSnapshotManager projectManager,
        LSPEditorFeatureDetector lspEditorFeatureDetector,
        JoinableTaskContext joinableTaskContext,
        TimeSpan delay)
    {
        _disposeTokenSource = new();
        _workQueue = new AsyncBatchingWorkQueue<Work>(delay, ProcessBatchAsync, _disposeTokenSource.Token);
        _latestProjectInfo = [];
        _listeners = [];

        var jtf = joinableTaskContext.Factory;

        _initializeTask = jtf.RunAsync(async () =>
        {
            // Switch to the main thread because IsLSPEditorAvailable() expects to.
            await jtf.SwitchToMainThreadAsync();

            // Because this service is only consumed by the language server, we only initialize it
            // when the LSP editor is available.
            if (lspEditorFeatureDetector.IsLSPEditorAvailable())
            {
                await InitializeAsync(projectManager, _disposeTokenSource.Token);
            }
        });

    }

    public void Dispose()
    {
        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
    }

    private async Task InitializeAsync(IProjectSnapshotManager projectManager, CancellationToken cancellationToken)
    {
        // Even though we aren't mutating the project snapshot manager, we call UpdateAsync(...) here to ensure
        // that we run on its dispatcher. That ensures that no changes will code in while we are iterating the
        // current set of projects.
        await projectManager.UpdateAsync(updater =>
        {
            foreach (var project in updater.GetProjects())
            {
                EnqueueUpdate(project.ToRazorProjectInfo());
            }

            projectManager.Changed += ProjectManager_Changed;
        },
        cancellationToken);
    }

    private void ProjectManager_Changed(object sender, ProjectChangeEventArgs e)
    {
        // Don't do any work if the solution is closing
        if (e.SolutionIsClosing)
        {
            return;
        }

        switch (e.Kind)
        {
            case ProjectChangeKind.ProjectAdded:
            case ProjectChangeKind.ProjectChanged:
            case ProjectChangeKind.DocumentRemoved:
            case ProjectChangeKind.DocumentAdded:
                var newer = e.Newer.AssumeNotNull();
                EnqueueUpdate(newer.ToRazorProjectInfo());
                break;

            case ProjectChangeKind.ProjectRemoved:
                var older = e.Older.AssumeNotNull();
                EnqueueRemove(older.Key);
                break;

            case ProjectChangeKind.DocumentChanged:
                break;

            default:
                throw new NotSupportedException($"Unsupported {nameof(ProjectChangeKind)}: {e.Kind}");
        }
    }

    private async ValueTask ProcessBatchAsync(ImmutableArray<Work> items, CancellationToken token)
    {
        foreach (var work in items.GetMostRecentUniqueItems(Comparer.Instance))
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            lock (_latestProjectInfo)
            {
                switch (work)
                {
                    case Update(var projectInfo):
                        _latestProjectInfo[projectInfo.ProjectKey] = projectInfo;
                        break;

                    case Remove(var projectKey):
                        _latestProjectInfo.Remove(projectKey);
                        break;

                    default:
                        Assumed.Unreachable();
                        break;
                }
            }

            foreach (var listener in _listeners)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                switch (work)
                {
                    case Update(var projectInfo):
                        await listener.UpdatedAsync(projectInfo, token).ConfigureAwait(false);
                        break;

                    case Remove(var projectKey):
                        await listener.RemovedAsync(projectKey, token).ConfigureAwait(false);
                        break;
                }
            }
        }
    }

    private void EnqueueUpdate(RazorProjectInfo projectInfo)
    {
        _workQueue.AddWork(new Update(projectInfo));
    }

    private void EnqueueRemove(ProjectKey projectKey)
    {
        _workQueue.AddWork(new Remove(projectKey));
    }

    public ImmutableArray<RazorProjectInfo> GetLatestProjectInfos()
    {
        lock (_latestProjectInfo)
        {
            using var builder = new PooledArrayBuilder<RazorProjectInfo>(capacity: _latestProjectInfo.Count);

            foreach (var (_, projectInfo) in _latestProjectInfo)
            {
                builder.Add(projectInfo);
            }

            return builder.DrainToImmutable();
        }
    }

    public void AddListener(IRazorProjectInfoListener listener)
    {
        ImmutableInterlocked.Update(ref _listeners, array => array.Add(listener));
    }
}
