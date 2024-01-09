// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal abstract partial class ProjectSnapshotManagerDispatcher
{
    private sealed class DefaultScheduler : TaskScheduler, IDisposable
    {
        private readonly IErrorReporter _errorReporter;
        private readonly AsyncQueue<Task> _taskQueue;
        private readonly CancellationTokenSource _disposeCancellationSource;

        public DefaultScheduler(IErrorReporter errorReporter)
        {
            _errorReporter = errorReporter;
            _disposeCancellationSource = new CancellationTokenSource();
            _taskQueue = new AsyncQueue<Task>();

            _ = Task.Run(ProcessQueueAsync);
        }

        public override int MaximumConcurrencyLevel => 1;

        protected override void QueueTask(Task task)
        {
            _taskQueue.TryEnqueue(task);
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

        protected override IEnumerable<Task> GetScheduledTasks()
            => _taskQueue.ToArray();

        private async Task ProcessQueueAsync()
        {
            var disposeToken = _disposeCancellationSource.Token;

            try
            {
                while (!disposeToken.IsCancellationRequested)
                {
                    try
                    {
                        // ConfigureAwait(true) is used below because this method runs on the
                        // ThreadPool and we want to continue running on the ThreadPool.
                        var task = await _taskQueue.DequeueAsync(disposeToken).ConfigureAwait(true);

                        var result = TryExecuteTask(task);
                        Debug.Assert(result);
                    }
                    catch (OperationCanceledException)
                    {
                        // Silently proceed.
                    }
                    catch (Exception ex)
                    {
                        _errorReporter.ReportError(ex);

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
                if (_taskQueue.IsCompleted)
                {
                    while (_taskQueue.TryDequeue(out _))
                    {
                        // Intentionally empty
                    }
                }
            }
        }

        public void Dispose()
        {
            _taskQueue.Complete();

            _disposeCancellationSource.Cancel();
            _disposeCancellationSource.Dispose();
        }
    }
}
