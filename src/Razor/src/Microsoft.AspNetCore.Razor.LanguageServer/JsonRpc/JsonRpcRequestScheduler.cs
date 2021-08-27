// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Threading;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.JsonRpc
{
    internal class JsonRpcRequestScheduler : IDisposable
    {
        private readonly Task _processingQueueTask;
        private readonly AsyncQueue<QueueItem> _queue;
        private readonly CancellationTokenSource _disposedCancellationTokenSource;
        private readonly ILogger<JsonRpcRequestScheduler> _logger;

        public JsonRpcRequestScheduler(ILoggerFactory loggerFactory)
        {
            _disposedCancellationTokenSource = new CancellationTokenSource();
            _logger = loggerFactory.CreateLogger<JsonRpcRequestScheduler>();
            _queue = new AsyncQueue<QueueItem>();

            // Start the queue processing
            _processingQueueTask = ProcessQueueAsync();
        }

        public bool Schedule(RequestProcessType type, string identifier, ProcessSchedulerDelegate processAsync)
        {
            lock (_disposedCancellationTokenSource)
            {
                if (_disposedCancellationTokenSource.IsCancellationRequested)
                {
                    // Shutting down
                    return false;
                }
            }

            var queueItem = new QueueItem(type, identifier, processAsync);

            // Try and enqueue item, if this fails it means we're tearing down.
            var enqueued = _queue.TryEnqueue(queueItem);
            return enqueued;
        }

        public void Dispose()
        {
            lock (_disposedCancellationTokenSource)
            {
                if (_disposedCancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                _disposedCancellationTokenSource.Cancel();
            }
        }

        private async Task ProcessQueueAsync()
        {
            try
            {
                while (!_disposedCancellationTokenSource.Token.IsCancellationRequested)
                {
                    var work = await _queue.DequeueAsync(_disposedCancellationTokenSource.Token).ConfigureAwait(false);

                    _logger.LogDebug("Queueing {Type} {Identifier} request for processing", work.Type, work.Identifier);

                    if (work.Type == RequestProcessType.Serial)
                    {
                        // Serial requests block other requests from starting to ensure up-to-date state is used.
                        await work.ProcessAsync(_disposedCancellationTokenSource.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        _ = Task.Run(() => work.ProcessAsync(_disposedCancellationTokenSource.Token), _disposedCancellationTokenSource.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // We're shutting down
            }
            catch (Exception e)
            {
                _logger.LogCritical($"Work queue threw an uncaught exception. Stopping processing: {e.Message}");
            }
        }

        private record QueueItem(RequestProcessType Type, string Identifier, ProcessSchedulerDelegate ProcessAsync);

        public delegate Task ProcessSchedulerDelegate(CancellationToken cancellationToken);

        public TestAccessor GetTestAccessor() => new TestAccessor(this);

        public class TestAccessor
        {
            private readonly JsonRpcRequestScheduler _requestScheduler;

            public TestAccessor(JsonRpcRequestScheduler requestScheduler)
            {
                _requestScheduler = requestScheduler;
            }

#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
            public Task CompleteWorkAsync() => _requestScheduler._processingQueueTask;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
        }
    }
}
