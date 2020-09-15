// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(Methods.TextDocumentCodeActionName)]
    internal class CodeActionsHandler : IRequestHandler<CodeActionParams, VSCodeAction[]>
    {
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly LSPDocumentManager _documentManager;
        private readonly LSPProjectionProvider _projectionProvider;
        private readonly LSPDocumentMappingProvider _documentMappingProvider;

        [ImportingConstructor]
        public CodeActionsHandler(
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
        }

        public async Task<VSCodeAction[]> HandleRequestAsync(CodeActionParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
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
                return default;
            }

            var projectionResultStart = await _projectionProvider.GetProjectionAsync(documentSnapshot, request.Range.Start, cancellationToken).ConfigureAwait(false);
            if (projectionResultStart == null || projectionResultStart.LanguageKind != RazorLanguageKind.CSharp)
            {
                return default;
            }

            var projectionResultEnd = await _projectionProvider.GetProjectionAsync(documentSnapshot, request.Range.End, cancellationToken).ConfigureAwait(false);
            if (projectionResultEnd == null || projectionResultEnd.LanguageKind != RazorLanguageKind.CSharp)
            {
                return default;
            }

            if (projectionResultStart.Uri != projectionResultEnd.Uri ||
                projectionResultStart.HostDocumentVersion != projectionResultEnd.HostDocumentVersion)
            {
                return default;
            }



            cancellationToken.ThrowIfCancellationRequested();

            var codeActionParams = new CodeActionParams()
            {
                Context = request.Context,
                Range = new Range()
                {
                    Start = projectionResultStart.Position,
                    End = projectionResultEnd.Position
                },
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = projectionResultStart.Uri
                }
            };

            var results = await _requestInvoker.ReinvokeRequestOnServerAsync<CodeActionParams, VSCodeAction[]>(
                Methods.TextDocumentCodeActionName,
                LanguageServerKind.CSharp.ToContentType(),
                codeActionParams,
                cancellationToken).ConfigureAwait(false);

            if (results == null || results.Length == 0)
            {
                return Array.Empty<VSCodeAction>();
            }

            return Array.Empty<VSCodeAction>();
        }
    }
}
