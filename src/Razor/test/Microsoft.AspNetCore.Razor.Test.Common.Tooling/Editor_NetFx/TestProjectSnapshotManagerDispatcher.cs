// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Test.Common.Editor;

internal class TestProjectSnapshotManagerDispatcher : IProjectSnapshotManagerDispatcher
{
    public TestProjectSnapshotManagerDispatcher()
    {
        Scheduler = SynchronizationContext.Current is null
            ? new ThrowingTaskScheduler()
            : TaskScheduler.FromCurrentSynchronizationContext();
    }

    public TaskScheduler Scheduler { get; }

    private Thread Thread { get; } = Thread.CurrentThread;

    public bool IsRunningOnDispatcher => Thread.CurrentThread == Thread;

    private class ThrowingTaskScheduler : TaskScheduler
    {
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return [];
        }

        protected override void QueueTask(Task task)
        {
            throw new InvalidOperationException($"Use [{nameof(UIFactAttribute)}]");
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            throw new InvalidOperationException($"Use [{nameof(UIFactAttribute)}]");
        }
    }
}
