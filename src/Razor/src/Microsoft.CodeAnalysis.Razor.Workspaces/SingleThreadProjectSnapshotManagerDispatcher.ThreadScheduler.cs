// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal abstract partial class SingleThreadProjectSnapshotManagerDispatcher
{
    private sealed class ThreadScheduler : TaskScheduler, IDisposable
    {
        private readonly Thread _thread;
        private readonly BlockingCollection<Task> _taskQueue;
        private readonly Action<Exception> _logException;
        private readonly CancellationTokenSource _disposeCancellationSource;

        public ThreadScheduler(string threadName, Action<Exception> logException)
        {
            _logException = logException;
            _disposeCancellationSource = new CancellationTokenSource();
            _taskQueue = [];

            _thread = new Thread(ThreadStart)
            {
                Name = threadName,
                IsBackground = true,
            };

            _thread.Start();
        }

        public override int MaximumConcurrencyLevel => 1;

        protected override void QueueTask(Task task)
        {
            _taskQueue.TryAdd(task, Timeout.Infinite, _disposeCancellationSource.Token);
        }

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

        protected override IEnumerable<Task> GetScheduledTasks() => _taskQueue.ToArray();

        private void ThreadStart()
        {
            var disposeToken = _disposeCancellationSource.Token;

            try
            {
                while (!disposeToken.IsCancellationRequested)
                {
                    try
                    {
                        if (_taskQueue.TryTake(out var task, Timeout.Infinite, disposeToken))
                        {
                            TryExecuteTask(task);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Silently proceed.
                    }
                    catch (Exception ex)
                    {
                        _logException(ex);

                        if (ex is ThreadAbortException)
                        {
                            // Don't rethrow if we're tearing down.
                            return;
                        }

                        throw;
                    }
                }
            }
            finally
            {
                if (_taskQueue.IsAddingCompleted)
                {
                    while (_taskQueue.Count > 0)
                    {
                        _taskQueue.TryTake(out _);
                    }
                }
            }
        }

        public void Dispose()
        {
            _taskQueue.CompleteAdding();

            _disposeCancellationSource.Cancel();
            _disposeCancellationSource.Dispose();
        }
    }
}
