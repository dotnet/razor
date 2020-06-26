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
        private const string ProgressNotificationValueName = "value";

        private readonly ILanguageServiceBroker2 _languageServiceBroker;

        private readonly ConcurrentDictionary<string, ProgressRequest> _activeRequests
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
               parameterToken[ProgressNotificationValueName] is null ||
               parameterToken[Methods.ProgressNotificationTokenName] is null)
            {
                return;
            }

            var token = parameterToken[Methods.ProgressNotificationTokenName].ToObject<string>(); // IProgress<object>>();

            if (string.IsNullOrEmpty(token) || !_activeRequests.TryGetValue(token, out var request))
            {
                return;
            }

            var value = parameterToken[ProgressNotificationValueName];

            try
            {
                request.HandlerCancellationToken.ThrowIfCancellationRequested();
                await request.OnProgressNotifyAsync(value).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // Initial handle request has been cancelled
                // We can deem this ProgressRequest complete
                ProgressCompleted(token);
                return;
            }

            CompleteAfterDelay(token, request);
        }

        private void CompleteAfterDelay(string token, ProgressRequest request)
        {
            CancellationTokenSource linkedCTS;

            lock (request.RequestLock)
            {
                var existingTimeoutCTS = request.TimeoutCancellationTokenSource;
                if (existingTimeoutCTS != null &&
                    existingTimeoutCTS.Token.CanBeCanceled &&
                    !existingTimeoutCTS.Token.IsCancellationRequested)
                {
                    existingTimeoutCTS.Cancel();
                }

                request.TimeoutCancellationTokenSource = new CancellationTokenSource();
                linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(
                    request.TimeoutCancellationTokenSource.Token,
                    request.HandlerCancellationToken);
            }

            _ = CompleteAfterDelayAsync(token, request.TimeoutAfterLastNotify, linkedCTS.Token); // Fire and forget
        }

        private async Task CompleteAfterDelayAsync(string token, TimeSpan delay, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // Task cancelled, new progress notification received.
                // Don't allow handler to return
                return;
            }

            ProgressCompleted(token);
        }

        public override bool TryListenForProgress(
            string token,
            Func<JToken, Task> onProgressNotifyAsync,
            TimeSpan timeoutAfterLastNotify,
            CancellationToken handlerCancellationToken,
            out Task onCompleted)
        {
            var onCompletedSource = new TaskCompletionSource<bool>();
            var request = new ProgressRequest(
                onProgressNotifyAsync,
                timeoutAfterLastNotify,
                handlerCancellationToken,
                onCompletedSource);

            if (!_activeRequests.TryAdd(token, request))
            {
                onCompleted = null;
                return false;
            }

            CompleteAfterDelay(token, request);
            onCompleted = onCompletedSource.Task;
            return true;
        }

        private void ProgressCompleted(string token)
        {
            if (_activeRequests.TryRemove(token, out var request))
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

        private class ProgressRequest
        {
            public ProgressRequest(
                Func<JToken, Task> onProgressNotifyAsync,
                TimeSpan timeoutAfterLastNotify,
                CancellationToken handlerCancellationToken,
                TaskCompletionSource<bool> onCompleted)
            {
                if (onProgressNotifyAsync is null)
                {
                    throw new ArgumentNullException(nameof(onProgressNotifyAsync));
                }

                if (onCompleted is null)
                {
                    throw new ArgumentNullException(nameof(onCompleted));
                }

                OnProgressNotifyAsync = onProgressNotifyAsync;
                TimeoutAfterLastNotify = timeoutAfterLastNotify;
                HandlerCancellationToken = handlerCancellationToken;
                OnCompleted = onCompleted;
            }

            internal Func<JToken, Task> OnProgressNotifyAsync { get; }
            internal TaskCompletionSource<bool> OnCompleted { get; }
            internal CancellationToken HandlerCancellationToken { get; }

            internal TimeSpan TimeoutAfterLastNotify { get; }
            internal CancellationTokenSource TimeoutCancellationTokenSource { get; set; }
            internal object RequestLock { get; } = new object();
        }
    }
}
