// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
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

        public WrapWithTagEndpoint(
            ClientNotifierServiceBase languageServer,
            RazorDocumentMappingService razorDocumentMappingService)
        {
            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            if (razorDocumentMappingService is null)
            {
                throw new ArgumentNullException(nameof(razorDocumentMappingService));
            }

            _languageServer = languageServer;
            _razorDocumentMappingService = razorDocumentMappingService;
        }

        public bool MutatesSolutionState => false;

        public TextDocumentIdentifier GetTextDocumentIdentifier(WrapWithTagParams request)
        {
            return request.TextDocument;
        }

        public async Task<WrapWithTagResponse?> HandleRequestAsync(WrapWithTagParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
        {
            var documentContext = requestContext.DocumentContext;
            if (documentContext is null)
            {
                requestContext.Logger.LogWarning("Failed to find document {textDocumentUri}.", request.TextDocument.Uri);
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                requestContext.Logger.LogWarning("Failed to retrieve generated output for document {textDocumentUri}.", request.TextDocument.Uri);
                return null;
            }

            var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
            if (request.Range?.Start.TryGetAbsoluteIndex(sourceText, requestContext.Logger, out var hostDocumentIndex) != true)
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
                requestContext.Logger.LogInformation("Unsupported language {languageKind:G}.", languageKind);
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var versionedIdentifier = new VersionedTextDocumentIdentifier {
                Uri = request.TextDocument.Uri,
                Version = documentContext.Version,
            };

            request.TextDocument = versionedIdentifier;
            var parameter = request;

            var htmlResponse = await _languageServer.SendRequestAsync<WrapWithTagParams, WrapWithTagResponse>(
                LanguageServerConstants.RazorWrapWithTagEndpoint,
                parameter,
                cancellationToken).ConfigureAwait(false);

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
