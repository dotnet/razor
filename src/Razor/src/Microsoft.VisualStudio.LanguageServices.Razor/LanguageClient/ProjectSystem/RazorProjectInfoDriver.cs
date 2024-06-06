// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Utilities;

namespace Microsoft.VisualStudio.Razor.LanguageClient.ProjectSystem;

internal sealed partial class RazorProjectInfoDriver : IRazorProjectInfoDriver, IDisposable
{
    private abstract record Work(ProjectKey ProjectKey);
    private sealed record Update(RazorProjectInfo ProjectInfo) : Work(ProjectInfo.ProjectKey);
    private sealed record Remove(ProjectKey ProjectKey) : Work(ProjectKey);

    private static readonly TimeSpan s_delay = TimeSpan.FromMilliseconds(250);

    private readonly IProjectSnapshotManager _projectManager;
    private readonly CancellationTokenSource _disposeTokenSource;
    private readonly AsyncBatchingWorkQueue<Work> _workQueue;

    private readonly Dictionary<ProjectKey, RazorProjectInfo> _latestProjectInfoMap;
    private ImmutableArray<IRazorProjectInfoListener> _listeners;

    public RazorProjectInfoDriver(IProjectSnapshotManager projectManager, TimeSpan? delay = null)
    {
        _projectManager = projectManager;
        _disposeTokenSource = new();
        _workQueue = new AsyncBatchingWorkQueue<Work>(delay ?? s_delay, ProcessBatchAsync, _disposeTokenSource.Token);
        _latestProjectInfoMap = [];
        _listeners = [];
    }

    public void Dispose()
    {
        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        // Even though we aren't mutating the project snapshot manager, we call UpdateAsync(...) here to ensure
        // that we run on its dispatcher. That ensures that no changes will code in while we are iterating the
        // current set of projects and connected to the Changed event.
        await _projectManager.UpdateAsync(updater =>
        {
            foreach (var project in updater.GetProjects())
            {
                EnqueueUpdate(project.ToRazorProjectInfo());
            }

            _projectManager.Changed += ProjectManager_Changed;
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

            // Update our map first
            lock (_latestProjectInfoMap)
            {
                switch (work)
                {
                    case Update(var projectInfo):
                        _latestProjectInfoMap[projectInfo.ProjectKey] = projectInfo;
                        break;

                    case Remove(var projectKey):
                        _latestProjectInfoMap.Remove(projectKey);
                        break;

                    default:
                        Assumed.Unreachable();
                        break;
                }
            }

            // Now, notify listeners
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

    public ImmutableArray<RazorProjectInfo> GetLatestProjectInfo()
    {
        lock (_latestProjectInfoMap)
        {
            using var builder = new PooledArrayBuilder<RazorProjectInfo>(capacity: _latestProjectInfoMap.Count);

            foreach (var (_, projectInfo) in _latestProjectInfoMap)
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
