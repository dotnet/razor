// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(Methods.TextDocumentReferencesName)]
    internal class FindAllReferencesHandler : IRequestHandler<ReferenceParams, VSReferenceItem[]>
    {
        // Roslyn sends Progress Notifications every 0.5s *only* if results have been found.
        // Consequently, at ~ time > 0.5s ~ after the last notification, we don't know whether Roslyn is
        // done searching for results, or just hasn't found any additional results yet.
        // To work around this, we wait for up to 3s since the last notification before timing out.
        private static readonly TimeSpan WaitForProgressNotificationTimeout = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan WaitForProgressCompletionTimeout = TimeSpan.FromSeconds(15);

        private readonly LSPRequestInvoker _requestInvoker;
        private readonly LSPDocumentManager _documentManager;
        private readonly LSPProjectionProvider _projectionProvider;
        private readonly LSPDocumentMappingProvider _documentMappingProvider;
        private readonly LSPProgressListener _lspProgressListener;

        [ImportingConstructor]
        public FindAllReferencesHandler(
            LSPRequestInvoker requestInvoker,
            LSPDocumentManager documentManager,
            LSPProjectionProvider projectionProvider,
            LSPDocumentMappingProvider documentMappingProvider,
            LSPProgressListener lspProgressListener)
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

            if (lspProgressListener is null)
            {
                throw new ArgumentNullException(nameof(lspProgressListener));
            }

            _requestInvoker = requestInvoker;
            _documentManager = documentManager;
            _projectionProvider = projectionProvider;
            _documentMappingProvider = documentMappingProvider;
            _lspProgressListener = lspProgressListener;
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
                return null;
            }

            var projectionResult = await _projectionProvider.GetProjectionAsync(documentSnapshot, request.Position, cancellationToken).ConfigureAwait(false);
            if (projectionResult == null || projectionResult.LanguageKind != RazorLanguageKind.CSharp)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Temporary till IProgress serialization is fixed
            var requestId = Guid.NewGuid().ToString(); // request.PartialResultToken.Id

            var referenceParams = new SerializableReferenceParams()
            {
                Position = projectionResult.Position,
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = projectionResult.Uri
                },
                Context = request.Context,
                PartialResultToken = requestId // request.PartialResultToken
            };

            if (!_lspProgressListener.TryListenForProgress(
                requestId,
                onProgressResult: (value) => ProcessReferenceItemsAsync(value, request.PartialResultToken, cancellationToken),
                WaitForProgressNotificationTimeout,
                out var onCompleted))
            {
                return null;
            }

            _ = await _requestInvoker.ReinvokeRequestOnServerAsync<SerializableReferenceParams, VSReferenceItem[]>(
                Methods.TextDocumentReferencesName,
                LanguageServerKind.CSharp,
                referenceParams,
                cancellationToken).ConfigureAwait(false);

            // We must not return till we have received the progress notifications
            // and reported the results via the PartialResultToken
            await onCompleted.ConfigureAwait(false);

            // Results returned through Progress notification
            return Array.Empty<VSReferenceItem>();
        }

        private async Task ProcessReferenceItemsAsync(
            JToken value,
            IProgress<object> progress,
            CancellationToken cancellationToken)
        {
            var result = value.ToObject<VSReferenceItem[]>();

            // Checks if the initial Handle request has been cancelled
            cancellationToken.ThrowIfCancellationRequested();

            if (result == null || result.Length == 0)
            {
                return;
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

            progress.Report(remappedLocations.ToArray());
        }

        // Temporary while the PartialResultToken serialization fix is in
        [DataContract]
        private class SerializableReferenceParams : TextDocumentPositionParams
        {
            [DataMember(Name = "context")]
            public ReferenceContext Context { get; set; }

            [DataMember(Name = "partialResultToken")]
            public string PartialResultToken { get; set; }
        }
    }
}
