// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Razor.Workspaces
{
    internal class DefaultProjectSnapshotManagerDispatcher : ProjectSnapshotManagerDispatcher
    {
        public override bool IsDispatcherThread
            => Thread.CurrentThread.ManagedThreadId == ProjectSnapshotManagerTaskScheduler.Instance.ThreadId;

        public override TaskScheduler DispatcherScheduler { get; } = ProjectSnapshotManagerTaskScheduler.Instance;

        internal class ProjectSnapshotManagerTaskScheduler : TaskScheduler
        {
            public static ProjectSnapshotManagerTaskScheduler Instance = new();

            private readonly Thread _thread;
            private readonly BlockingCollection<Task> _tasks = new();

            private ProjectSnapshotManagerTaskScheduler()
            {
                _thread = new Thread(ThreadStart)
                {
                    Name = "Razor." + nameof(ProjectSnapshotManagerDispatcher),
                    IsBackground = true,
                };

                _thread.Start();
            }

            public int ThreadId => _thread.ManagedThreadId;

            public override int MaximumConcurrencyLevel => 1;

            protected override void QueueTask(Task task) => _tasks.Add(task);

            protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            {
                // If the task was previously queued it means that we're ensuring it's running on our single threaded scheduler.
                // Otherwise, we can't enforce that behavior and therefore need it to be re-queued before execution.
                if (taskWasPreviouslyQueued)
                {
                    return TryExecuteTask(task);
                }

                return false;
            }

            protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();

            private void ThreadStart()
            {
                while (true)
                {
                    try
                    {
                        var task = _tasks.Take();
                        TryExecuteTask(task);
                    }
                    catch (ThreadAbortException)
                    {
                        // Fires when things shut down or in tests. Swallow thread abort exceptions and bail out.
                        return;
                    }
                }
            }
        }
    }
}
