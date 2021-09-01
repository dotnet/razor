// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Razor.Workspaces
{
    internal abstract class ProjectSnapshotManagerDispatcherBase : ProjectSnapshotManagerDispatcher
    {
        private readonly ProjectSnapshotManagerTaskScheduler _dispatcherScheduler;

        public ProjectSnapshotManagerDispatcherBase(string threadName)
        {
            _dispatcherScheduler = new ProjectSnapshotManagerTaskScheduler(threadName);
        }

        public override bool IsDispatcherThread => Thread.CurrentThread.ManagedThreadId == _dispatcherScheduler.ThreadId;

        public override TaskScheduler DispatcherScheduler => _dispatcherScheduler;

        private class ProjectSnapshotManagerTaskScheduler : TaskScheduler
        {
            private readonly string _threadName;
            private readonly Thread _thread;
            private readonly BlockingCollection<Task> _tasks = new();

            public ProjectSnapshotManagerTaskScheduler(string threadName)
            {
                _threadName = threadName;

                _thread = new Thread(ThreadStart)
                {
                    Name = threadName,
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
                        if (!TryExecuteTask(task))
                        {
                            Debug.WriteLine("Task failed to execute on thread " + _threadName + ".");
                        }
                    }
                    catch (ThreadAbortException e)
                    {
                        // Fires when things shut down or in tests. Swallow thread abort exceptions and bail out.
                        Debug.WriteLine("Thread " + _threadName + " was aborted." + Environment.NewLine +
                            "Stack trace:" + Environment.NewLine +
                            e.StackTrace);
                        return;
                    }
                }
            }
        }
    }
}
