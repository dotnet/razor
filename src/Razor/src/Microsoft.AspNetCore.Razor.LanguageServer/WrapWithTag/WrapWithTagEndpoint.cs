// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts.WrapWithTag;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.WrapWithTag
{
    internal class WrapWithTagEndpoint : IWrapWithTagEndpoint
    {
        private readonly ClientNotifierServiceBase _languageServer;
        private readonly RazorDocumentMappingService _razorDocumentMappingService;
        private readonly DocumentContextFactory _documentContextFactory;
        private readonly ILogger _logger;

        public WrapWithTagEndpoint(
            ClientNotifierServiceBase languageServer,
            DocumentContextFactory documentContextFactory,
            RazorDocumentMappingService razorDocumentMappingService,
            ILoggerFactory loggerFactory)
        {
            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            if (documentContextFactory is null)
            {
                throw new ArgumentNullException(nameof(documentContextFactory));
            }

            if (razorDocumentMappingService is null)
            {
                throw new ArgumentNullException(nameof(razorDocumentMappingService));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _languageServer = languageServer;
            _documentContextFactory = documentContextFactory;
            _razorDocumentMappingService = razorDocumentMappingService;
            _logger = loggerFactory.CreateLogger<WrapWithTagEndpoint>();
        }

        public async Task<WrapWithTagResponse?> Handle(WrapWithTagParamsBridge request, CancellationToken cancellationToken)
        {
            var documentContext = await _documentContextFactory.TryCreateAsync(request.TextDocument.Uri, cancellationToken).ConfigureAwait(false);
            if (documentContext is null)
            {
                _logger.LogWarning("Failed to find document {textDocumentUri}.", request.TextDocument.Uri);
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                _logger.LogWarning("Failed to retrieve generated output for document {textDocumentUri}.", request.TextDocument.Uri);
                return null;
            }

            var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
            if (request.Range?.Start.TryGetAbsoluteIndex(sourceText, _logger, out var hostDocumentIndex) != true)
            {
                return null;
            }

            // Since we're at the start of the selection, lets prefer the language to the right of the cursor if possible.
            // That way with the following situation:
            //
            // @if (true) {
            //   |<p></p>
            // }
            //
            // Instead of C#, which certainly would be expected to go in an if statement, we'll see HTML, which obviously
            // is the better choice for this operation.
            var languageKind = _razorDocumentMappingService.GetLanguageKind(codeDocument, hostDocumentIndex, rightAssociative: true);
            if (languageKind is not RazorLanguageKind.Html)
            {
                _logger.LogInformation("Unsupported language {languageKind:G}.", languageKind);
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var parameter = request;
            var response = await _languageServer.SendRequestAsync(LanguageServerConstants.RazorWrapWithTagEndpoint, parameter).ConfigureAwait(false);
            var htmlResponse = await response.Returning<WrapWithTagResponse>(cancellationToken).ConfigureAwait(false);

            if (htmlResponse.TextEdits is not null)
            {
                htmlResponse.TextEdits = await CleanUpTextEditsAsync(documentContext, htmlResponse.TextEdits, cancellationToken).ConfigureAwait(false);
            }

            return htmlResponse;
        }

        /// <summary>
        /// Sometimes the Html language server will send back an edit that contains a tilde, because the generated
        /// document we send them has lots of tildes. In those cases, we need to do some extra work to compute the
        /// minimal text edits
        /// </summary>
        // Internal for testing
        internal static async Task<TextEdit[]> CleanUpTextEditsAsync(DocumentContext documentContext, TextEdit[] edits, CancellationToken cancellationToken)
        {
            // Avoid computing a minimal diff if we don't need to
            if (!edits.Any(e => e.NewText.Contains("~")))
                return edits;

            // First we apply the edits that the Html language server wanted, to the Html document
            var htmlSourceText = await documentContext.GetHtmlSourceTextAsync(cancellationToken).ConfigureAwait(false);
            var textChanges = edits.Select(e => e.AsTextChange(htmlSourceText));
            var changedText = htmlSourceText.WithChanges(textChanges);

            // Now we use our minimal text differ algorithm to get the bare minimum of edits
            var minimalChanges = SourceTextDiffer.GetMinimalTextChanges(htmlSourceText, changedText, lineDiffOnly: false);
            var minimalEdits = minimalChanges.Select(f => f.AsTextEdit(htmlSourceText)).ToArray();

            return minimalEdits;
        }
    }
}
