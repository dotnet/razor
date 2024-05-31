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
using Microsoft.CodeAnalysis.Razor.Utilities;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal abstract partial class RazorProjectInfoPublisher : IRazorProjectInfoPublisher, IDisposable
{
    private abstract record Work(ProjectKey ProjectKey);
    private sealed record Update(RazorProjectInfo ProjectInfo) : Work(ProjectInfo.ProjectKey);
    private sealed record Remove(ProjectKey ProjectKey) : Work(ProjectKey);

    private static readonly TimeSpan s_delay = TimeSpan.FromMilliseconds(250);

    private readonly CancellationTokenSource _disposeTokenSource;
    private readonly AsyncBatchingWorkQueue<Work> _workQueue;

    private readonly Dictionary<ProjectKey, RazorProjectInfo> _latestProjectInfo;
    private ImmutableArray<IRazorProjectInfoListener> _listeners;

    protected RazorProjectInfoPublisher()
        : this(s_delay)
    {
    }

    protected RazorProjectInfoPublisher(TimeSpan delay)
    {
        _disposeTokenSource = new();
        _workQueue = new AsyncBatchingWorkQueue<Work>(delay, ProcessBatchAsync, _disposeTokenSource.Token);
        _latestProjectInfo = [];
        _listeners = [];
    }

    public void Dispose()
    {
        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
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

    protected void EnqueueUpdate(RazorProjectInfo projectInfo)
    {
        _workQueue.AddWork(new Update(projectInfo));
    }

    protected void EnqueueRemove(ProjectKey projectKey)
    {
        _workQueue.AddWork(new Remove(projectKey));
    }

    ImmutableArray<RazorProjectInfo> IRazorProjectInfoPublisher.GetLatestProjects()
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

    void IRazorProjectInfoPublisher.AddListener(IRazorProjectInfoListener listener)
    {
        ImmutableInterlocked.Update(ref _listeners, array => array.Add(listener));
    }
}
