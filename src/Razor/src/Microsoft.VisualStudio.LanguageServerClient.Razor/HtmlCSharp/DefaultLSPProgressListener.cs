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

        private ConcurrentDictionary<string, CallbackRequest> ActiveRequests { get; }
            = new ConcurrentDictionary<string, CallbackRequest>();

        public override async Task ClientNotifyAsyncListenerAsync(object sender, LanguageClientNotifyEventArgs args)
        {
            await ProcessProgressNotificationAsync(args.MethodName, args.ParameterToken).ConfigureAwait(false);
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

            if (string.IsNullOrEmpty(token) || !ActiveRequests.TryGetValue(token, out var callbackRequest))
            {
                return;
            }

            var value = parameterToken["value"];

            try
            {
                await callbackRequest.Callback(value).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // Initial textDocument/references handle request
                // has been cancelled
                //
                // Release the handler if it's still waiting
                callbackRequest.WaitForEventCompletion?.Set();
                return;
            }

            // Checks if the callback request supports blocking
            // till progress notification completion
            if (callbackRequest.WaitForEventCompletion is null)
            {
                return;
            }

            var cts = new CancellationTokenSource();

            lock (callbackRequest.RequestLock)
            {
                var existingCTS = callbackRequest.CancellationTokenSource;
                if (existingCTS != null &&
                    existingCTS.Token.CanBeCanceled &&
                    !existingCTS.Token.IsCancellationRequested)
                {
                    existingCTS.Cancel();
                }

                callbackRequest.CancellationTokenSource = cts;
            }

            _ = CompleteAfterDelayAsync(callbackRequest); // Fire and forget
        }

        private async Task CompleteAfterDelayAsync(CallbackRequest request)
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

            request.WaitForEventCompletion.Set();
        }

        internal override bool Subscribe(CallbackRequest callbackRequest, string requestId)
        {
            return ActiveRequests.TryAdd(requestId, callbackRequest);
        }

        internal override bool Unsubscribe(string requestId)
        {
            return ActiveRequests.TryRemove(requestId, out _);
        }

        public void Dispose()
        {
            _languageServiceBroker.ClientNotifyAsync -= ClientNotifyAsyncListenerAsync;
        }
    }
}
