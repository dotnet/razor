// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Composition;
using System.Linq;
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
        private static readonly string[] CSharpTriggerCharacters = new[] { ".", "@" };
        private static readonly string[] HtmlTriggerCharacters = new[] { "<", "&", "\\", "/", "'", "\"", "=", ":" };

        [ImportingConstructor]
        public CompletionHandler(
            JoinableTaskContext joinableTaskContext,
            ILanguageClientBroker languageClientBroker,
            LSPDocumentManager documentManager,
            LSPDocumentSynchronizer documentSynchronizer,
            RazorLogger logger) : base(joinableTaskContext, languageClientBroker, documentManager, documentSynchronizer, logger)
        {
        }

        public async Task<SumType<CompletionItem[], CompletionList>?> HandleRequestAsync(CompletionParams request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (!DocumentManager.TryGetDocument(request.TextDocument.Uri, out var documentSnapshot))
            {
                return null;
            }

            // Switch to a background thread.
            await TaskScheduler.Default;

            var projectionResult = await GetProjectionAsync(documentSnapshot, request.Position, cancellationToken);
            if (projectionResult == null)
            {
                return null;
            }

            if (request.Context.TriggerKind == CompletionTriggerKind.TriggerCharacter &&
                !IsApplicableTriggerCharacter(request.Context.TriggerCharacter, projectionResult.LanguageKind))
            {
                // We were triggered but the trigger character doesn't make sense for the current cursor position. Bail.
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

        private bool IsApplicableTriggerCharacter(string triggerCharacter, RazorLanguageKind languageKind)
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
