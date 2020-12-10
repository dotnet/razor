// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage.MessageInterception;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp;
using System.Linq;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Export(typeof(MessageInterceptor))]
    [InterceptMethod(Methods.TextDocumentPublishDiagnosticsName)]
    internal class RazorHtmlPublishDiagnosticsInterceptor : MessageInterceptor
    {
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly LSPDocumentManager _documentManager;
        private readonly LSPDocumentMappingProvider _documentMappingProvider;
        private readonly LSPDocumentSynchronizer _documentSynchronizer;

        [ImportingConstructor]
        public RazorHtmlPublishDiagnosticsInterceptor(
            LSPRequestInvoker requestInvoker,
            LSPDocumentManager documentManager,
            LSPDocumentMappingProvider documentMappingProvider,
            LSPDocumentSynchronizer documentSynchronizer)
        {
            if (requestInvoker is null)
            {
                throw new ArgumentNullException(nameof(requestInvoker));
            }

            if (documentManager is null)
            {
                throw new ArgumentNullException(nameof(documentManager));
            }

            if (documentMappingProvider is null)
            {
                throw new ArgumentNullException(nameof(documentMappingProvider));
            }

            if (documentSynchronizer is null)
            {
                throw new ArgumentNullException(nameof(documentSynchronizer));
            }

            _requestInvoker = requestInvoker;
            _documentManager = documentManager;
            _documentMappingProvider = documentMappingProvider;
            _documentSynchronizer = documentSynchronizer;
        }

        public override async Task<InterceptionResult> ApplyChangesAsync(JToken token, string containedLanguageName, CancellationToken cancellationToken)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var diagnosticParams = token.ToObject<VSPublishDiagnosticParams>();

            if (diagnosticParams is null)
            {
                throw new ArgumentException("Conversion of token failed.");
            }

            // We only support interception of Virtual HTML Files
            if (!RazorLSPConventions.IsVirtualHtmlFile(diagnosticParams.Uri))
            {
                return CreateDefaultResponse(token);
            }

            var razorDocumentUri = RazorLSPConventions.GetRazorDocumentUri(diagnosticParams.Uri);

            if (!_documentManager.TryGetDocument(razorDocumentUri, out var razorDocumentSnapshot))
            {
                return CreateDefaultResponse(token);
            }

            if (!razorDocumentSnapshot.TryGetVirtualDocument<HtmlVirtualDocumentSnapshot>(out var htmlDocumentSnapshot))
            {
                return CreateDefaultResponse(token);
            }

            // Ensure we're working with the virtual document specified in the diagnostic params
            if (!htmlDocumentSnapshot.Uri.Equals(diagnosticParams.Uri))
            {
                return CreateDefaultResponse(token);
            }

            // Note; this is an `interceptor` & not a handler, hence 
            // it's possible another interceptor mutates this request 
            // later in the toolchain. Such an interceptor would likely 
            // expect a `__virtual.html` suffix instead of `.razor`.
            diagnosticParams.Uri = razorDocumentUri;

            // Return early if there aren't any diagnostics to process
            if (diagnosticParams.Diagnostics?.Any() != true)
            {
                return CreateResponse(diagnosticParams);
            }

            var synchronized = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync(
                razorDocumentSnapshot.Version,
                htmlDocumentSnapshot,
                cancellationToken).ConfigureAwait(false);
            if (!synchronized)
            {
                // Could not synchronize, we'll clear out the diagnostics as we don't
                // know whether or not they're still valid in the current state of the
                // document.
                return CreateResponse(diagnosticParams, clearDiagnostics: true);
            }

            var unmappedDiagnostics = diagnosticParams.Diagnostics;
            var filteredDiagnostics = unmappedDiagnostics.Where(d => !CanDiagnosticBeFiltered(d));
            if (!filteredDiagnostics.Any())
            {
                return CreateResponse(diagnosticParams, clearDiagnostics: true);
            }

            var rangesToMap = filteredDiagnostics.Select(r => r.Range).ToArray();
            var mappingResult = await _documentMappingProvider.MapToDocumentRangesAsync(
                RazorLanguageKind.Html,
                razorDocumentUri,
                rangesToMap,
                LanguageServerMappingBehavior.Inclusive,
                cancellationToken).ConfigureAwait(false);

            if (mappingResult == null || mappingResult.HostDocumentVersion != razorDocumentSnapshot.Version)
            {
                return CreateResponse(diagnosticParams, clearDiagnostics: true);
            }

            var mappedDiagnostics = new List<Diagnostic>(filteredDiagnostics.Count());

            for (var i = 0; i < filteredDiagnostics.Count(); i++)
            {
                var diagnostic = filteredDiagnostics.ElementAt(i);
                var range = mappingResult.Ranges[i];

                if (range.IsUndefined())
                {
                    // Couldn't remap the range correctly.
                    // If this isn't an `Error` Severity Diagnostic we can discard it.
                    if (diagnostic.Severity != DiagnosticSeverity.Error)
                    {
                        continue;
                    }

                    // For `Error` Severity diagnostics we still show the diagnostics to
                    // the user, however we set the range to an undefined range to ensure
                    // clicking on the diagnostic doesn't cause errors.
                }

                diagnostic.Range = range;
                mappedDiagnostics.Add(diagnostic);
            }

            diagnosticParams.Diagnostics = mappedDiagnostics.ToArray();

            return CreateResponse(diagnosticParams);


            static InterceptionResult CreateDefaultResponse(JToken token) =>
                new InterceptionResult(token, changedDocumentUri: false);

            static InterceptionResult CreateResponse(
                VSPublishDiagnosticParams diagnosticParams,
                bool clearDiagnostics = false,
                bool changedDocumentUri = true)
            {
                if (clearDiagnostics)
                {
                    diagnosticParams.Diagnostics = Array.Empty<Diagnostic>();
                }

                var newToken = JToken.FromObject(diagnosticParams);
                return new InterceptionResult(newToken, changedDocumentUri);
            }

            static bool CanDiagnosticBeFiltered(Diagnostic d) => false;
            // TODO:
            // DiagnosticsToIgnore.Contains(d.Code) && d.Severity != DiagnosticSeverity.Error; 
        }
    }
}
