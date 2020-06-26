// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using System.Composition;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [Export(typeof(LSPProgressListener))]
    internal class DefaultLSPProgressListener : LSPProgressListener, IDisposable
    {
        private readonly ILanguageServiceBroker2 _languageServiceBroker;

        private ConcurrentDictionary<string, ProgressRequest> _activeRequests
            = new ConcurrentDictionary<string, ProgressRequest>();

        [ImportingConstructor]
        public DefaultLSPProgressListener(ILanguageServiceBroker2 languageServiceBroker)
        {
            if (languageServiceBroker is null)
            {
                throw new ArgumentNullException(nameof(languageServiceBroker));
            }

            _languageServiceBroker = languageServiceBroker;

            _languageServiceBroker.ClientNotifyAsync += ClientNotifyAsyncListenerAsync;
        }

        private Task ClientNotifyAsyncListenerAsync(object sender, LanguageClientNotifyEventArgs args)
        {
            return ProcessProgressNotificationAsync(args.MethodName, args.ParameterToken);
        }

        private async Task ProcessProgressNotificationAsync(string methodName, JToken parameterToken)
        {
            if (methodName != Methods.ProgressNotificationName ||
               !parameterToken.HasValues ||
               parameterToken["value"] is null ||
               parameterToken["token"] is null)
            {
                return;
            }

            var token = parameterToken["token"].ToObject<string>(); // IProgress<object>>();

            if (string.IsNullOrEmpty(token) || !_activeRequests.TryGetValue(token, out var request))
            {
                return;
            }

            var value = parameterToken["value"];

            try
            {
                await request.OnProgressResult(value).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // Initial handle request has been cancelled
                // We can deem this ProgressRequest complete
                ProgressCompleted(token);
                return;
            }

            var cts = new CancellationTokenSource();

            lock (request.RequestLock)
            {
                var existingCTS = request.CancellationTokenSource;
                if (existingCTS != null &&
                    existingCTS.Token.CanBeCanceled &&
                    !existingCTS.Token.IsCancellationRequested)
                {
                    existingCTS.Cancel();
                }

                request.CancellationTokenSource = cts;
            }

            _ = CompleteAfterDelayAsync(token, request); // Fire and forget
        }

        private async Task CompleteAfterDelayAsync(string token, ProgressRequest request)
        {
            if (request.CancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await Task.Delay(request.TimeoutAfterLastNotify, request.CancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // Task cancelled, new progress notification received.
                // Don't allow handler to return
                return;
            }

            ProgressCompleted(token);
        }

        internal override bool TryListenForProgress(
            string requestId,
            Func<JToken, Task> onProgressResult,
            TimeSpan timeoutAfterLastNotify,
            out Task onCompleted)
        {
            var onCompletedSource = new TaskCompletionSource<bool>();
            var progressRequest = new ProgressRequest(onProgressResult, timeoutAfterLastNotify, onCompletedSource);
            onCompleted = onCompletedSource.Task;

            return _activeRequests.TryAdd(requestId, progressRequest);
        }

        private void ProgressCompleted(string requestId)
        {
            if (_activeRequests.TryRemove(requestId, out var request))
            {
                // We're setting the result of a Task<bool>
                // however we only return a Task to the
                // handler/subscriber, so the bool is ignored
                request.OnCompleted.SetResult(false);
            }
        }

        public void Dispose()
        {
            _languageServiceBroker.ClientNotifyAsync -= ClientNotifyAsyncListenerAsync;
        }

        internal class ProgressRequest
        {
            internal ProgressRequest(
                Func<JToken, Task> onProgressResult,
                TimeSpan timeoutAfterLastNotify,
                TaskCompletionSource<bool> onCompleted)
            {
                if (onProgressResult is null)
                {
                    throw new ArgumentNullException(nameof(onProgressResult));
                }

                if (onCompleted is null)
                {
                    throw new ArgumentNullException(nameof(onCompleted));
                }

                OnProgressResult = onProgressResult;
                TimeoutAfterLastNotify = timeoutAfterLastNotify;
                OnCompleted = onCompleted;
            }

            internal Func<JToken, Task> OnProgressResult { get; }
            internal TaskCompletionSource<bool> OnCompleted { get; }

            internal TimeSpan TimeoutAfterLastNotify { get; }
            internal object RequestLock { get; } = new object();
            internal CancellationTokenSource CancellationTokenSource { get; set; }
        }
    }
}
