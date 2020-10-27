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
    [ExportLspMethod(MSLSPMethods.WorkspacePullDiagnosticName)]
    internal class WorkspacePullDiagnosticsHandler :
        LSPProgressListenerHandlerBase<WorkspaceDocumentDiagnosticsParams, WorkspaceDiagnosticReport[]?>,
        IRequestHandler<WorkspaceDocumentDiagnosticsParams, WorkspaceDiagnosticReport[]?>
    {
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly LSPDocumentManager _documentManager;
        private readonly LSPProjectionProvider _projectionProvider;
        private readonly LSPDocumentMappingProvider _documentMappingProvider;
        private readonly LSPProgressListener _lspProgressListener;

        [ImportingConstructor]
        public WorkspacePullDiagnosticsHandler(
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

        // Internal for testing
        internal async override Task<WorkspaceDiagnosticReport[]?> HandleRequestAsync(WorkspaceDocumentDiagnosticsParams request, ClientCapabilities clientCapabilities, string token, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (clientCapabilities is null)
            {
                throw new ArgumentNullException(nameof(clientCapabilities));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var referenceParams = new SerializableWorkspaceDocumentDiagnosticsParams()
            {
                PreviousResults = request.PreviousResults,
                PartialResultToken = token // request.PartialResultToken
            };

            if (!_lspProgressListener.TryListenForProgress(
                token,
                onProgressNotifyAsync: (value, ct) => ProcessWorkspaceDiagnosticsAsync(value, request.PartialResultToken, ct),
                WaitForProgressNotificationTimeout,
                cancellationToken,
                out var onCompleted))
            {
                return null;
            }

            var result = await _requestInvoker.ReinvokeRequestOnServerAsync<SerializableWorkspaceDocumentDiagnosticsParams, WorkspaceDiagnosticReport[]?>(
                MSLSPMethods.WorkspacePullDiagnosticName,
                RazorLSPConstants.CSharpContentTypeName,
                referenceParams,
                cancellationToken).ConfigureAwait(false);

            if (result == null)
            {
                return null;
            }

            // We must not return till we have received the progress notifications
            // and reported the results via the PartialResultToken
            await onCompleted.ConfigureAwait(false);

            // Results returned through Progress notification
            var remappedResults = await RemapWorkspaceDiagnosticsAsync(result, cancellationToken).ConfigureAwait(false);
            return remappedResults;
        }

        private async Task ProcessWorkspaceDiagnosticsAsync(
            JToken value,
            IProgress<WorkspaceDiagnosticReport[]?> progress,
            CancellationToken cancellationToken)
        {
            var result = value.ToObject<WorkspaceDiagnosticReport[]?>();

            if (result == null || result.Length == 0)
            {
                return;
            }

            var remappedResults = await RemapWorkspaceDiagnosticsAsync(result, cancellationToken).ConfigureAwait(false);

            progress.Report(remappedResults);
        }

        private async Task<WorkspaceDiagnosticReport[]?> RemapWorkspaceDiagnosticsAsync(WorkspaceDiagnosticReport[]? diagnosticReport, CancellationToken cancellationToken)
        {
            var remappedLocations = new List<WorkspaceDiagnosticReport>();

            foreach (var diagnostic in diagnosticReport)
            {
                //if (diagnostic?.Location is null || diagnostic.Text is null)
                //{
                //    continue;
                //}

                //if (!RazorLSPConventions.IsRazorCSharpFile(diagnostic.Location.Uri))
                //{
                //    // This location doesn't point to a virtual cs file. No need to remap.
                //    remappedLocations.Add(diagnostic);
                //    continue;
                //}

                //var razorDocumentUri = RazorLSPConventions.GetRazorDocumentUri(diagnostic.Location.Uri);
                //var mappingResult = await _documentMappingProvider.MapToDocumentRangesAsync(
                //    RazorLanguageKind.CSharp,
                //    razorDocumentUri,
                //    new[] { diagnostic.Location.Range },
                //    cancellationToken).ConfigureAwait(false);

                //if (mappingResult == null ||
                //    mappingResult.Ranges[0].IsUndefined() ||
                //    (_documentManager.TryGetDocument(razorDocumentUri, out var mappedDocumentSnapshot) &&
                //    mappingResult.HostDocumentVersion != mappedDocumentSnapshot.Version))
                //{
                //    // Couldn't remap the location or the document changed in the meantime. Discard this location.
                //    continue;
                //}

                //diagnostic.Location.Uri = razorDocumentUri;
                //diagnostic.DisplayPath = razorDocumentUri.AbsolutePath;
                //diagnostic.Location.Range = mappingResult.Ranges[0];

                //remappedLocations.Add(diagnostic);
            }

            return remappedLocations.ToArray();
        }

        // Temporary while the PartialResultToken serialization fix is in
        [DataContract]
        private class SerializableWorkspaceDocumentDiagnosticsParams
        {
            [DataMember(Name = "previousResults", IsRequired = false)]
            public DiagnosticParams[]? PreviousResults { get; set; }

            [DataMember(Name = "partialResultToken")]
            public string PartialResultToken { get; set; }

            //[DataMember(Name = "workDoneToken")]
            //public string WorkDoneToken { get; set; }
        }
    }
}
