// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(Methods.TextDocumentReferencesName)]
    internal class FindAllReferencesHandler : IRequestHandler<ReferenceParams, VSReferenceItem[]>, IDisposable
    {
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly LSPDocumentManager _documentManager;
        private readonly LSPProjectionProvider _projectionProvider;
        private readonly LSPDocumentMappingProvider _documentMappingProvider;

        private readonly TimeSpan WaitForProgressCompletionTimeout = new TimeSpan(0, 0, 15);

        // Roslyn sends Progress Notifications every 0.5s *only* if results have been found.
        // Consequently, at ~ time > 0.5s ~ after the last notification, we don't know whether Roslyn is
        // done searching for results, or just hasn't found any additional results yet.
        // 
        // https://github.com/dotnet/roslyn/blob/1462b1e9c78a7efe338a6859ee5f88fa07d4adc8/src/Features/LanguageServer/Protocol/CustomProtocol/FindUsagesLSPContext.cs#L52-L55
        // 
        // To work around this, we wait for up to 3s since the last notification before timing out.
        // 
        private readonly TimeSpan WaitForProgressNotificationTimeout = new TimeSpan(0, 0, 0, 3);

        private IProgress<object> ActiveRequest { get; set; }
        private ManualResetEvent WaitForProgressResults { get; set; }
        private CancellationTokenSource QueuedHandlerUnblock { get; set; }
        private readonly object _queuedHandlerUnblockLock = new object();

        [ImportingConstructor]
        public FindAllReferencesHandler(
            LSPRequestInvoker requestInvoker,
            LSPDocumentManager documentManager,
            LSPProjectionProvider projectionProvider,
            LSPDocumentMappingProvider documentMappingProvider)
        {
            if (requestInvoker is null)
            {
                throw new ArgumentNullException(nameof(requestInvoker));
            }

            if (documentManager is null)
            {
                throw new ArgumentNullException(nameof(documentManager));
            }

            if (projectionProvider is null)
            {
                throw new ArgumentNullException(nameof(projectionProvider));
            }

            if (documentMappingProvider is null)
            {
                throw new ArgumentNullException(nameof(documentMappingProvider));
            }

            _requestInvoker = requestInvoker;
            _documentManager = documentManager;
            _projectionProvider = projectionProvider;
            _documentMappingProvider = documentMappingProvider;

            _requestInvoker.AddClientNotifyAsyncHandler(ClientNotifyAsyncHandler);

            WaitForProgressResults = new ManualResetEvent(false);
        }

        public async Task ClientNotifyAsyncHandler(object sender, LanguageClientNotifyEventArgs args)
        {
            await ProcessProgressNotification(args.MethodName, args.ParameterToken).ConfigureAwait(false);
        }

        // For internal testing only to get around LanguageClientNotifyEventArgs accessibility/permissions issues
        internal async Task ClientNotifyAsyncHandlerTest(object sender, (string MethodName, JToken ParameterToken) args)
        {
            await ProcessProgressNotification(args.MethodName, args.ParameterToken).ConfigureAwait(false);
        }

        private async Task ProcessProgressNotification(string methodName, JToken parameterToken)
        {
            // Token support not yet available from VS LSP
            //var token = args.ParameterToken["token"].ToObject<IProgress<object>>();

            if (methodName != Methods.ProgressNotificationName ||
                !parameterToken.HasValues ||
                parameterToken["value"] is null)
            {
                return;
            }

            var referenceResults = parameterToken["value"].ToObject<VSReferenceItem[]>();
            var remappedResults = await ProcessReferenceItems(referenceResults.ToArray(), CancellationToken.None).ConfigureAwait(false);
            ActiveRequest?.Report(remappedResults);

            var cts = new CancellationTokenSource();

            lock (_queuedHandlerUnblockLock)
            {
                if (QueuedHandlerUnblock != null &&
                    QueuedHandlerUnblock.Token.CanBeCanceled &&
                    !QueuedHandlerUnblock.Token.IsCancellationRequested)
                {
                    QueuedHandlerUnblock.Cancel();
                }

                QueuedHandlerUnblock = cts;
            }

            _ = CompleteAfterDelayAsync(cts.Token); // Fire and forget
        }

        private async Task CompleteAfterDelayAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await Task.Delay(WaitForProgressNotificationTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // Task cancelled, new progress notification received.
                // Don't allow handler to return
                return;
            }

            WaitForProgressResults.Set();
        }

        public async Task<VSReferenceItem[]> HandleRequestAsync(ReferenceParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (clientCapabilities is null)
            {
                throw new ArgumentNullException(nameof(clientCapabilities));
            }

            if (!_documentManager.TryGetDocument(request.TextDocument.Uri, out var documentSnapshot))
            {
                return Array.Empty<VSReferenceItem>();
            }

            // Cancels any previous Find All References requests which may still be active
            ResetFarRequest();

            var projectionResult = await _projectionProvider.GetProjectionAsync(documentSnapshot, request.Position, cancellationToken).ConfigureAwait(false);
            if (projectionResult == null || projectionResult.LanguageKind != RazorLanguageKind.CSharp)
            {
                return Array.Empty<VSReferenceItem>();
            }

            cancellationToken.ThrowIfCancellationRequested();

            var referenceParams = new ReferenceParams()
            {
                Position = projectionResult.Position,
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = projectionResult.Uri
                },
                Context = request.Context,
                PartialResultToken = request.PartialResultToken
            };

            ActiveRequest = request.PartialResultToken;

            _ = await _requestInvoker.ReinvokeRequestOnServerAsync<ReferenceParams, VSReferenceItem[]>(
                Methods.TextDocumentReferencesName,
                LanguageServerKind.CSharp,
                referenceParams,
                cancellationToken).ConfigureAwait(false);

            // We must not return till we have received the progress notifications
            // and reported the results via the PartialResultToken
            WaitForProgressResults.WaitOne(WaitForProgressCompletionTimeout);
            ResetFarRequest();

            // Results returned through Progress notification
            return Array.Empty<VSReferenceItem>();
        }

        private void ResetFarRequest()
        {
            ActiveRequest = null;
            WaitForProgressResults.Reset();
        }

        private async Task<VSReferenceItem[]> ProcessReferenceItems(VSReferenceItem[] result, CancellationToken cancellationToken)
        {
            if (result == null || result.Length == 0)
            {
                return result;
            }

            var remappedLocations = new List<VSReferenceItem>();

            foreach (var referenceItem in result)
            {
                if (referenceItem?.Location is null || referenceItem.Text is null)
                {
                    continue;
                }

                if (!RazorLSPConventions.IsRazorCSharpFile(referenceItem.Location.Uri))
                {
                    // This location doesn't point to a virtual cs file. No need to remap.
                    remappedLocations.Add(referenceItem);
                    continue;
                }

                var razorDocumentUri = RazorLSPConventions.GetRazorDocumentUri(referenceItem.Location.Uri);
                var mappingResult = await _documentMappingProvider.MapToDocumentRangesAsync(
                    RazorLanguageKind.CSharp,
                    razorDocumentUri,
                    new[] { referenceItem.Location.Range },
                    cancellationToken).ConfigureAwait(false);

                if (mappingResult == null ||
                    mappingResult.Ranges[0].IsUndefined() ||
                    (_documentManager.TryGetDocument(razorDocumentUri, out var mappedDocumentSnapshot) &&
                    mappingResult.HostDocumentVersion != mappedDocumentSnapshot.Version))
                {
                    // Couldn't remap the location or the document changed in the meantime. Discard this location.
                    continue;
                }

                referenceItem.Location.Uri = razorDocumentUri;
                referenceItem.DisplayPath = razorDocumentUri.AbsolutePath;
                referenceItem.Location.Range = mappingResult.Ranges[0];

                remappedLocations.Add(referenceItem);
            }

            return remappedLocations.ToArray();
        }

        public void Dispose()
        {
            _requestInvoker.RemoveClientNotifyAsyncHandler(ClientNotifyAsyncHandler);
        }
    }
}
