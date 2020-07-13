// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(Methods.TextDocumentOnTypeFormattingName)]
    internal class OnTypeFormattingHandler : IRequestHandler<DocumentOnTypeFormattingParams, TextEdit[]>
    {
        private static readonly IReadOnlyList<string> CSharpTriggerCharacters = new[] { "}", ";" };
        private static readonly IReadOnlyList<string> HtmlTriggerCharacters = Array.Empty<string>();
        private static readonly IReadOnlyList<string> AllTriggerCharacters = CSharpTriggerCharacters.Concat(HtmlTriggerCharacters).ToArray();

        private readonly LSPDocumentManager _documentManager;
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly LSPProjectionProvider _projectionProvider;
        private readonly LSPDocumentMappingProvider _documentMappingProvider;

        [ImportingConstructor]
        public OnTypeFormattingHandler(
            LSPDocumentManager documentManager,
            LSPRequestInvoker requestInvoker,
            LSPProjectionProvider projectionProvider,
            LSPDocumentMappingProvider documentMappingProvider)
        {
            if (documentManager is null)
            {
                throw new ArgumentNullException(nameof(documentManager));
            }

            if (requestInvoker is null)
            {
                throw new ArgumentNullException(nameof(requestInvoker));
            }

            if (projectionProvider is null)
            {
                throw new ArgumentNullException(nameof(projectionProvider));
            }

            if (documentMappingProvider is null)
            {
                throw new ArgumentNullException(nameof(documentMappingProvider));
            }

            _documentManager = documentManager;
            _requestInvoker = requestInvoker;
            _projectionProvider = projectionProvider;
            _documentMappingProvider = documentMappingProvider;
        }

        public async Task<TextEdit[]> HandleRequestAsync(DocumentOnTypeFormattingParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            if (!AllTriggerCharacters.Contains(request.Character, StringComparer.Ordinal))
            {
                // Unexpected trigger character.
                return null;
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

            if (!IsApplicableTriggerCharacter(request.Character, projectionResult.LanguageKind))
            {
                // We were triggered but the trigger character doesn't make sense for the current cursor position. Bail.
                return null;
            }

            var formattingParams = new DocumentOnTypeFormattingParams()
            {
                Character = request.Character,
                Options = request.Options,
                Position = projectionResult.Position,
                TextDocument = new TextDocumentIdentifier() { Uri = projectionResult.Uri }
            };

            cancellationToken.ThrowIfCancellationRequested();

            var serverKind = projectionResult.LanguageKind == RazorLanguageKind.CSharp ? LanguageServerKind.CSharp : LanguageServerKind.Html;
            var response = await _requestInvoker.ReinvokeRequestOnServerAsync<DocumentOnTypeFormattingParams, TextEdit[]>(
                Methods.TextDocumentOnTypeFormattingName,
                serverKind,
                formattingParams,
                cancellationToken).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var remappedEdits = await _documentMappingProvider.RemapFormattedTextEditsAsync(projectionResult.Uri, response, request.Options, cancellationToken).ConfigureAwait(false);
            return remappedEdits;
        }

        private static bool IsApplicableTriggerCharacter(string triggerCharacter, RazorLanguageKind languageKind)
        {
            if (languageKind == RazorLanguageKind.CSharp)
            {
                return CSharpTriggerCharacters.Contains(triggerCharacter);
            }
            else if (languageKind == RazorLanguageKind.Html)
            {
                return HtmlTriggerCharacters.Contains(triggerCharacter);
            }

            // Unknown trigger character.
            return false;
        }
    }
}
