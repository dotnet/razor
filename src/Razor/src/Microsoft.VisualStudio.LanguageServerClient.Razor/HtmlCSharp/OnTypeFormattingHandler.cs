// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(Methods.TextDocumentOnTypeFormattingName)]
    internal class OnTypeFormattingHandler : IRequestHandler<DocumentOnTypeFormattingParams, TextEdit[]>
    {
        private static readonly TextEdit[] EmptyEdits = Array.Empty<TextEdit>();

        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly LSPDocumentManager _documentManager;
        private readonly SVsServiceProvider _serviceProvider;
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly LSPProjectionProvider _projectionProvider;
        private readonly LSPDocumentMappingProvider _documentMappingProvider;

        [ImportingConstructor]
        public OnTypeFormattingHandler(
            JoinableTaskContext joinableTaskContext,
            LSPDocumentManager documentManager,
            SVsServiceProvider serviceProvider,
            LSPRequestInvoker requestInvoker,
            LSPProjectionProvider projectionProvider,
            LSPDocumentMappingProvider documentMappingProvider)
        {
            if (joinableTaskContext is null)
            {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            if (documentManager is null)
            {
                throw new ArgumentNullException(nameof(documentManager));
            }

            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
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

            _joinableTaskFactory = joinableTaskContext.Factory;
            _documentManager = documentManager;
            _serviceProvider = serviceProvider;
            _requestInvoker = requestInvoker;
            _projectionProvider = projectionProvider;
            _documentMappingProvider = documentMappingProvider;
        }

        public async Task<TextEdit[]> HandleRequestAsync(DocumentOnTypeFormattingParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            if (request.Character != ">")
            {
                // We currently only support auto-closing tags feature.
                return EmptyEdits;
            }

            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (!_documentManager.TryGetDocument(request.TextDocument.Uri, out var documentSnapshot))
            {
                return EmptyEdits;
            }

            // Switch to a background thread.
            await TaskScheduler.Default;

            var projectionResult = await _projectionProvider.GetProjectionAsync(documentSnapshot, request.Position, cancellationToken).ConfigureAwait(false);
            if (projectionResult == null || projectionResult.LanguageKind != RazorLanguageKind.Html)
            {
                return EmptyEdits;
            }

            if (request.Options.OtherOptions == null)
            {
                request.Options.OtherOptions = new Dictionary<string, object>();
            }
            request.Options.OtherOptions[LanguageServerConstants.ExpectsCursorPlaceholderKey] = true;

            var formattingParams = new DocumentOnTypeFormattingParams()
            {
                Character = request.Character,
                Options = request.Options,
                Position = projectionResult.Position,
                TextDocument = new TextDocumentIdentifier() { Uri = projectionResult.Uri }
            };

            var serverKind = projectionResult.LanguageKind == RazorLanguageKind.CSharp ? LanguageServerKind.CSharp : LanguageServerKind.Html;
            var edits = await _requestInvoker.RequestServerAsync<DocumentOnTypeFormattingParams, TextEdit[]>(
                Methods.TextDocumentCompletionName,
                serverKind,
                formattingParams,
                cancellationToken).ConfigureAwait(false);

            if (edits == null)
            {
                return EmptyEdits;
            }

            var mappedEdits = new List<TextEdit>();
            foreach (var edit in edits)
            {
                if (edit.Range == null || edit.NewText == null)
                {
                    // Sometimes the HTML language server returns invalid edits like these. We should just ignore those.
                    continue;
                }

                var mappingResult = await _documentMappingProvider.RazorMapToDocumentRangeAsync(projectionResult.LanguageKind, request.TextDocument.Uri, edit.Range, cancellationToken).ConfigureAwait(false);

                if (mappingResult == null || mappingResult.HostDocumentVersion != documentSnapshot.Version)
                {
                    // Couldn't remap the edits properly. Discard this request.
                    return EmptyEdits;
                }

                var mappedEdit = new TextEdit()
                {
                    NewText = edit.NewText,
                    Range = mappingResult.Range
                };
                mappedEdits.Add(mappedEdit);
            }

            if (mappedEdits.Count == 0)
            {
                return EmptyEdits;
            }

            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (!_documentManager.TryGetDocument(request.TextDocument.Uri, out var newDocumentSnapshot) ||
                newDocumentSnapshot.Version != documentSnapshot.Version)
            {
                // The document changed while were working on the background. Discard this request.
                return EmptyEdits;
            }

            VsUtilities.ApplyTextEdits(_serviceProvider, documentSnapshot.Uri, documentSnapshot.Snapshot, mappedEdits);

            // We would have already applied the edits and moved the cursor. Return empty.
            return EmptyEdits;
        }
    }
}
