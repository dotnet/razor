// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Utilities;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal abstract partial class RazorProjectInfoPublisher : IRazorProjectInfoPublisher, IDisposable
{
    private static readonly TimeSpan s_delay = TimeSpan.FromMilliseconds(250);

    private readonly CancellationTokenSource _disposeTokenSource;
    private readonly AsyncBatchingWorkQueue<RazorProjectInfo> _workQueue;
    private ImmutableArray<IRazorProjectInfoListener> _listeners;

    protected RazorProjectInfoPublisher()
        : this(s_delay)
    {
    }

    protected RazorProjectInfoPublisher(TimeSpan delay)
    {
        _disposeTokenSource = new();
        _workQueue = new AsyncBatchingWorkQueue<RazorProjectInfo>(delay, ProcessBatchAsync, _disposeTokenSource.Token);
        _listeners = [];
    }

    public void Dispose()
    {
        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
    }

    private async ValueTask ProcessBatchAsync(ImmutableArray<RazorProjectInfo> items, CancellationToken token)
    {
        foreach (var projectInfo in items.GetMostRecentUniqueItems(Comparer.Instance))
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            foreach (var listener in _listeners)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                await listener.UpdatedAsync(projectInfo).ConfigureAwait(false);
            }
        }
    }

    protected void AddWork(RazorProjectInfo projectInfo)
    {
        _workQueue.AddWork(projectInfo);
    }

    void IRazorProjectInfoPublisher.AddListener(IRazorProjectInfoListener listener)
    {
        ImmutableInterlocked.Update(ref _listeners, array => array.Add(listener));
    }
}
