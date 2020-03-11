// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(Methods.TextDocumentCompletionName)]
    internal class CompletionHandler : RazorLSPHandlerBase, IRequestHandler<CompletionParams, SumType<CompletionItem[], CompletionList>?>
    {
        [ImportingConstructor]
        public CompletionHandler(
            JoinableTaskContext joinableTaskContext,
            ILanguageClientBroker languageClientBroker,
            LSPDocumentManager documentManager,
            LSPDocumentSynchronizer documentSynchronizer) : base(joinableTaskContext, languageClientBroker, documentManager, documentSynchronizer)
        {
        }

        public async Task<SumType<CompletionItem[], CompletionList>?> HandleRequestAsync(CompletionParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (!DocumentManager.TryGetDocument(request.TextDocument.Uri, out var documentSnapshot))
            {
                return null;
            }

            var projectionResult = await GetProjectionAsync(documentSnapshot, request.Position, cancellationToken);
            if (projectionResult == null)
            {
                return null;
            }

            var completionParams = new CompletionParams()
            {
                Context = request.Context,
                Position = projectionResult.Position,
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = projectionResult.Uri
                }
            };

            var serverKind = projectionResult.LanguageKind == RazorLanguageKind.CSharp ? LanguageServerKind.CSharp : LanguageServerKind.Html;
            var result = await RequestServerAsync<CompletionParams, SumType<CompletionItem[], CompletionList>?>(
                LanguageClientBroker,
                Methods.TextDocumentCompletionName,
                serverKind,
                completionParams,
                cancellationToken);

            return result;
        }
    }
}
