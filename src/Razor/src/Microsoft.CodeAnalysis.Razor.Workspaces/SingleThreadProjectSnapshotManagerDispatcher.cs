// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal abstract partial class SingleThreadProjectSnapshotManagerDispatcher : IProjectSnapshotManagerDispatcher, IDisposable
{
    private readonly ThreadScheduler _scheduler;

    public bool IsRunningOnDispatcher => TaskScheduler.Current == _scheduler;
    public TaskScheduler Scheduler => _scheduler;

    public SingleThreadProjectSnapshotManagerDispatcher(string threadName)
    {
        if (threadName is null)
        {
            throw new ArgumentNullException(nameof(threadName));
        }

        _scheduler = new ThreadScheduler(threadName, LogException);
    }

    public void Dispose()
    {
        _scheduler.Dispose();
    }

    protected abstract void LogException(Exception ex);
}
