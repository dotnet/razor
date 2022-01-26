// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(VSInternalMethods.TextDocumentInlineCompletionName)]
    internal class InlineCompletionHandler : IRequestHandler<VSInternalInlineCompletionRequest, VSInternalInlineCompletionList?>
    {
        internal static readonly ImmutableHashSet<string> CSharpKeywords = ImmutableHashSet.Create(
            "~", "Attribute", "checked", "class", "ctor", "cw", "do", "else", "enum", "equals", "Exception", "for", "foreach", "forr",
            "if", "indexer", "interface", "invoke", "iterator", "iterindex", "lock", "mbox", "namespace", "#if", "#region", "prop",
            "propfull", "propg", "sim", "struct", "svm", "switch", "try", "tryf", "unchecked", "unsafe", "using", "while");

        private readonly LSPDocumentManager _documentManager;
        private readonly LSPRequestInvoker _requestInvoker;
        private readonly LSPProjectionProvider _projectionProvider;
        private readonly LSPDocumentMappingProvider _documentMappingProvider;
        private readonly FormattingOptionsProvider _formattingOptionsProvider;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public InlineCompletionHandler(
            LSPDocumentManager documentManager,
            LSPRequestInvoker requestInvoker,
            LSPProjectionProvider projectionProvider,
            LSPDocumentMappingProvider documentMappingProvider,
            HTMLCSharpLanguageServerLogHubLoggerProvider loggerProvider,
            FormattingOptionsProvider formattingOptionsProvider)
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

            if (loggerProvider is null)
            {
                throw new ArgumentNullException(nameof(loggerProvider));
            }

            if (formattingOptionsProvider is null)
            {
                throw new ArgumentNullException(nameof(formattingOptionsProvider));
            }

            _documentManager = documentManager;
            _requestInvoker = requestInvoker;
            _projectionProvider = projectionProvider;
            _documentMappingProvider = documentMappingProvider;
            _formattingOptionsProvider = formattingOptionsProvider;

            _logger = loggerProvider.CreateLogger(nameof(InlineCompletionHandler));
        }

        public async Task<VSInternalInlineCompletionList?> HandleRequestAsync(VSInternalInlineCompletionRequest request, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            _logger.LogInformation($"Starting request for {request.TextDocument.Uri} at {request.Position}.");

            if (!_documentManager.TryGetDocument(request.TextDocument.Uri, out var documentSnapshot))
            {
                return null;
            }

            var projectionResult = await _projectionProvider.GetProjectionAsync(documentSnapshot, request.Position, cancellationToken).ConfigureAwait(false);
            if (projectionResult is null)
            {
                _logger.LogWarning($"Failed to find document {request.TextDocument.Uri}.");
                return null;
            }

            if (projectionResult.LanguageKind != RazorLanguageKind.CSharp)
            {
                _logger.LogInformation($"Inline completions not supported for {projectionResult.LanguageKind}");
                return null;
            }

            var inlineCompletionParams = new VSInternalInlineCompletionRequest
            {
                Context = request.Context,
                TextDocument = new TextDocumentIdentifier { Uri = projectionResult.Uri },
                Position = projectionResult.Position,
            };

            _logger.LogInformation($"Requesting inline completions for {projectionResult.Uri}.");

            var serverKind = projectionResult.LanguageKind.ToLanguageServerKind();
            var textBuffer = serverKind.GetTextBuffer(documentSnapshot);
            var languageServerName = serverKind.ToLanguageServerName();
            var response = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalInlineCompletionRequest, VSInternalInlineCompletionList?>(
                textBuffer,
                VSInternalMethods.TextDocumentInlineCompletionName,
                languageServerName,
                inlineCompletionParams,
                cancellationToken).ConfigureAwait(false);

            if (!ReinvocationResponseHelper.TryExtractResultOrLog(response, _logger, languageServerName, out var result))
            {
                return null;
            }

            if (response?.Response == null)
            {
                _logger.LogInformation($"Did not get any items for {projectionResult.LanguageKind}");
                return null;
            }

            _logger.LogInformation("Received result, remapping.");

            var formattingOptions = _formattingOptionsProvider.GetOptions(documentSnapshot);

            var items = new List<VSInternalInlineCompletionItem>();
            foreach (var item in response.Response.Items)
            {
                var containsSnippet = item.TextFormat == InsertTextFormat.Snippet;
                var range = item.Range ?? new Range { Start = projectionResult.Position, End = projectionResult.Position };

                var textEdit = new TextEdit { NewText = item.Text, Range = range };
                var remappedEdit = await _documentMappingProvider.RemapFormattedTextEditsAsync(projectionResult.Uri, new[] { textEdit }, formattingOptions, containsSnippet, cancellationToken).ConfigureAwait(false);

                if (!remappedEdit.Any())
                {
                    _logger.LogInformation("Discarding inline completion item after remapping");
                    continue;
                }

                var remappedItem = new VSInternalInlineCompletionItem
                {
                    Command = item.Command,
                    Range = remappedEdit.Single().Range,
                    Text = remappedEdit.Single().NewText,
                    TextFormat = item.TextFormat,
                };
                items.Add(remappedItem);
            }

            _logger.LogInformation($"Returning items.");
            return new VSInternalInlineCompletionList
            {
                Items = items.ToArray()
            };
        }
    }
}

