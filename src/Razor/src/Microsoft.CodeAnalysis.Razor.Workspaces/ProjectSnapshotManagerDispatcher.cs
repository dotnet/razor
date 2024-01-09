// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal abstract partial class ProjectSnapshotManagerDispatcher : IProjectSnapshotManagerDispatcher, IDisposable
{
    private readonly DefaultScheduler _scheduler;

    public bool IsRunningOnScheduler => TaskScheduler.Current == _scheduler;
    public TaskScheduler Scheduler => _scheduler;

    protected ProjectSnapshotManagerDispatcher(IErrorReporter errorReporter)
    {
        _scheduler = new DefaultScheduler(errorReporter);
    }

    public void Dispose()
    {
        _scheduler.Dispose();
    }
}
