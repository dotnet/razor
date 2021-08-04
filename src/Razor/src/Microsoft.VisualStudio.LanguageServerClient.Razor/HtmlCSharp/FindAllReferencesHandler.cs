﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(Methods.TextDocumentReferencesName)]
    internal class FindAllReferencesHandler :
        LSPProgressListenerHandlerBase<ReferenceParams, VSInternalReferenceItem[]>,
        IRequestHandler<ReferenceParams, VSInternalReferenceItem[]>
    {
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly LSPDocumentManager _documentManager;
        private readonly LSPProjectionProvider _projectionProvider;
        private readonly LSPDocumentMappingProvider _documentMappingProvider;
        private readonly LSPProgressListener _lspProgressListener;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public FindAllReferencesHandler(
            LSPRequestInvoker requestInvoker,
            LSPDocumentManager documentManager,
            LSPProjectionProvider projectionProvider,
            LSPDocumentMappingProvider documentMappingProvider,
            LSPProgressListener lspProgressListener,
            HTMLCSharpLanguageServerLogHubLoggerProvider loggerProvider)
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

            if (loggerProvider is null)
            {
                throw new ArgumentNullException(nameof(loggerProvider));
            }

            _requestInvoker = requestInvoker;
            _documentManager = documentManager;
            _projectionProvider = projectionProvider;
            _documentMappingProvider = documentMappingProvider;
            _lspProgressListener = lspProgressListener;

            _logger = loggerProvider.CreateLogger(nameof(FindAllReferencesHandler));
        }

        // Internal for testing
        internal async override Task<VSInternalReferenceItem[]> HandleRequestAsync(ReferenceParams request, ClientCapabilities clientCapabilities, string token, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (clientCapabilities is null)
            {
                throw new ArgumentNullException(nameof(clientCapabilities));
            }

            _logger.LogInformation($"Starting request for {request.TextDocument.Uri}.");

            if (!_documentManager.TryGetDocument(request.TextDocument.Uri, out var documentSnapshot))
            {
                _logger.LogWarning($"Failed to find document {request.TextDocument.Uri}.");
                return null;
            }

            var projectionResult = await _projectionProvider.GetProjectionAsync(
                documentSnapshot,
                request.Position,
                cancellationToken).ConfigureAwait(false);
            if (projectionResult == null)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var referenceParams = new SerializableReferenceParams()
            {
                Position = projectionResult.Position,
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = projectionResult.Uri
                },
                Context = request.Context,
                PartialResultToken = token // request.PartialResultToken
            };

            _logger.LogInformation("Attaching to progress listener.");

            if (!_lspProgressListener.TryListenForProgress(
                token,
                onProgressNotifyAsync: (value, ct) => ProcessReferenceItemsAsync(value, request.PartialResultToken, ct),
                DelayAfterLastNotifyAsync,
                cancellationToken,
                out var onCompleted))
            {
                _logger.LogWarning("Failed to attach to progress listener.");
                return null;
            }

            _logger.LogInformation($"Requesting references for {projectionResult.Uri}.");

            var response = await _requestInvoker.ReinvokeRequestOnServerAsync<SerializableReferenceParams, VSInternalReferenceItem[]>(
                Methods.TextDocumentReferencesName,
                projectionResult.LanguageKind.ToContainedLanguageServerName(),
                referenceParams,
                cancellationToken).ConfigureAwait(false);
            var result = response.Result;

            if (result is null)
            {
                _logger.LogInformation("Received no results from initial request.");
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Waiting on progress notifications.");

            // We must not return till we have received the progress notifications
            // and reported the results via the PartialResultToken
            await onCompleted.ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Finished waiting, remapping results.");

            // Results returned through Progress notification
            var remappedResults = await RemapReferenceItemsAsync(result, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation($"Returning {remappedResults.Length} results.");

            return remappedResults;

            // Local functions
            async Task DelayAfterLastNotifyAsync(CancellationToken cancellationToken)
            {
                using var combined = ImmediateNotificationTimeout.CombineWith(cancellationToken);

                try
                {
                    await Task.Delay(WaitForProgressNotificationTimeout, combined.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException) when (ImmediateNotificationTimeout.IsCancellationRequested)
                {
                    // The delay was requested to complete immediately
                }
            }
        }

        private async Task ProcessReferenceItemsAsync(
            JToken value,
            IProgress<object> progress,
            CancellationToken cancellationToken)
        {
            var result = value.ToObject<VSInternalReferenceItem[]>();

            if (result == null || result.Length == 0)
            {
                _logger.LogInformation("Received empty progress notification");
                return;
            }

            _logger.LogInformation($"Received {result.Length} references, remapping.");
            var remappedResults = await RemapReferenceItemsAsync(result, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation($"Reporting {remappedResults.Length} results.");
            progress.Report(remappedResults);
        }

        private async Task<VSInternalReferenceItem[]> RemapReferenceItemsAsync(VSInternalReferenceItem[] result, CancellationToken cancellationToken)
        {
            var remappedLocations = new List<VSInternalReferenceItem>();

            foreach (var referenceItem in result)
            {
                if (referenceItem?.Location is null || referenceItem.Text is null)
                {
                    continue;
                }

                // Temporary fix for codebehind leaking through
                // Revert when https://github.com/dotnet/aspnetcore/issues/22512 is resolved
                referenceItem.DefinitionText = FilterReferenceDisplayText(referenceItem.DefinitionText);
                referenceItem.Text = FilterReferenceDisplayText(referenceItem.Text);

                // Indicates the reference item is directly available in the code
                referenceItem.Origin = VSInternalItemOrigin.Exact;

                if (!RazorLSPConventions.IsVirtualCSharpFile(referenceItem.Location.Uri) &&
                    !RazorLSPConventions.IsVirtualHtmlFile(referenceItem.Location.Uri))
                {
                    // This location doesn't point to a virtual cs file. No need to remap.
                    remappedLocations.Add(referenceItem);
                    continue;
                }

                var razorDocumentUri = RazorLSPConventions.GetRazorDocumentUri(referenceItem.Location.Uri);
                var languageKind = RazorLSPConventions.IsVirtualCSharpFile(referenceItem.Location.Uri) ? RazorLanguageKind.CSharp : RazorLanguageKind.Html;
                var mappingResult = await _documentMappingProvider.MapToDocumentRangesAsync(
                    languageKind,
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

        private static object FilterReferenceDisplayText(object referenceText)
        {
            const string CodeBehindObjectPrefix = "__o = ";
            const string CodeBehindBackingFieldSuffix = "k__BackingField";

            if (referenceText is string text)
            {
                if (text.StartsWith(CodeBehindObjectPrefix, StringComparison.Ordinal))
                {
                    return text
                        .Substring(CodeBehindObjectPrefix.Length, text.Length - CodeBehindObjectPrefix.Length - 1); // -1 for trailing `;`
                }

                return text.Replace(CodeBehindBackingFieldSuffix, string.Empty);
            }

            if (referenceText is ClassifiedTextElement textElement &&
                FilterReferenceClassifiedRuns(textElement.Runs.ToArray()))
            {
                var filteredRuns = textElement.Runs.Skip(4); // `__o`, ` `, `=`, ` `
                filteredRuns = filteredRuns.Take(filteredRuns.Count() - 1); // Trailing `;`
                return new ClassifiedTextElement(filteredRuns);
            }

            return referenceText;
        }

        private static bool FilterReferenceClassifiedRuns(IReadOnlyList<ClassifiedTextRun> runs)
        {
            if (runs.Count < 5)
            {
                return false;
            }

            return VerifyRunMatches(runs[0], "field name", "__o") &&
                VerifyRunMatches(runs[1], "text", " ") &&
                VerifyRunMatches(runs[2], "operator", "=") &&
                VerifyRunMatches(runs[3], "text", " ") &&
                VerifyRunMatches(runs[runs.Count - 1], "punctuation", ";");

            static bool VerifyRunMatches(ClassifiedTextRun run, string expectedClassificationType, string expectedText)
            {
                return run.ClassificationTypeName == expectedClassificationType &&
                    run.Text == expectedText;
            }
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
