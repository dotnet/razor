// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis.Razor;

internal abstract class ProjectSnapshotManagerDispatcher : IDisposable
{
    private readonly ProjectSnapshotManagerTaskScheduler _dispatcherScheduler;

    public bool IsDispatcherThread
        => Thread.CurrentThread.ManagedThreadId == _dispatcherScheduler.ThreadId;
    public TaskScheduler DispatcherScheduler
        => _dispatcherScheduler;

    protected ProjectSnapshotManagerDispatcher(string threadName, IErrorReporter errorReporter)
    {
        if (threadName is null)
        {
            throw new ArgumentNullException(nameof(threadName));
        }

        _dispatcherScheduler = new ProjectSnapshotManagerTaskScheduler(threadName, errorReporter);
    }

    public void Dispose()
    {
        _dispatcherScheduler.Dispose();
    }

    public Task RunOnDispatcherThreadAsync(Action action, CancellationToken cancellationToken)
        => Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.None, DispatcherScheduler);

    public Task RunOnDispatcherThreadAsync<TArg>(Action<TArg, CancellationToken> action, TArg arg, CancellationToken cancellationToken)
        => Task.Factory.StartNew(() => action(arg, cancellationToken), cancellationToken, TaskCreationOptions.None, DispatcherScheduler);

    public Task<TResult> RunOnDispatcherThreadAsync<TResult>(Func<TResult> action, CancellationToken cancellationToken)
        => Task.Factory.StartNew(action, cancellationToken, TaskCreationOptions.None, DispatcherScheduler);

    public virtual void AssertDispatcherThread([CallerMemberName] string? caller = null)
    {
        if (!IsDispatcherThread)
        {
            caller = caller is null ? "The method" : $"'{caller}'";
            throw new InvalidOperationException(caller + " must be called on the project snapshot manager's thread.");
        }
    }

    private class ProjectSnapshotManagerTaskScheduler : TaskScheduler, IDisposable
    {
        private readonly Thread _thread;
        private readonly BlockingCollection<Task> _tasks = new();
        private readonly IErrorReporter _errorReporter;
        private bool _disposed;
        private readonly object _disposalLock = new();

        public ProjectSnapshotManagerTaskScheduler(string threadName, IErrorReporter errorReporter)
        {
            _thread = new Thread(ThreadStart)
            {
                Name = threadName,
                IsBackground = true,
            };

            _thread.Start();
            _errorReporter = errorReporter;
        }

        public int ThreadId => _thread.ManagedThreadId;

        public override int MaximumConcurrencyLevel => 1;

        protected override void QueueTask(Task task)
        {
            lock (_disposalLock)
            {
                if (_disposed)
                {
                    return;
                }
            }

            _tasks.Add(task);
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

        protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();

        private void ThreadStart()
        {
            while (!_disposed)
            {
                try
                {
                    var task = _tasks.Take();
                    TryExecuteTask(task);
                }
                catch (ThreadAbortException ex)
                {
                    // Fires when things shut down or in tests. Log exception and bail out.
                    _errorReporter.ReportError(ex);
                    return;
                }
                catch (Exception ex)
                {
                    lock (_disposalLock)
                    {
                        if (_disposed)
                        {
                            // Graceful teardown
                            return;
                        }
                    }

                    // Unexpected exception. Log and throw.
                    _errorReporter.ReportError(ex);
                    throw;
                }
            }
        }

        public void Dispose()
        {
            lock (_disposalLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _tasks.CompleteAdding();
            }
        }
    }
}
